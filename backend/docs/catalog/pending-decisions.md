# Decisiones pendientes del catálogo educativo (Fase 7.3.1)

Cada decisión bloquea o condiciona la carga del catálogo. **Ninguna se ha resuelto por inferencia:** los CSV marcan estos
datos como `PENDIENTE`. Prioridad: **B** = bloquea la carga (el esquema lo exige o cambia la estructura), **A** = afecta al
contenido pedagógico, **M** = afecta a la calidad o a la trazabilidad.

## 0. Decisiones registradas (provisionales)

| Ref. | Decisión | Estado |
|---|---|---|
| R-01 | Se trabaja **provisionalmente con cuatro niveles**. | Provisional. La discrepancia documental (diagrama de 3 niveles frente a secciones detalladas y README de 4) **sigue abierta, pendiente de conciliación** (D-01). |
| R-02 | Las señas y frases adicionales de los niveles con contenido incompleto (Nivel 3 y Nivel 4, y los listados pendientes de los demás) **serán seleccionadas por los dos integrantes del equipo**. | Registrado. No se ha elegido ningún contenido. |
| R-03 | Los contenidos ya inventariados siguen siendo **provisionales; ninguno está aprobado**. | Vigente. |
| R-04 | Se propone usar **UUID estables** como identificadores. | Propuesta técnica, pendiente de validación en el diseño del proceso de carga (D-35). |

XP, precisión mínima, obligatoriedad y dificultad siguen **pendientes**. «Biblioteca» sigue sin nivel asignado.

Valores de ejemplo propuestos solo cuando la fuente los menciona; la propuesta es una **opción**, no un dato aprobado.

## 1. Estructura de niveles

| ID | Pri. | Decisión | Evidencia / conflicto | Opciones |
|---|---|---|---|---|
| D-01 | A | Número de niveles. **Decidido provisionalmente: 4 (R-01).** Queda pendiente conciliar la discrepancia documental. | Diagrama del documento de diseño: 3 niveles (Básico, Cotidiano, Académico). Secciones detalladas, tabla de cantidades y README: 4 niveles. | Conciliar los documentos de diseño con los 4 niveles, o revisar la decisión; la distribución definitiva de contenidos no está aprobada |
| D-02 | A | Contenido del Nivel 4 | Solo temáticas (universidad, trabajo, tecnología, salud, presentaciones, comunicación, situaciones profesionales) y un escenario; ninguna seña ni frase concreta. | Las señas/frases las seleccionarán los dos integrantes del equipo (R-02); mientras tanto el nivel queda sin contenido |
| D-03 | B | `xpRequerida` de cada nivel | No documentada. Solo existen ejemplos: «+100 XP» por seña y «1.250 / 1.500» en la maqueta del perfil. | Fijar un valor por nivel (Nivel 1 normalmente 0, a confirmar) |
| D-04 | B | `precisionMinima` de cada nivel | No documentada. Los umbrales de estrellas (60, 75, 90 %) describen la puntuación de un intento, no un requisito de nivel. 60 % es el límite de «Reintentar», pero no se declara como mínimo de nivel. | Fijar un valor por nivel; confirmar si coincide con algún umbral de estrellas |
| D-05 | M | `orden` frente a `numero` | Se asumió `orden = numero` (inferido). | Confirmar o indicar otro orden de recorrido |
| D-06 | A | Significado de `Perfil.nivelActual` | La maqueta del perfil muestra «Nivel: 8» (nivel de jugador por XP), mientras que los niveles del mundo son 1–4. El backend (Fase 7.2) lo interpreta como `Nivel.numero`. | Confirmar que es el nivel de mundo; si el «Nivel: 8» es otro concepto, queda fuera del catálogo |
| D-07 | M | Dónde guardar `objetivoEducativo`, audiencia (10–14, 15–18 años) y categorías temáticas | El esquema no tiene columnas para ello; solo `Nivel.descripcion`. | Resumir en `descripcion`, o abrir una fase aparte para ampliar el esquema |
| D-08 | M | Nombre y descripción oficiales de cada nivel | Los nombres vienen de los títulos del documento; las descripciones del CSV son resumen propio del texto fuente. | Aprobar o reescribir |

## 2. Alcance y contenido

| ID | Pri. | Decisión | Evidencia / conflicto | Opciones |
|---|---|---|---|---|
| D-09 | A | Cuota por nivel frente a contenido real | Documento y README: 10 señas por nivel, corpus de 30–40. Inventario: Nivel 1 con 33 elementos (27 letras + 6 palabras), Nivel 2 con 10, Nivel 3 con 5 frases, Nivel 4 con 0; 49 contenidos en total. | Decidir si las letras cuentan dentro de la cuota, o si el alfabeto es contenido aparte |
| D-10 | A | ¿Las 27 letras del alfabeto forman parte del catálogo como señas? | Existen en Unity (`LscAlphabet.asset`) y el controlador las llama «Nivel 1: abecedario LSC», pero el documento no enumera letras. Se incluyeron como `SENA` (el enum no tiene tipo «letra»). | Incluirlas / excluirlas / convertirlas en un subconjunto |
| D-11 | A | Números del Nivel 1 | Se mencionan como categoría; no se indica cuáles ni cuántos. | Enumerar |
| D-12 | A | Colores del Nivel 1 | Ídem. | Enumerar |
| D-13 | A | «Saludos» del Nivel 1 | Categoría sin lista. Solo Hola y Adiós aparecen como señas concretas. | Confirmar que Saludos = Hola + Adiós o ampliar |
| D-14 | M | «Sí / No» | Un solo elemento en el texto. Se inventariaron como dos señas (`SENA_SI`, `SENA_NO`). | Confirmar dos señas |
| D-15 | M | «Adiós» | Está en el documento de diseño, no en la tabla del README (que no lista «hola» ni «adiós»). | Confirmar que forma parte del Nivel 1 |
| D-16 | A | Nivel 2: ¿los 10 ejemplos son la lista cerrada? | El documento los llama «ejemplos». Las categorías animales, lugares, emociones y actividades no tienen ninguna seña concreta (solo Feliz/Triste cubren emociones). | Confirmar la lista o ampliarla |
| D-17 | A | Nivel 3: cuota 10 frente a 5 frases documentadas | Solo hay 5 frases; el resto de la cuota está sin definir. | Las demás frases las seleccionarán los dos integrantes del equipo (R-02), o se reduce la cuota |
| D-18 | A | Frase «Mi nombre es...» | Texto incompleto: no se sabe si es fija o si incluye deletrear un nombre. | Definir el texto y la composición |
| D-19 | A | Composición de las frases | No se indica de qué señas se compone cada frase ni si esas señas (cómo, estás, necesito, quiero, nombre, dónde) se enseñan antes como `SENA`. | Definir la composición y si las señas componentes entran al catálogo |
| D-20 | A | «Biblioteca» | Aparece como componente del ejemplo de evaluación de Nivel 3, sin nivel asignado. Inventariada sin relación: tal como está, una seña sin nivel recibiría `404` en `GET /senas/:id`. | Asignar nivel o descartar |
| D-21 | A | Escenarios del Nivel 3 y 4 («pedir ayuda para encontrar la biblioteca», «comunicar una presentación mañana») | Son situaciones, no frases con texto. El esquema no modela escenarios. | Decidir si se convierten en `FRASE` con texto definido o quedan fuera del catálogo |
| D-22 | M | Reutilización de contenido entre niveles | HOLA, AYUDA, COLEGIO y AGUA aparecen en más de un nivel o como componente de una frase. Se inventarió una sola `Sena` por concepto (la relación `NivelSena` permite varios niveles). | Confirmar reutilización frente a copias independientes |

## 3. Relaciones nivel–seña

| ID | Pri. | Decisión | Evidencia | Opciones |
|---|---|---|---|---|
| D-23 | B | `esObligatoria` por relación | No documentado. Con la regla 7.2, el siguiente nivel exige completar todas las obligatorias del anterior con precisión ≥ `precisionMinima`; si todas las 33 del Nivel 1 fueran obligatorias, el desbloqueo exigiría las 27 letras. El valor por defecto del esquema (`true`) no se aplicó. | Definir por seña |
| D-24 | A | Orden pedagógico dentro del nivel | El orden del CSV es el de aparición en las fuentes (alfabético para las letras); no hay criterio pedagógico aprobado. | Aprobar o reordenar |
| D-25 | A | Posición del alfabeto frente a las palabras en el Nivel 1 | El documento lista «Alfabeto» primero; es el único indicio. | Confirmar |

## 4. Atributos de cada seña

| ID | Pri. | Decisión | Evidencia | Opciones |
|---|---|---|---|---|
| D-26 | B | `dificultad` de cada seña (`FACIL`, `MEDIA`, `DIFICIL`) | No documentada para ninguna seña. Es obligatoria en el esquema (sin valor por defecto). | Definir por seña |
| D-27 | A | Criterio de dificultad | Sin criterio pedagógico escrito. La matriz de validación solo identifica qué letras tienen movimiento (G, H, J, Ñ, S, Z). | Definir el criterio antes de asignar |
| D-28 | A | `descripcion` de palabras y frases | Solo las 27 letras tienen texto (la instrucción de pose del asset). | Redactar y aprobar |
| D-29 | A | Texto de `descripcion` de las letras | Proviene de las instrucciones de pose del asset; están marcadas como aproximadas (13) o provisionales (14), y no han sido validadas contra LSC. | Aprobar como descripción o rehacer |
| D-30 | M | `nombre` de las letras | Propuesto «Letra A»; podría ser «A». | Elegir |
| D-31 | A | Letras P y Q | La matriz las marca `REFERENCIA_INSUFICIENTE` (no hay referencia suficiente para validarlas). | Incluir, aplazar o completar la referencia |
| D-32 | A | `animacion` | El documento prevé animaciones pre-creadas en el Animator de Unity; `Assets/Animations` está vacío y las letras usan poses procedurales. No existe convención para el valor del campo (nombre de clip, ruta, id). | Definir convención y producir las animaciones |
| D-33 | A | `patronReferencia` | Formato «MediaPipe evolutivo» sin definir; ninguna seña tiene patrón. El documento solo esboza una plantilla para HOLA (posición inicial, orientación, forma, trayectoria, posición final, expresión y orientación del rostro). Los datos del asset son poses de la mano 3D del avatar, no patrones de reconocimiento. | Definir formato; dejar `null` hasta tenerlo |

## 5. Validación y trazabilidad

| ID | Pri. | Decisión | Evidencia | Opciones |
|---|---|---|---|---|
| D-34 | A | Validación lingüística LSC | Ninguna seña está validada (25 letras `PENDIENTE`, 2 `REFERENCIA_INSUFICIENTE`; palabras y frases sin revisión). No hay responsable de validación definido. | Designar validador y criterio de aprobación |
| D-35 | B | Convención de identificadores | `Sena.nombre` no es único y el esquema no tiene clave natural; los identificadores `LETRA_*`, `SENA_*`, `FRASE_*` son provisionales y no existen en la base de datos. Una carga repetible necesitará una clave estable. | **Propuesta técnica (R-04): UUID estables** asignados a cada contenido y nivel, con el mismo valor en cada ejecución de la carga. Pendiente de validación en el diseño del proceso de carga (cómo se generan, dónde se guardan y cómo se mantiene la idempotencia sin modificar el esquema). Los prefijos `LETRA_`/`SENA_`/`FRASE_` seguirían siendo etiquetas legibles del inventario, no claves |
| D-36 | M | Ubicación del documento de diseño | `VideoJuegoContexto.docx` está fuera del repositorio (carpeta «Diseño de interfaces»), no en su raíz. | Incorporarlo al repo o mantener la referencia externa |
| D-37 | M | Cifras de la maqueta del perfil | «Señas aprendidas: 32» y «Nivel 8» son ilustrativas; no concuerdan con 30–40 señas ni con 4 niveles. | Confirmar que son solo ejemplos |

## Resumen

- Total: **37 decisiones** — 5 bloquean la carga (**B**: D-03, D-04, D-23, D-26, D-35), 23 afectan al contenido (**A**, incluida D-01, decidida provisionalmente y pendiente de conciliación) y 9 a la calidad o la trazabilidad (**M**).
- No hay datos suficientes para marcar el catálogo como listo. Ver `catalog-audit.md`, sección «Conclusión de preparación».
