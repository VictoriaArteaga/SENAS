# Auditoría del catálogo educativo (Fase 7.3.1)

Fecha: 2026-10-31. Alcance: análisis y documentación. No se ha tocado ninguna base de datos, ni el esquema, ni código.

**Convención de certeza** usada en este documento y en los CSV:

- **DOCUMENTADO:** el dato aparece de forma explícita en una fuente.
- **INFERIDO:** se deduce de una fuente pero no está escrito; requiere aprobación.
- **PENDIENTE:** no existe en ninguna fuente. No se rellenó.

Estados de validación usados en los CSV: niveles `PENDIENTE_APROBACION`; señas y frases `LSC_PENDIENTE` (nadie ha validado la
seña contra LSC) o `LSC_REFERENCIA_INSUFICIENTE` (estado de la matriz de validación del proyecto para P y Q).

## Decisiones provisionales registradas

- Se trabaja **provisionalmente con cuatro niveles**. La discrepancia entre el diagrama de tres niveles del documento de diseño y las secciones detalladas y el README (cuatro) **se conserva como observación pendiente de conciliación**. La distribución definitiva de contenidos no está aprobada.
- Las señas y frases adicionales de los niveles pendientes **serán seleccionadas por los dos integrantes del equipo**. No se ha elegido ninguna.
- Todos los contenidos inventariados siguen siendo **provisionales**; ninguno está aprobado.
- Se propone usar **UUID estables** como identificadores. Es una propuesta técnica pendiente de validación en el diseño del proceso de carga.
- Siguen pendientes XP, precisión mínima, obligatoriedad y dificultad. «Biblioteca» sigue sin nivel.
- Las cantidades y relaciones del inventario no cambian: no hay evidencia nueva que lo justifique.

## 1. Fuentes revisadas

| Fuente | Qué aporta |
|---|---|
| `VideoJuegoContexto.docx` (27 330 bytes; **fuera del repositorio**, en la carpeta «Diseño de interfaces», no en la raíz) | Documento de diseño: niveles, contenido temático, ejemplos de señas y frases, cuotas, puntuación, XP, plantilla de patrón. Fuente principal. |
| `README.md` (raíz del repo) | Tabla de 4 niveles, cuota de 10 por nivel, corpus de 30–40, hoja de ruta con «Definición del corpus de 30–40 señas» sin marcar. |
| `Assets/Project/Data/LscAlphabet.asset` + `Scripts/Alphabet/LscAlphabet.cs`, `AlphabetLevelController.cs` | 27 letras con instrucción de pose para la mano 3D del avatar; el controlador se describe como «Nivel 1: abecedario LSC». |
| `Assets/Project/Reference/LSC/lsc-validation-matrix.md` (y documentos de esa carpeta) | Estado de validación LSC por letra (25 `PENDIENTE`, P y Q `REFERENCIA_INSUFICIENTE`); letras con movimiento. |
| `Assets/Animations/`, `Assets/IA/MediaPipe/README.md` | Sin clips de animación de señas; MediaPipe solo captura landmarks, sin patrones de referencia. |
| `backend/prisma/schema.prisma`, módulos `levels` y `senas`, `backend/README.md` | Estructura destino y reglas de desbloqueo vigentes. |
| `README.md` / `docs/` del frontend de autenticación y escena | Revisados; sin contenido educativo. |

Contraste hecho por script (solo lectura): las 27 instrucciones del asset coinciden literalmente con el texto de la matriz, y las
6 letras con movimiento del asset (G, H, J, Ñ, S, Z) coinciden con las de la matriz (0 discrepancias).

## 2. Resumen

| Concepto | Cantidad |
|---|---|
| Niveles inventariados | 4 provisionales (el diagrama del documento muestra 3: discrepancia pendiente de conciliación, ver D-01) |
| Contenidos (filas de `senas-inventory.csv`) | 49 |
| de tipo `SENA` | 44 (27 letras, 6 palabras de Nivel 1, 10 de Nivel 2, 1 «Biblioteca») |
| de tipo `FRASE` | 5 |
| Relaciones nivel–seña | 48 (Nivel 1: 33 · Nivel 2: 10 · Nivel 3: 5 · Nivel 4: 0) |
| Contenidos sin nivel | 1 (`SENA_BIBLIOTECA`) |
| Identificadores duplicados | 0 |
| Decisiones pendientes | 37 (5 bloqueantes) |

## 3. Niveles

| N.º | Nombre | Estado de los campos de `Nivel` |
|---|---|---|
| 1 | Primeras señas | `numero`, `nombre`: documentados. `orden`: inferido (= número). `descripcion`: resumen del texto. `xpRequerida`, `precisionMinima`: **pendientes**. |
| 2 | Mi mundo | ídem |
| 3 | Situaciones cotidianas | ídem |
| 4 | Reto universitario | ídem; sin señas ni frases enumeradas |

La columna `objetivoEducativo` del CSV no tiene campo equivalente en el esquema (ver sección 8). Solo el Nivel 1 declara un
objetivo textual («Introducir al jugador en la LSC»); los demás son **inferidos** de su temática. La progresión pedagógica
documentada es *señas individuales → palabras → frases → situaciones comunicativas*.

## 4. Señas y frases

**Nivel 1 (33).** Alfabeto de 27 letras (A–Z con Ñ entre N y O): su existencia es **documentada** en Unity, su pertenencia al
Nivel 1 es **inferida** del controlador y de la mención «Alfabeto» del documento. Palabras **documentadas**: Sí, No, Gracias,
Hola, Adiós, Por favor. «Números», «Colores» y «Saludos» se nombran como categorías sin ningún elemento: **pendientes**.

**Nivel 2 (10).** Mamá, Papá, Amigo, Colegio, Casa, Comida, Agua, Feliz, Triste, Ayuda: el documento los llama «ejemplos». Las
categorías animales, lugares y actividades no tienen ninguna seña concreta.

**Nivel 3 (5 frases).** «Hola, ¿cómo estás?», «Necesito ayuda.», «Quiero agua.», «Mi nombre es...», «¿Dónde está el colegio?».
Todas documentadas, una incompleta. «Biblioteca» aparece como componente de un ejemplo de evaluación, sin nivel.

**Nivel 4 (0).** Solo temáticas y un escenario («presentación mañana»), sin texto de frase.

**Atributos de cada seña.** `dificultad`, `animacion` y `patronReferencia` son **pendientes para las 49 filas**; la
`descripcion` solo existe para las 27 letras (instrucción de pose del asset, sin validar) y es pendiente para las otras 22.

## 5. Duplicados e inconsistencias

**Duplicados de identificador:** ninguno. Se reutiliza una sola `Sena` cuando el mismo concepto aparece en varios lugares
(HOLA, AYUDA, COLEGIO, AGUA; COMIDA también es categoría). Las frases del Nivel 3 son contenidos propios.

**Inconsistencias en las fuentes:**

1. Niveles: 3 en el diagrama del documento, 4 en el resto. Se trabaja provisionalmente con 4; la discrepancia sigue **pendiente de conciliación**.
2. Cuota de 10 por nivel y corpus de 30–40 frente a lo inventariado: Nivel 1 tiene 33 (por el alfabeto), Nivel 3 tiene 5 y Nivel 4 tiene 0; total 49, por encima del rango 30–40.
3. «Hola» y «Adiós» están en el documento pero no en la tabla del README; «Sí / No» aparece como un solo elemento.
4. El documento prevé animaciones pre-creadas en Animator, pero `Assets/Animations` está vacío; las letras se resuelven con poses procedurales.
5. La maqueta del perfil (`Nivel: 8`, `Señas aprendidas: 32`) no concuerda con 4 niveles ni con la cuota; se trata como ilustrativa.
6. El README marca como pendiente la «Definición del corpus de 30–40 señas»: el propio proyecto reconoce que no está cerrado.
7. El estado de las letras es doble: la etiqueta del asset (13 «aproximada», 14 «provisional») no equivale al estado LSC de la matriz.

## 6. Datos faltantes

- Por nivel: `xpRequerida`, `precisionMinima`, contenido del Nivel 4.
- Por seña: `dificultad` (obligatoria en el esquema), `animacion`, `patronReferencia`, `descripcion` de palabras y frases.
- Por relación: `esObligatoria` y orden pedagógico.
- Listados de «Números», «Colores», «Saludos», animales, lugares, actividades y composición de cada frase.
- Validador y criterio de validación lingüística LSC.

## 7. Decisiones que requieren aprobación

Ver `pending-decisions.md` (37 decisiones, con prioridad, evidencia y opciones). Las bloqueantes: XP (D-03), precisión mínima
(D-04), obligatoriedad (D-23), dificultad por seña (D-26) e identificadores (D-35, con los UUID estables como propuesta técnica
por validar). El número de niveles (D-01) está decidido provisionalmente en 4 y pendiente de conciliación documental. La
selección del contenido adicional de los niveles pendientes corresponde a los dos integrantes del equipo.

## 8. Compatibilidad con Prisma (solo observaciones; el esquema no se modificó)

| Aspecto | Observación |
|---|---|
| Enums | `Sena.tipo` (`SENA`/`FRASE`) cubre todo lo inventariado; las letras se marcan `SENA` porque no existe un tipo «letra». `Sena.dificultad` es `FACIL`/`MEDIA`/`DIFICIL`: ningún valor está definido, por eso el CSV usa `PENDIENTE`, que **no es un valor válido** y debe resolverse antes de cualquier carga. |
| `Sena.dificultad` obligatoria sin valor por defecto | Bloquea la carga mientras no se defina. |
| `xpRequerida` y `precisionMinima` con valor por defecto 0 | Una carga que omitiera esos datos los dejaría silenciosamente en 0: los niveles no exigirían XP y la regla de precisión de la Fase 7.2 se cumpliría siempre. El valor por defecto no debe usarse como relleno. |
| `esObligatoria` con valor por defecto `true` | Ídem: marcaría las 33 relaciones del Nivel 1 como obligatorias y condicionaría el desbloqueo del Nivel 2 (D-23). |
| `Sena.nombre` no único; sin clave natural | Los identificadores del inventario son provisionales y no persisten; una carga repetible necesitará una clave estable. Propuesta técnica: UUID estables, pendiente de validación en el diseño del proceso de carga (D-35). |
| `objetivoEducativo`, audiencia, categoría temática, estado de validación, fuente | Sin columna en el esquema; solo `Nivel.descripcion` y `Sena.descripcion` son texto libre (D-07). |
| `animacion` (String?) y `patronReferencia` (Json?) | Nulables: pueden cargarse vacíos. Su formato no está definido (D-32, D-33). |
| `NivelSena` | `(nivelId, senaId)` único y `orden` único por nivel: el CSV de relaciones los cumple (verificado por script). |
| `Nivel.numero`/`orden` | Únicos y > 0: 1–4 cumplen. |
| Permisos | `senas_app` solo lee estas tablas; cualquier carga futura requiere el rol propietario (`senas_migrator`), fuera de esta fase. |
| Seña sin nivel | `GET /senas/:id` devolvería 404 (regla 7.2.1): `SENA_BIBLIOTECA` hoy quedaría inaccesible. |

## 9. Conclusión de preparación

**El catálogo NO está listo para cargarse.** Hay un inventario trazable y sin duplicados, pero faltan datos obligatorios
(`dificultad` de las 49 filas, `xpRequerida` y `precisionMinima` de los 4 niveles, `esObligatoria`), el contenido del Nivel 4 y
la mitad del Nivel 3, y ninguna seña ha sido validada contra LSC. Hasta que se resuelvan las decisiones bloqueantes de
`pending-decisions.md`, no debe iniciarse la Fase 7.3.2.

## 10. Verificación realizada

- Cada fila de los CSV tiene una `fuente` no vacía (verificado por script).
- Enumeraciones: `tipo` ∈ {`SENA`, `FRASE`}; `dificultad` solo `PENDIENTE` (ver sección 8).
- Identificadores únicos: 49 de 49. Pares nivel–seña únicos y `orden` único por nivel: sí.
- Todas las relaciones apuntan a un nivel y a una seña existentes en el inventario.
- Los 3 CSV se escribieron en UTF-8 sin BOM, con encabezados exactos.
