# Auditoría de poses 3D del alfabeto LSC (Fase 6.1)

Rama: `feature/one-level` · Solo auditoría: no se modificó ninguna escena, script, asset de datos ni pose.

## Estado de las correcciones (actualizado en la Fase 6.3)

Se corrigieron las poses estáticas de **A, B, D, F, I, R, T, U, X y Z**; el detalle está en `pose-corrections.md`. Las tablas de más abajo conservan los hallazgos originales de la auditoría (Fase 6.1). Todas las poses siguen **pendientes de validación** por una persona usuaria de LSC.

## 1. Referencias encontradas

Carpeta: `Assets/Project/Reference/LSC/Alphabet/`

- **27 de 27 imágenes presentes**, una por letra, con nombre = letra: `A.png` … `Z.png` y `Ñ.png` (la Ñ incluida y correctamente nombrada).
- Formato: PNG RGBA, entre 161×202 y 177×221 px. Son **fotografías de una mano real** sobre fondo claro, con un rótulo azul y la letra al pie; algunas llevan **flechas naranjas** (G, H, J, Ñ, S, Z).
- Legibilidad: la forma general de la mano y qué dedos están extendidos se distingue en todas. La resolución es baja para detalles finos (contacto exacto entre pulgar y dedos, dedos ocultos).
- Las imágenes originales no se tocaron. Unity (modo batch, solo lectura) generó los `.meta` de importación de los PNG; aparecen como archivos sin seguimiento en git.
- La hoja antigua `Assets/Project/Reference/lsc_alphabet_reference.jpeg` (206×245 px) queda **superada** por estas fotos; las poses de la Fase 5 se basaron en ella y por eso varias eran incorrectas (ver tabla).

## 2. Referencias faltantes

Ninguna. No hay imágenes ilegibles. Sí hay **información que las imágenes estáticas no contienen** (sección 4 y 6).

## 3. Tabla comparativa (27 letras)

Cómo se hizo: render de cada pose actual de `LscAlphabet.asset` con el rig real (mano derecha, palma hacia la cámara, igual que en el juego) junto a la foto de referencia. Las clasificaciones son juicio visual, **no validación lingüística**.

Clases: `correct` · `needs_adjustment` · `insufficient_reference` · `missing_reference`. Prioridad: P1 alta, P2 media, P3 depende de información adicional.

| Letra | Referencia (lo observable) | Pose 3D actual | Diferencias | Clase | Prior. |
|---|---|---|---|---|---|
| A | Puño; pulgar pegado al costado del índice, apuntando arriba | Puño con pulgar abierto ~45° hacia fuera | Pulgar demasiado separado | needs_adjustment | P1 |
| B | Mano plana, 4 dedos extendidos **juntos**, pulgar plegado sobre la palma | 4 dedos extendidos pero **abiertos en abanico**; pulgar sobre la palma | Falta aducción (juntar dedos) | needs_adjustment | P1 |
| C | Arco en C: dedos curvados juntos, pulgar opuesto formando la abertura; mano de canto | Dedos semicerrados (parece garra) y pulgar apenas curvado | Falta arco abierto y oposición del pulgar | needs_adjustment | P2 |
| D | Índice arriba; medio, anular, meñique y pulgar forman un círculo (pulgar toca el medio) | Índice arriba, resto en puño; pulgar sin tocar | No se forma el círculo | needs_adjustment | P1 |
| E | Dedos curvados en garra con las puntas hacia la palma; pulgar debajo, visible | Casi un puño; pulgar oculto | Flexión y pulgar distintos | needs_adjustment | P2 |
| F | **Solo el índice extendido**; resto curvado; pulgar a lo largo del lado del índice | 3 dedos extendidos (medio, anular, meñique) + círculo índice-pulgar (tipo "OK") | **Configuración equivocada** | needs_adjustment | P1 |
| G | Mano de lado, índice curvado/extendido hacia la izquierda sobre el pulgar; **flecha curva** | Índice horizontal extendido, pulgar arriba, sin movimiento | Gancho del índice, pulgar, orientación; movimiento no implementado | needs_adjustment | P2 |
| H | Mano de lado, índice y medio **extendidos y juntos**, horizontales; **flecha curva** descendente | Índice y medio horizontales pero **separados**; sin movimiento | Juntar dedos; movimiento no implementado | needs_adjustment | P2 |
| I | **Índice extendido**, resto en puño, pulgar cruzado por delante | **Meñique** extendido | **Dedo equivocado** | needs_adjustment | P1 |
| J | Puño con dedo extendido inclinado a la izquierda (parece el meñique); **flecha curva** debajo | Meñique extendido vertical + recorrido inventado | Inclinación; trayectoria no documentada (ver sección 4) | needs_adjustment | P2 |
| K | Mano de lado: índice apunta arriba-izquierda y medio horizontal a la izquierda, bien separados | Índice y medio en V vertical, pulgar fuera | Orientación (≈90°) y separación mayor | needs_adjustment | P2 |
| L | Índice arriba, pulgar horizontal en L, resto curvado | Igual | Diferencia mínima | correct | — |
| M | Mano con el antebrazo horizontal; **3 dedos (índice, medio, anular) colgando hacia abajo** juntos | Puño con pulgar escondido, vista de frente | Dedos extendidos hacia abajo y orientación | needs_adjustment | P2 |
| N | Igual que M pero **2 dedos (índice, medio)** hacia abajo | Idéntica a M (puño) | Igual que M | needs_adjustment | P2 |
| Ñ | Como N + **flecha doble horizontal** bajo las puntas (oscilación lateral) | Puño + oscilación de roll ±10° | Dedos/orientación como N; amplitud inventada | needs_adjustment | P2 |
| O | Yemas de los dedos tocan la punta del pulgar formando un círculo cerrado | Dedos curvados y separados, sin cerrar el círculo | Falta contacto con el pulgar | needs_adjustment | P2 |
| P | Muñeca flexionada; dedos señalan hacia abajo (visibles 2) | Mano girada −60° con abanico de dedos y pulgar | Distinta; no se distingue qué dedos ni el pulgar | insufficient_reference | P3 |
| Q | Dedos y pulgar **juntos en cono/pellizco**, visto de canto | Mano girada con índice horizontal | Distinta; no se distingue qué dedos intervienen | insufficient_reference | P3 |
| R | Índice y medio **cruzados**, resto curvado | Índice y medio juntos sin cruzar | Falta el cruce | needs_adjustment | P1 |
| S | Índice extendido arriba, resto curvado, pulgar cruzado; **flecha en forma de S** | Puño sin dedo extendido; sin movimiento | Dedo extendido; movimiento no implementado | needs_adjustment | P2 |
| T | Tres dedos extendidos (dos altos juntos + uno corto a la izquierda), pulgar cruzado sobre el resto | Puño | Configuración equivocada (identidad exacta del dedo corto inferida por el lado del pulgar: confirmar) | needs_adjustment | P1 |
| U | Índice y **meñique** extendidos, medio y anular curvados, pulgar encima | Índice y medio extendidos juntos | Dedos equivocados | needs_adjustment | P1 |
| V | Índice y medio extendidos en V, pulgar sobre anular/meñique | Igual | Ángulo de separación similar | correct | — |
| W | Índice, medio y anular extendidos con separación leve, pulgar sobre el meñique | Igual (algo más abiertos) | Reducir separación (menor) | correct | — |
| X | Índice en gancho, resto en puño, pulgar cruzado | Índice con gancho corto | Gancho más pronunciado en la referencia | needs_adjustment | P1 |
| Y | Pulgar y meñique extendidos, resto curvado | Igual | Diferencia mínima | correct | — |
| Z | **Índice y medio extendidos juntos**; **trazo en Z con flecha** | Solo índice extendido + trazo Z de 10 cm inventado | Faltan dedos; trayectoria sin documentar | needs_adjustment | P1 (pose) / P2 (movimiento) |

**Resumen:** `correct` 4 (L, V, W, Y) · `needs_adjustment` 21 · `insufficient_reference` 2 (P, Q) · `missing_reference` 0. La etiqueta `PoseValidation.ApproximateFromReference` del asset es **inexacta** para A, B, C y D: la auditoría las marca como `needs_adjustment`. No se modificó el asset.

## 4. Auditoría de movimientos

| Letra | Indicio en la referencia | Documentado | Desconocido | Implementación actual |
|---|---|---|---|---|
| G | Flecha curva naranja junto a la mano | Existe movimiento; forma curva y sentido (por la punta de la flecha) | Amplitud, velocidad, repeticiones, giro de muñeca | Ninguno |
| H | Flecha curva descendente sobre la mano | Existe movimiento; arco hacia abajo-derecha | Amplitud, velocidad, repeticiones | Ninguno |
| J | Flecha curva bajo el dedo extendido | Existe movimiento curvo | Trayectoria exacta, amplitud, velocidad | Recorrido **inventado** (abajo + gancho a la izquierda) |
| Ñ | Flecha doble horizontal bajo las puntas | Oscilación lateral de las puntas | Amplitud, ciclos, velocidad; si gira la muñeca o la mano | Roll ±10° **inventado** |
| S | Trazo en S con punta de flecha | Forma S y sentido de la flecha | Tamaño, velocidad | Ninguno |
| Z | Glifo Z con flecha | Forma Z (barra superior, diagonal, barra inferior) y sentido | Tamaño, velocidad, mano que traza (dedos extendidos) | Trazo Z de 10 cm **inventado** |

Una imagen estática no permite reconstruir amplitud, velocidad ni número de repeticiones. Los movimientos actuales de J, Ñ y Z deben considerarse **provisionales**. Ninguna otra letra muestra indicios de movimiento.

## 5. Diferencias principales (agrupadas)

1. **Dedo extendido equivocado:** F, I, U, T, Z (y Ñ/N/M en número de dedos).
2. **Aducción (dedos juntos):** B, H, R, M, N; el rig en reposo tiene los dedos en abanico y `spread = 0` no los junta.
3. **Pulgar:** A (pegado), C/D/E/O (oposición y contacto), F (a lo largo del índice), T/I (cruzado).
4. **Orientación de la muñeca:** G, H, K, M, N, P, Q se muestran de lado o con la mano apuntando hacia abajo.
5. **Contacto entre yemas:** D, O (círculos).
6. **Cruce de dedos:** R.

## 6. Problemas del rig o del controlador

1. **Aducción no modelada:** el reposo del rig separa los dedos; con `spread = 0` no quedan juntos. Faltan valores de aducción calibrados (B, U, R, H, M, N).
2. **Pulgar con un solo eje de apertura:** `HandPoseDriver` aplica flexión (Z local) a las 3 articulaciones y apertura (X local) solo a la primera. No hay rotación del pulgar sobre su eje largo ni apertura por articulación, lo que limita la oposición necesaria en C, D, E, O y Q.
3. **Semántica de ejes sin documentar:** `wristEuler` actúa sobre los ejes locales del hueso `J_Right`; `wristOffset` está en el espacio del padre (en la orientación de demostración: x = profundidad, y = abajo, z = derecha). El comentario "metros" no indica ejes. Convención `spread` positivo = hacia el lado del meñique.
4. **Hueso de la mano izquierda oculto por escala 0:** el `SkinnedMeshRenderer` conserva límites de ~0,6 m de ancho; conviene fijar límites locales para encuadre y culling.
5. **Animator del prefab:** el prefab trae un Animator que reaplica la pose base; el driver lo desactiva en `Initialize` (funciona, pero es una dependencia implícita).
6. **Cámara de aprendizaje fija de frente:** varias letras se fotografían de lado o por el dorso (K, M, N, P, Q, G, H); hará falta girar la mano (o permitir rotar la vista) para que la pose sea reconocible.
7. **Etiqueta de validación del asset** (`ApproximateFromReference`/`ProvisionalNeedsValidation`) no refleja esta auditoría.
8. **El render de comprobación requiere una instancia nueva de la mano por letra** (reutilizar una sola instancia fuera de Play deja la malla sin actualizar); solo afecta a herramientas de edición.

## 7. Recomendaciones técnicas

- Añadir al modelo de datos una tabla de **aducción** por dedo (valores de `spread` que juntan los dedos) y documentar los ejes de `wristEuler`/`wristOffset` en `HandPose.cs`.
- Ampliar el pulgar: rotación sobre su eje largo y apertura en la segunda articulación (cambio mínimo en `FingerPose`/`HandPoseDriver`).
- Fijar `localBounds` de la malla y documentar la desactivación del Animator.
- Reescribir las poses por grupos, comparando cada una con su foto en una hoja lado a lado (renderizado por letra, mismo ángulo).
- Para las letras con movimiento, no inventar parámetros: implementar solo la forma documentada por la flecha y dejar amplitud/velocidad como valores marcados "provisionales" hasta que una persona experta en LSC los confirme.
- Actualizar `validation` de cada letra tras corregir (y mantener visible "pendiente de validación" hasta revisión por una persona usuaria de LSC).

## 8. Qué puede mejorarse con las referencias actuales y qué no

**Mejorables con las fotos disponibles (la forma de los dedos es legible):** A, B, C, D, E, F, G (mano), H (mano), I, J (mano), K, M, N, Ñ (mano), O, R, S (mano), T, U, X, Z (mano). L, V, W e Y ya coinciden y solo admiten retoques menores.

**Requieren información adicional:**
- **P y Q:** vista adicional (frente/lateral) o descripción de qué dedos y qué posición del pulgar.
- **T:** confirmar qué dedo es el corto de la izquierda.
- **K, M, N, E, O:** confirmar posición/contacto del pulgar y dedos ocultos.
- **Movimiento de G, H, J, Ñ, S, Z:** amplitud, velocidad, repeticiones y qué parte de la mano se mueve (descripción de una persona experta o vídeo).
- **Validación lingüística final de las 27 letras** con una persona usuaria de LSC.

## 9. Propuesta de orden de trabajo

1. **Infraestructura mínima del rig (P0):** aducción calibrada, ejes documentados, pulgar con rotación axial, límites de malla.
2. **Letras de configuración clara y cambio sencillo (P1):** I, F, U, R, T, X, Z (dedos), A, B, D.
3. **Letras de orientación/pulgar (P2):** C, E, O, K, G, H, M, N, Ñ (pose estática), con ajuste de la vista de la cámara si hace falta.
4. **Movimientos documentados (P2):** G, H, J, Ñ, S, Z con la forma de la flecha y parámetros marcados provisionales.
5. **Re-auditoría** con la misma hoja lado a lado y actualización de `validation`.
6. **P3:** P y Q cuando exista información adicional; revisión por una persona usuaria de LSC.
