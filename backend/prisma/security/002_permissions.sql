-- ============================================================================
-- 002_permissions.sql  -  PROPUESTA PENDIENTE DE REVISION. NO SE EJECUTA AUTOMATICAMENTE.
-- ============================================================================
-- Cuando:   DESPUES de aplicar la migracion inicial (las tablas deben existir).
-- Quien:    el rol `senas_migrator` (PROPIETARIO de las tablas). Un rol que no
--           sea propietario no puede hacer GRANT/ALTER TABLE sobre ellas.
-- Que hace: concede privilegios minimos a senas_app y retira todo acceso a los
--           roles publicos de Supabase (anon, authenticated, service_role) y a PUBLIC.
--
-- Hay que re-ejecutarlo (ampliado) cada vez que una migracion futura añada
-- tablas: las tablas nuevas NO reciben privilegios automaticamente, a proposito
-- (fallar cerrado es mas seguro que fallar abierto).
--
-- Si se usa el SQL Editor de Supabase, eliminar las lineas que comienzan con "\".
-- Idempotente: GRANT/REVOKE repetidos no tienen efecto adicional.
-- ============================================================================

\set ON_ERROR_STOP on

BEGIN;

DO $$
BEGIN
  IF current_user <> 'senas_migrator' THEN
    RAISE EXCEPTION 'Ejecutar como senas_migrator (propietario de las tablas). Rol actual: %', current_user;
  END IF;
  IF to_regclass('public.usuarios') IS NULL OR to_regclass('public.evaluaciones') IS NULL
     OR to_regclass('public.sesiones') IS NULL
     OR to_regclass('public._prisma_migrations') IS NULL THEN
    RAISE EXCEPTION 'Faltan tablas: aplicar primero la migracion inicial con prisma migrate deploy.';
  END IF;
END
$$;

-- ---------------------------------------------------------------------------
-- 1. Retirar acceso a roles publicos (defensa en profundidad)
-- ---------------------------------------------------------------------------
-- Las tablas creadas por senas_migrator NO heredan los privilegios por defecto
-- que Supabase configura para tablas creadas por `postgres`, pero esos valores
-- varian entre proyectos; se revoca explicitamente para no depender de ellos.
-- `_prisma_migrations` existe tras `migrate deploy`; se protege igual.
REVOKE ALL ON TABLE
  public.usuarios, public.perfiles, public.niveles, public.senas,
  public.nivel_senas, public.progreso_senas, public.evaluaciones, public.sesiones,
  public._prisma_migrations
FROM PUBLIC, anon, authenticated, service_role;

-- ---------------------------------------------------------------------------
-- 2. Privilegios minimos de senas_app (CRUD que Prisma necesita, tabla por tabla)
-- ---------------------------------------------------------------------------
-- Prisma necesita SELECT en cualquier tabla que consulte, y tambien para
-- UPDATE ... WHERE y para INSERT ... RETURNING.
--
-- Nunca se concede: TRUNCATE, REFERENCES, TRIGGER, ni nada sobre _prisma_migrations.
-- Las FK en cascada (ON DELETE CASCADE) las ejecuta PostgreSQL con los privilegios
-- del propietario, por lo que no requieren DELETE en la tabla hija.

-- Cuentas: se crean y actualizan; la baja es logica (is_active). Sin DELETE.
GRANT SELECT, INSERT, UPDATE ON TABLE public.usuarios TO senas_app;
GRANT SELECT, INSERT, UPDATE ON TABLE public.perfiles TO senas_app;

-- Contenido educativo (niveles, señas, relacion nivel-seña): el backend solo lo
-- lee. Se carga/edita con senas_migrator (seeds o migraciones de datos).
GRANT SELECT ON TABLE public.niveles     TO senas_app;
GRANT SELECT ON TABLE public.senas       TO senas_app;
GRANT SELECT ON TABLE public.nivel_senas TO senas_app;

-- Agregado por usuario/seña: se crea y se actualiza tras cada evaluacion.
GRANT SELECT, INSERT, UPDATE ON TABLE public.progreso_senas TO senas_app;

-- Historial de intentos: INMUTABLE desde la aplicacion (solo alta y consulta).
-- Sin UPDATE ni DELETE: ni un bug ni una inyeccion pueden reescribir el historial.
GRANT SELECT, INSERT ON TABLE public.evaluaciones TO senas_app;

-- Sesiones (refresh tokens hasheados): se crean al iniciar sesion, y se actualizan al
-- rotar el token, registrar el ultimo uso o revocar (fecha_revocacion). Sin DELETE:
-- las sesiones vencidas/revocadas se conservan como rastro de auditoria; su limpieza,
-- si se necesita, la hace senas_migrator (o un rol de mantenimiento) y no la aplicacion.
-- Nunca se concede acceso a anon/authenticated/service_role (REVOKE de arriba).
GRANT SELECT, INSERT, UPDATE ON TABLE public.sesiones TO senas_app;

-- Las tablas usan UUID generados por Prisma: no hay secuencias que conceder.

COMMIT;
