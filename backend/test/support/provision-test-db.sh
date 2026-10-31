#!/usr/bin/env bash
# Crea (up) o destruye (down) un PostgreSQL LOCAL, TEMPORAL y AISLADO para las pruebas de integración.
# Nunca toca Supabase ni lee .env.
#
#   test/support/provision-test-db.sh up   <directorio-temporal>   # imprime TEST_DATABASE_URL
#   test/support/provision-test-db.sh down <directorio-temporal>
#
# up reproduce el orden real de despliegue, con roles que simulan Supabase (postgres sin superusuario,
# anon, authenticated, service_role):
#   001_roles.sql -> prisma migrate deploy (como senas_migrator, solo contra esta base local)
#   -> 002_permissions.sql -> 003_rls.sql -> 004_verify.sql
# La URL devuelta usa el rol de ejecución `senas_app` (privilegios mínimos + RLS), igual que producción.
#
# Variables: PG_BIN (carpeta bin de PostgreSQL), PG_TEST_PORT (por defecto 54330).
set -euo pipefail

ACTION="${1:?uso: provision-test-db.sh up|down <directorio-temporal>}"
DIR="${2:?falta el directorio temporal}"
PG_BIN="${PG_BIN:-/c/Program Files/PostgreSQL/18/bin}"
PORT="${PG_TEST_PORT:-54330}"
DB=senas_test
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
export PGHOST=127.0.0.1 PGPORT="$PORT" PGCLIENTENCODING=UTF8 LC_MESSAGES=C

psql_as() { local user=$1 db=$2; shift 2; PGUSER="$user" "$PG_BIN/psql" -X -q -v ON_ERROR_STOP=1 -d "$db" "$@"; }

if [ "$ACTION" = "down" ]; then
  "$PG_BIN/pg_ctl" -D "$DIR/data" -m immediate stop >/dev/null 2>&1 || true
  rm -rf "$DIR"
  echo "PostgreSQL temporal eliminado" >&2
  exit 0
fi

rm -rf "$DIR"; mkdir -p "$DIR"
"$PG_BIN/initdb" -D "$DIR/data" -U tmp --auth=trust -E UTF8 >/dev/null
"$PG_BIN/pg_ctl" -D "$DIR/data" -o "-p $PORT -c listen_addresses=127.0.0.1" -l "$DIR/pg.log" -w start >/dev/null

psql_as tmp postgres -c "CREATE ROLE postgres LOGIN CREATEROLE CREATEDB REPLICATION BYPASSRLS"
psql_as tmp postgres -c "CREATE ROLE anon LOGIN"
psql_as tmp postgres -c "CREATE ROLE authenticated LOGIN"
psql_as tmp postgres -c "CREATE ROLE service_role LOGIN BYPASSRLS"
psql_as tmp postgres -c "CREATE DATABASE $DB OWNER postgres"
psql_as tmp "$DB" -c "
  ALTER SCHEMA public OWNER TO postgres;
  GRANT ALL ON SCHEMA public TO anon, authenticated, service_role;
  ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public GRANT ALL ON TABLES TO anon, authenticated, service_role;"

psql_as postgres "$DB" -f "$ROOT/prisma/security/001_roles.sql"

# Migración con el flujo real de Prisma, usando el rol migrador (solo esta base local).
(cd "$ROOT" && DOTENV_CONFIG_PATH=/no/existe.env MIGRATION_DATABASE_URL="postgresql://senas_migrator@127.0.0.1:$PORT/$DB" \
  npx prisma migrate deploy >&2)

psql_as senas_migrator "$DB" -f "$ROOT/prisma/security/002_permissions.sql"
psql_as senas_migrator "$DB" -f "$ROOT/prisma/security/003_rls.sql"
psql_as postgres "$DB" -f "$ROOT/prisma/security/004_verify.sql" >/dev/null

echo "postgresql://senas_app@127.0.0.1:$PORT/$DB"
