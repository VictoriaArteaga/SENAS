-- ============================================================================
-- 003_rls.sql  -  PROPUESTA PENDIENTE DE REVISION. NO SE EJECUTA AUTOMATICAMENTE.
-- ============================================================================
-- Cuando:   DESPUES de 002_permissions.sql.
-- Quien:    el rol `senas_migrator` (propietario; solo el propietario puede
--           ALTER TABLE ... ENABLE ROW LEVEL SECURITY).
-- Que hace: habilita RLS en las 8 tablas (+ _prisma_migrations) y crea politicas
--           permisivas SOLO para senas_app, una por operacion concedida.
--
-- Efecto:
--   anon / authenticated    : RLS activado y sin politicas => 0 filas, ademas de no
--                             tener GRANT (doble barrera).
--   service_role / postgres : tienen BYPASSRLS en Supabase; RLS no les aplica
--                             (service_role ya no tiene GRANT tras 002).
--   senas_app               : sujeto a RLS (no es propietario ni tiene BYPASSRLS);
--                             solo hace lo que permiten estas politicas.
--   senas_migrator          : propietario => RLS no le aplica (no se usa FORCE, ver abajo).
--
-- PRIMERA VERSION: las politicas son USING (true) / WITH CHECK (true): el backend
-- accede a todas las filas porque la autorizacion por jugador la hace NestJS.
-- RLS cumple aqui una funcion de CONTENCION (solo senas_app, y solo esos verbos),
-- no de aislamiento por jugador.
--
-- FORCE ROW LEVEL SECURITY: NO se usa. FORCE sujetaria tambien al propietario
-- (senas_migrator) y habria que darle politicas para cargar seeds. senas_migrator
-- es de confianza y no se expone a la aplicacion; para endurecer mas, añadir FORCE
-- y una politica `TO senas_migrator USING (true) WITH CHECK (true)` por tabla.
--
-- FUTURO (aislamiento por jugador): con autenticacion implementada, NestJS podria
-- fijar la identidad por transaccion (SELECT set_config('app.user_id', $1, true))
-- y las politicas pasarian a USING (usuario_id = current_setting('app.user_id')::uuid).
-- No se implementa ahora porque ese mecanismo de identidad aun no existe.
--
-- Si se usa el SQL Editor de Supabase, eliminar las lineas que comienzan con "\".
-- Idempotente: DROP POLICY IF EXISTS + CREATE POLICY; ENABLE RLS es repetible.
-- ============================================================================

\set ON_ERROR_STOP on

BEGIN;

DO $$
BEGIN
  IF current_user <> 'senas_migrator' THEN
    RAISE EXCEPTION 'Ejecutar como senas_migrator (propietario de las tablas). Rol actual: %', current_user;
  END IF;
END
$$;

-- 1. Habilitar RLS ----------------------------------------------------------
ALTER TABLE public.usuarios           ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.perfiles           ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.niveles            ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.senas              ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.nivel_senas        ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.progreso_senas     ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.evaluaciones       ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.sesiones           ENABLE ROW LEVEL SECURITY;
-- Sin politicas: solo el propietario y los roles BYPASSRLS pueden tocarla.
ALTER TABLE public._prisma_migrations ENABLE ROW LEVEL SECURITY;

-- 2. Politicas de senas_app (una por verbo concedido en 002) -----------------
-- usuarios: SELECT, INSERT, UPDATE
DROP POLICY IF EXISTS usuarios_app_select ON public.usuarios;
DROP POLICY IF EXISTS usuarios_app_insert ON public.usuarios;
DROP POLICY IF EXISTS usuarios_app_update ON public.usuarios;
CREATE POLICY usuarios_app_select ON public.usuarios FOR SELECT TO senas_app USING (true);
CREATE POLICY usuarios_app_insert ON public.usuarios FOR INSERT TO senas_app WITH CHECK (true);
CREATE POLICY usuarios_app_update ON public.usuarios FOR UPDATE TO senas_app USING (true) WITH CHECK (true);

-- perfiles: SELECT, INSERT, UPDATE
DROP POLICY IF EXISTS perfiles_app_select ON public.perfiles;
DROP POLICY IF EXISTS perfiles_app_insert ON public.perfiles;
DROP POLICY IF EXISTS perfiles_app_update ON public.perfiles;
CREATE POLICY perfiles_app_select ON public.perfiles FOR SELECT TO senas_app USING (true);
CREATE POLICY perfiles_app_insert ON public.perfiles FOR INSERT TO senas_app WITH CHECK (true);
CREATE POLICY perfiles_app_update ON public.perfiles FOR UPDATE TO senas_app USING (true) WITH CHECK (true);

-- niveles, senas, nivel_senas: solo SELECT
DROP POLICY IF EXISTS niveles_app_select ON public.niveles;
CREATE POLICY niveles_app_select ON public.niveles FOR SELECT TO senas_app USING (true);

DROP POLICY IF EXISTS senas_app_select ON public.senas;
CREATE POLICY senas_app_select ON public.senas FOR SELECT TO senas_app USING (true);

DROP POLICY IF EXISTS nivel_senas_app_select ON public.nivel_senas;
CREATE POLICY nivel_senas_app_select ON public.nivel_senas FOR SELECT TO senas_app USING (true);

-- progreso_senas: SELECT, INSERT, UPDATE
DROP POLICY IF EXISTS progreso_senas_app_select ON public.progreso_senas;
DROP POLICY IF EXISTS progreso_senas_app_insert ON public.progreso_senas;
DROP POLICY IF EXISTS progreso_senas_app_update ON public.progreso_senas;
CREATE POLICY progreso_senas_app_select ON public.progreso_senas FOR SELECT TO senas_app USING (true);
CREATE POLICY progreso_senas_app_insert ON public.progreso_senas FOR INSERT TO senas_app WITH CHECK (true);
CREATE POLICY progreso_senas_app_update ON public.progreso_senas FOR UPDATE TO senas_app USING (true) WITH CHECK (true);

-- evaluaciones: SELECT, INSERT (historial inmutable)
DROP POLICY IF EXISTS evaluaciones_app_select ON public.evaluaciones;
DROP POLICY IF EXISTS evaluaciones_app_insert ON public.evaluaciones;
CREATE POLICY evaluaciones_app_select ON public.evaluaciones FOR SELECT TO senas_app USING (true);
CREATE POLICY evaluaciones_app_insert ON public.evaluaciones FOR INSERT TO senas_app WITH CHECK (true);


-- sesiones: SELECT, INSERT, UPDATE (sin DELETE). La autorizacion por sesion/usuario la hace
-- NestJS (USING (true) es contencion, no aislamiento por jugador).
DROP POLICY IF EXISTS sesiones_app_select ON public.sesiones;
DROP POLICY IF EXISTS sesiones_app_insert ON public.sesiones;
DROP POLICY IF EXISTS sesiones_app_update ON public.sesiones;
CREATE POLICY sesiones_app_select ON public.sesiones FOR SELECT TO senas_app USING (true);
CREATE POLICY sesiones_app_insert ON public.sesiones FOR INSERT TO senas_app WITH CHECK (true);
CREATE POLICY sesiones_app_update ON public.sesiones FOR UPDATE TO senas_app USING (true) WITH CHECK (true);

COMMIT;
