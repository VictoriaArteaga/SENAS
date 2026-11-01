-- ============================================================================
-- 004_verify.sql  -  SOLO LECTURA. Termina con ERROR si alguna condicion no se cumple.
-- ============================================================================
-- Cuando: despues de 001-003 (y de la migracion inicial). Quien: un administrador
-- (cualquier rol que lea el catalogo). No modifica datos ni estructura.
-- Termina con la nota 'VERIFICACION OK' solo si todas las aserciones pasan.
-- Si se usa el SQL Editor de Supabase, eliminar las lineas que comienzan con "\".
-- ============================================================================
\set ON_ERROR_STOP on

-- Detalle informativo, limitado a las tablas del proyecto
SELECT c.relname AS tabla, pg_get_userbyid(c.relowner) AS owner,
       c.relrowsecurity AS rls, c.relforcerowsecurity AS rls_forced
FROM pg_class c
WHERE c.relnamespace = 'public'::regnamespace AND c.relkind = 'r'
  AND c.relname IN ('usuarios','perfiles','niveles','senas','nivel_senas','progreso_senas','evaluaciones','sesiones','_prisma_migrations')
ORDER BY 1;

-- aclexplode lee relacl directamente: no depende de la pertenencia a roles (a diferencia de
-- information_schema.role_table_grants, que solo muestra grants de roles que el usuario "ve").
SELECT c.relname AS tabla,
       CASE a.grantee WHEN 0 THEN 'PUBLIC' ELSE a.grantee::regrole::text END AS grantee,
       string_agg(a.privilege_type, ', ' ORDER BY a.privilege_type) AS privilegios
FROM pg_class c CROSS JOIN LATERAL aclexplode(c.relacl) a
WHERE c.relnamespace = 'public'::regnamespace
  AND c.relname IN ('usuarios','perfiles','niveles','senas','nivel_senas','progreso_senas','evaluaciones','sesiones','_prisma_migrations')
GROUP BY 1, 2 ORDER BY 1, 2;

DO $$
DECLARE
  tablas text[] := ARRAY['usuarios','perfiles','niveles','senas','nivel_senas','progreso_senas','evaluaciones','sesiones','_prisma_migrations'];
  n int;
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='senas_app') OR NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='senas_migrator') THEN
    RAISE EXCEPTION 'Faltan los roles senas_app / senas_migrator';
  END IF;
  SELECT count(*) INTO n FROM pg_roles WHERE rolname IN ('senas_app','senas_migrator')
    AND (rolsuper OR rolcreaterole OR rolcreatedb OR rolbypassrls OR rolreplication);
  IF n > 0 THEN RAISE EXCEPTION 'Roles con atributos privilegiados'; END IF;

  SELECT count(*) INTO n FROM pg_class WHERE relnamespace='public'::regnamespace AND relkind='r' AND relname = ANY(tablas);
  IF n <> 9 THEN RAISE EXCEPTION 'Se esperaban 9 tablas (8 del proyecto + _prisma_migrations), hay %', n; END IF;

  SELECT count(*) INTO n FROM pg_class WHERE relnamespace='public'::regnamespace AND relname = ANY(tablas)
    AND (NOT relrowsecurity OR pg_get_userbyid(relowner) <> 'senas_migrator');
  IF n > 0 THEN RAISE EXCEPTION '% tablas sin RLS o con propietario distinto de senas_migrator', n; END IF;

  SELECT count(*) INTO n FROM pg_class c CROSS JOIN LATERAL aclexplode(c.relacl) a
   WHERE c.relnamespace='public'::regnamespace AND c.relname = ANY(tablas)
     AND (a.grantee = 0 OR a.grantee IN (SELECT oid FROM pg_roles WHERE rolname IN ('anon','authenticated','service_role')));
  IF n > 0 THEN RAISE EXCEPTION 'Hay privilegios para PUBLIC/anon/authenticated/service_role'; END IF;

  SELECT count(*) INTO n FROM unnest(tablas) t
   WHERE has_table_privilege('senas_app', 'public.'||t, 'DELETE') OR has_table_privilege('senas_app', 'public.'||t, 'TRUNCATE')
      OR has_table_privilege('senas_app', 'public.'||t, 'REFERENCES') OR has_table_privilege('senas_app', 'public.'||t, 'TRIGGER');
  IF n > 0 THEN RAISE EXCEPTION 'senas_app tiene DELETE/TRUNCATE/REFERENCES/TRIGGER'; END IF;

  -- sesiones: senas_app debe poder SELECT, INSERT y UPDATE (y nada de DELETE, ya cubierto arriba)
  IF NOT (has_table_privilege('senas_app','public.sesiones','SELECT')
      AND has_table_privilege('senas_app','public.sesiones','INSERT')
      AND has_table_privilege('senas_app','public.sesiones','UPDATE')) THEN
    RAISE EXCEPTION 'senas_app debe tener SELECT, INSERT y UPDATE sobre sesiones';
  END IF;
  IF has_table_privilege('senas_app','public.evaluaciones','UPDATE') THEN RAISE EXCEPTION 'senas_app puede UPDATE evaluaciones'; END IF;
  IF has_table_privilege('senas_app','public._prisma_migrations','SELECT') THEN RAISE EXCEPTION 'senas_app accede a _prisma_migrations'; END IF;
  IF has_database_privilege('senas_migrator', current_database(), 'CREATE') THEN RAISE EXCEPTION 'senas_migrator tiene CREATE sobre la base de datos (no necesario)'; END IF;
  IF has_database_privilege('senas_app', current_database(), 'CREATE') THEN RAISE EXCEPTION 'senas_app tiene CREATE sobre la base de datos'; END IF;
  IF EXISTS (SELECT 1 FROM pg_auth_members WHERE member IN ('senas_app'::regrole, 'senas_migrator'::regrole)) THEN RAISE EXCEPTION 'senas_app/senas_migrator son miembros de otros roles'; END IF;
  IF has_schema_privilege('senas_app','public','CREATE') THEN RAISE EXCEPTION 'senas_app puede crear objetos en public'; END IF;

  SELECT count(*) INTO n FROM pg_policies WHERE schemaname='public' AND tablename = ANY(tablas);
  IF n <> 17 THEN RAISE EXCEPTION 'Se esperaban 17 politicas, hay %', n; END IF;
  SELECT count(*) INTO n FROM pg_policies WHERE schemaname='public' AND tablename = ANY(tablas) AND roles <> ARRAY['senas_app']::name[];
  IF n > 0 THEN RAISE EXCEPTION 'Hay politicas que no son exclusivas de senas_app'; END IF;

  RAISE NOTICE 'VERIFICACION OK';
END
$$;
