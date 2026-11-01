-- ============================================================================
-- 001_roles.sql  -  PROPUESTA PENDIENTE DE REVISION. NO SE EJECUTA AUTOMATICAMENTE.
-- ============================================================================
-- Cuando:   ANTES de aplicar la migracion inicial.
-- Quien:    un administrador del proyecto Supabase (rol `postgres`, que tiene
--           CREATEROLE), desde el SQL Editor o psql.
-- Que hace: crea los dos roles de la aplicacion y concede al de migraciones
--           USAGE y CREATE sobre el schema public. NO crea tablas, NO habilita RLS.
--
-- Roles:
--   senas_migrator : ejecuta `prisma migrate deploy`. Sera PROPIETARIO de las
--                    tablas. Jamas se usa en la aplicacion en ejecucion.
--   senas_app      : lo usa NestJS en ejecucion. Privilegios minimos, sin
--                    BYPASSRLS, no es propietario de nada.
--
-- CONTRASEÑAS: este archivo NO contiene contraseñas. Los roles se crean con LOGIN
-- pero sin contraseña (no pueden iniciar sesion). El administrador las asigna a
-- mano, con valores aleatorios generados por el y guardados fuera de Git:
--     ALTER ROLE senas_migrator PASSWORD '<generada>';
--     ALTER ROLE senas_app      PASSWORD '<generada>';
-- (ejecutar una a una; no dejarlas en un historial compartido).
--
-- Con el pooler de Supabase (Supavisor) el usuario de conexion es
-- `<rol>.<project_ref>`, por ejemplo `senas_app.<project_ref>`.
--
-- Si se usa el SQL Editor de Supabase (no admite meta-comandos de psql),
-- eliminar las lineas que comienzan con "\".
--
-- Sin CONNECTION LIMIT: no hay evidencia de que 20/5 sean adecuados para el pooler
-- (Supavisor); el limite efectivo lo gobierna el pool de Supabase.
-- Transaccional: si algo falla, no queda ningun cambio parcial.
-- Idempotente: puede ejecutarse varias veces.
-- ============================================================================

\set ON_ERROR_STOP on

BEGIN;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'senas_migrator') THEN
    CREATE ROLE senas_migrator LOGIN;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'senas_app') THEN
    CREATE ROLE senas_app LOGIN;
  END IF;
END
$$;

-- NO se usan clausulas NOSUPERUSER/NOBYPASSRLS/NOREPLICATION: el rol `postgres` de
-- Supabase no es superusuario y PostgreSQL le prohibe nombrarlas (ni siquiera en
-- negativo). Los valores por defecto de CREATE ROLE ya son NO*; se comprueba aqui.
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname IN ('senas_app','senas_migrator')
             AND (rolsuper OR rolcreaterole OR rolcreatedb OR rolbypassrls OR rolreplication)) THEN
    RAISE EXCEPTION 'senas_app/senas_migrator tienen atributos privilegiados; corregir manualmente.';
  END IF;
  -- Ninguno de los dos roles debe ser miembro de otro rol (no heredan nada). Las filas
  -- donde otro rol es miembro DE ellos (p. ej. postgres como administrador en PG16+) son normales.
  IF EXISTS (SELECT 1 FROM pg_auth_members WHERE member IN ('senas_app'::regrole, 'senas_migrator'::regrole)) THEN
    RAISE EXCEPTION 'senas_app/senas_migrator son miembros de otros roles; revisar membresias.';
  END IF;
END
$$;

-- ---------------------------------------------------------------------------
-- Privilegios del rol de migraciones
-- ---------------------------------------------------------------------------
-- NO se concede CREATE sobre la base de datos: la migracion inicial ya no contiene
-- `CREATE SCHEMA IF NOT EXISTS "public"` (que exigia ese permiso). El migrador solo
-- necesita USAGE y CREATE sobre el schema `public`, que ya existe.

-- Crear tablas, indices, tipos y la tabla _prisma_migrations en `public`.
GRANT USAGE, CREATE ON SCHEMA public TO senas_migrator;

-- ---------------------------------------------------------------------------
-- Privilegios del rol de aplicacion (solo a nivel de schema; los de tabla se
-- conceden en 002_permissions.sql cuando las tablas ya existen)
-- ---------------------------------------------------------------------------
GRANT USAGE ON SCHEMA public TO senas_app;
-- Sin CREATE en el schema: senas_app no puede crear tablas ni tipos.
REVOKE CREATE ON SCHEMA public FROM senas_app;
-- Los tipos enum de la migracion tienen USAGE para PUBLIC por defecto, asi que
-- senas_app puede usarlos sin GRANT adicional.

COMMIT;
