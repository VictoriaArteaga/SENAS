-- CreateEnum
CREATE TYPE "tipo_sena" AS ENUM ('SENA', 'FRASE');

-- CreateEnum
CREATE TYPE "dificultad" AS ENUM ('FACIL', 'MEDIA', 'DIFICIL');

-- CreateEnum
CREATE TYPE "resultado_evaluacion" AS ENUM ('APROBADO', 'REINTENTAR');

-- CreateTable
CREATE TABLE "usuarios" (
    "id" UUID NOT NULL,
    "username" TEXT NOT NULL,
    "email" TEXT NOT NULL,
    "password_hash" TEXT NOT NULL,
    "created_at" TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(6) NOT NULL,
    "is_active" BOOLEAN NOT NULL DEFAULT true,

    CONSTRAINT "usuarios_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "perfiles" (
    "id" UUID NOT NULL,
    "usuario_id" UUID NOT NULL,
    "display_name" TEXT NOT NULL,
    "avatar_config" JSONB,
    "total_xp" INTEGER NOT NULL DEFAULT 0,
    "nivel_actual" INTEGER NOT NULL DEFAULT 1,
    "precision_promedio" DECIMAL(5,2) NOT NULL DEFAULT 0,
    "created_at" TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(6) NOT NULL,

    CONSTRAINT "perfiles_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "niveles" (
    "id" UUID NOT NULL,
    "numero" INTEGER NOT NULL,
    "nombre" TEXT NOT NULL,
    "descripcion" TEXT,
    "orden" INTEGER NOT NULL,
    "xp_requerida" INTEGER NOT NULL DEFAULT 0,
    "precision_minima" DECIMAL(5,2) NOT NULL DEFAULT 0,
    "created_at" TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(6) NOT NULL,

    CONSTRAINT "niveles_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "senas" (
    "id" UUID NOT NULL,
    "nombre" TEXT NOT NULL,
    "descripcion" TEXT,
    "tipo" "tipo_sena" NOT NULL,
    "dificultad" "dificultad" NOT NULL,
    "animacion" TEXT,
    "patron_referencia" JSONB,
    "created_at" TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(6) NOT NULL,

    CONSTRAINT "senas_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "nivel_senas" (
    "id" UUID NOT NULL,
    "nivel_id" UUID NOT NULL,
    "sena_id" UUID NOT NULL,
    "orden" INTEGER NOT NULL,
    "es_obligatoria" BOOLEAN NOT NULL DEFAULT true,

    CONSTRAINT "nivel_senas_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "progreso_senas" (
    "id" UUID NOT NULL,
    "usuario_id" UUID NOT NULL,
    "sena_id" UUID NOT NULL,
    "veces_practicada" INTEGER NOT NULL DEFAULT 0,
    "mejor_precision" DECIMAL(5,2) NOT NULL DEFAULT 0,
    "precision_promedio" DECIMAL(5,2) NOT NULL DEFAULT 0,
    "completada" BOOLEAN NOT NULL DEFAULT false,
    "fecha_primera_practica" TIMESTAMPTZ(6),
    "fecha_ultima_practica" TIMESTAMPTZ(6),

    CONSTRAINT "progreso_senas_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "evaluaciones" (
    "id" UUID NOT NULL,
    "usuario_id" UUID NOT NULL,
    "sena_id" UUID NOT NULL,
    "nivel_id" UUID NOT NULL,
    "precision" DECIMAL(5,2) NOT NULL,
    "estrellas" INTEGER NOT NULL,
    "xp_obtenida" INTEGER NOT NULL DEFAULT 0,
    "resultado" "resultado_evaluacion" NOT NULL,
    "feedback" TEXT,
    "datos_reconocimiento" JSONB,
    "created_at" TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "evaluaciones_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "sesiones" (
    "id" UUID NOT NULL,
    "usuario_id" UUID NOT NULL,
    "token_hash" TEXT NOT NULL,
    "fecha_creacion" TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "fecha_expiracion" TIMESTAMPTZ(6) NOT NULL,
    "fecha_revocacion" TIMESTAMPTZ(6),
    "ultimo_uso" TIMESTAMPTZ(6),
    "informacion_dispositivo" JSONB,

    CONSTRAINT "sesiones_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "usuarios_username_key" ON "usuarios"("username");

-- CreateIndex
CREATE UNIQUE INDEX "usuarios_email_key" ON "usuarios"("email");

-- CreateIndex
CREATE UNIQUE INDEX "perfiles_usuario_id_key" ON "perfiles"("usuario_id");

-- CreateIndex
CREATE UNIQUE INDEX "niveles_numero_key" ON "niveles"("numero");

-- CreateIndex
CREATE UNIQUE INDEX "niveles_orden_key" ON "niveles"("orden");

-- CreateIndex
CREATE INDEX "nivel_senas_sena_id_idx" ON "nivel_senas"("sena_id");

-- CreateIndex
CREATE UNIQUE INDEX "nivel_senas_nivel_id_sena_id_key" ON "nivel_senas"("nivel_id", "sena_id");

-- CreateIndex
CREATE UNIQUE INDEX "nivel_senas_nivel_id_orden_key" ON "nivel_senas"("nivel_id", "orden");

-- CreateIndex
CREATE INDEX "progreso_senas_sena_id_idx" ON "progreso_senas"("sena_id");

-- CreateIndex
CREATE UNIQUE INDEX "progreso_senas_usuario_id_sena_id_key" ON "progreso_senas"("usuario_id", "sena_id");

-- CreateIndex
CREATE INDEX "evaluaciones_usuario_id_created_at_idx" ON "evaluaciones"("usuario_id", "created_at");

-- CreateIndex
CREATE INDEX "evaluaciones_usuario_id_sena_id_idx" ON "evaluaciones"("usuario_id", "sena_id");

-- CreateIndex
CREATE INDEX "evaluaciones_sena_id_idx" ON "evaluaciones"("sena_id");

-- CreateIndex
CREATE INDEX "evaluaciones_nivel_id_idx" ON "evaluaciones"("nivel_id");

-- CreateIndex
CREATE UNIQUE INDEX "sesiones_token_hash_key" ON "sesiones"("token_hash");

-- CreateIndex
CREATE INDEX "sesiones_usuario_id_idx" ON "sesiones"("usuario_id");

-- CreateIndex
CREATE INDEX "sesiones_fecha_expiracion_idx" ON "sesiones"("fecha_expiracion");

-- CreateIndex
CREATE INDEX "sesiones_fecha_revocacion_idx" ON "sesiones"("fecha_revocacion");

-- AddForeignKey
ALTER TABLE "perfiles" ADD CONSTRAINT "perfiles_usuario_id_fkey" FOREIGN KEY ("usuario_id") REFERENCES "usuarios"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "nivel_senas" ADD CONSTRAINT "nivel_senas_nivel_id_fkey" FOREIGN KEY ("nivel_id") REFERENCES "niveles"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "nivel_senas" ADD CONSTRAINT "nivel_senas_sena_id_fkey" FOREIGN KEY ("sena_id") REFERENCES "senas"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "progreso_senas" ADD CONSTRAINT "progreso_senas_usuario_id_fkey" FOREIGN KEY ("usuario_id") REFERENCES "usuarios"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "progreso_senas" ADD CONSTRAINT "progreso_senas_sena_id_fkey" FOREIGN KEY ("sena_id") REFERENCES "senas"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "evaluaciones" ADD CONSTRAINT "evaluaciones_usuario_id_fkey" FOREIGN KEY ("usuario_id") REFERENCES "usuarios"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "evaluaciones" ADD CONSTRAINT "evaluaciones_sena_id_fkey" FOREIGN KEY ("sena_id") REFERENCES "senas"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "evaluaciones" ADD CONSTRAINT "evaluaciones_nivel_id_fkey" FOREIGN KEY ("nivel_id") REFERENCES "niveles"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "sesiones" ADD CONSTRAINT "sesiones_usuario_id_fkey" FOREIGN KEY ("usuario_id") REFERENCES "usuarios"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- ============================================================================
-- CHECK constraints (manuales: Prisma no las expresa en schema.prisma)
-- ============================================================================

-- AddCheck perfiles
ALTER TABLE "perfiles"
    ADD CONSTRAINT "perfiles_total_xp_check" CHECK ("total_xp" >= 0),
    ADD CONSTRAINT "perfiles_nivel_actual_check" CHECK ("nivel_actual" > 0),
    ADD CONSTRAINT "perfiles_precision_promedio_check" CHECK ("precision_promedio" BETWEEN 0 AND 100);

-- AddCheck niveles
ALTER TABLE "niveles"
    ADD CONSTRAINT "niveles_numero_check" CHECK ("numero" > 0),
    ADD CONSTRAINT "niveles_orden_check" CHECK ("orden" > 0),
    ADD CONSTRAINT "niveles_xp_requerida_check" CHECK ("xp_requerida" >= 0),
    ADD CONSTRAINT "niveles_precision_minima_check" CHECK ("precision_minima" BETWEEN 0 AND 100);

-- AddCheck nivel_senas
ALTER TABLE "nivel_senas"
    ADD CONSTRAINT "nivel_senas_orden_check" CHECK ("orden" > 0);

-- AddCheck progreso_senas
ALTER TABLE "progreso_senas"
    ADD CONSTRAINT "progreso_senas_veces_practicada_check" CHECK ("veces_practicada" >= 0),
    ADD CONSTRAINT "progreso_senas_mejor_precision_check" CHECK ("mejor_precision" BETWEEN 0 AND 100),
    ADD CONSTRAINT "progreso_senas_precision_promedio_check" CHECK ("precision_promedio" BETWEEN 0 AND 100);

-- AddCheck evaluaciones
ALTER TABLE "evaluaciones"
    ADD CONSTRAINT "evaluaciones_precision_check" CHECK ("precision" BETWEEN 0 AND 100),
    ADD CONSTRAINT "evaluaciones_estrellas_check" CHECK ("estrellas" BETWEEN 0 AND 3),
    ADD CONSTRAINT "evaluaciones_xp_obtenida_check" CHECK ("xp_obtenida" >= 0);

-- AddCheck sesiones
ALTER TABLE "sesiones"
    ADD CONSTRAINT "sesiones_fecha_expiracion_check" CHECK ("fecha_expiracion" > "fecha_creacion");
