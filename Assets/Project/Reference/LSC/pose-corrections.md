# Correcciones de poses estáticas P1 (Fase 6.3)

Rama: `feature/one-level` · Datos: `Assets/Project/Data/LscAlphabet.asset` · Referencias: `Assets/Project/Reference/LSC/Alphabet/`

> **Estado de validación:** ninguna pose está validada por una persona usuaria de LSC. "Coincidencia visual" significa solo que la silueta del modelo 3D se parece razonablemente a la fotografía, en el mismo encuadre (palma hacia la cámara). El juego sigue mostrando "pendiente de validación" en todas las letras.

## Método

Para cada letra: foto original → pose anterior → ajuste con los campos de `HandPose`/`FingerPose` → render con el mismo encuadre de la auditoría (frente y lado) → comparación lado a lado → ajuste → comprobación en Game View. Los ángulos del pulgar se exploraron con candidatos renderizados (con apoyo de una búsqueda numérica de la punta del pulgar, descartada cuando daba valores poco naturales).

Convenciones (ver `HandPose.cs`): `spread` del pulgar positivo lo lleva hacia la mano; `spreadMiddle` es la apertura de la 2.ª articulación; `adduction` 0 = reposo del rig, 1 = dedos paralelos al medio. Campos nuevos de la Fase 6.2 usados: `adduction` (global e individual), `twist` y `spreadMiddle` del pulgar. `opposition` y `wristEuler`/`wristOffset` **no** hicieron falta.

## Tabla de correcciones

| Letra | Problema inicial | Corrección realizada | Resultado | Pendiente |
|---|---|---|---|---|
| A | Pulgar muy separado (≈45° hacia fuera) | Pulgar pegado al costado del índice: `along` | Coincidencia visual buena (puño + pulgar pegado apuntando arriba) | Validación LSC |
| B | Dedos en abanico; pulgar apenas visible | Aducción global 1,1; pulgar plegado sobre la palma (`B-thumb`) | Coincidencia visual buena (4 dedos juntos, pulgar cruzando la palma) | Validación LSC; el pulgar queda algo más bajo que en la foto |
| D | Solo índice arriba; no se formaba el círculo | Índice recto; medio/anular/meñique curvados y pulgar hacia la punta del medio | El círculo pulgar–medio se ve de lado; de frente (cámara actual) queda oculto tras los dedos | La foto está tomada de perfil: mostrarlo de frente requiere decidir una orientación de vista (fase de cámara/muñeca) |
| F | Tres dedos extendidos + círculo "OK" (configuración equivocada) | Solo índice extendido; medio/anular/meñique curvados (70/85/50); pulgar pegado al índice | Coincidencia visual buena | F e I se parecen: se distinguen por la posición del pulgar; confirmar con experta/o |
| I | Extendía el meñique (dedo equivocado) | Índice extendido; resto en puño; pulgar cruzado por delante (`over`) | Coincidencia visual buena | Validación LSC |
| R | Índice y medio juntos sin cruzar | Aducción 1; índice `spread` +8 e `spread` −8 en el medio (cruce de puntas); pulgar `over` | Se lee como cruzado; hay solapamiento leve entre dedos | Revisar el cruce con una experta/o; sigue `Provisional` |
| T | Puño (configuración equivocada) | Índice y medio extendidos y juntos (aducción individual del índice = 1), anular cerrado, meñique extendido aparte, pulgar `over` | La silueta coincide con la foto (dos dedos altos juntos + uno corto a la izquierda) | **Identidad del dedo corto sin confirmar** (ver más abajo); sigue `Provisional` |
| U | Índice y medio extendidos juntos (dedos equivocados) | Índice y meñique extendidos; medio y anular cerrados; pulgar `over` | Coincidencia visual buena ("cuernos") | Validación LSC |
| X | Gancho del índice poco pronunciado | Índice (0,15,95,30): gancho en la falange media; pulgar `over` | Coincidencia visual razonable (la foto es de perfil) | Validación LSC |
| Z | Solo el índice extendido | Índice y medio extendidos y juntos (aducción global 1,15), anular/meñique cerrados, pulgar `over`. Los 4 fotogramas de movimiento reciben la nueva configuración de dedos; `wristEuler`/`wristOffset` y tiempos **no se tocaron** | Coincidencia visual buena de los dedos | La trayectoria sigue siendo provisional e inventada (fase de movimiento) |

## Valores utilizados

Notación: `F(spread, proximal, medio, distal)`. `Ext` = F(0,0,0,0). `Puño` = F(0,85,100,60). Todos los demás campos valen 0 salvo indicación.

Pulgares reutilizados:

| Nombre | Valores | Uso |
|---|---|---|
| `along` | spread −4, curl 0/0/0, `twist` 1, `spreadMiddle` 40 | A, F |
| `over` | spread 42, curl 0/0/0, `spreadMiddle` 40 | I, R, T, U, X, Z |
| `B-thumb` | spread 42, curl 20/20/10, `spreadMiddle` 40 | B |
| D | spread 34, curl 0/0/0, `twist` 10, `spreadMiddle` 40 | D |

Poses:

| Letra | Pulgar | Índice | Medio | Anular | Meñique | `adduction` (mano) |
|---|---|---|---|---|---|---|
| A | `along` | Puño | Puño | Puño | Puño | 0 |
| B | `B-thumb` | Ext | Ext | Ext | Ext | 1,1 |
| D | D | Ext | F(0,50,65,45) | F(0,50,65,45) | F(0,50,65,45) | 0 |
| F | `along` | Ext | F(0,70,85,50) | F(0,70,85,50) | F(0,70,85,50) | 0 |
| I | `over` | Ext | Puño | Puño | Puño | 0 |
| R | `over` | F(8,0,0,0) | F(−8,0,0,0) | Puño | Puño | 1 |
| T | `over` | Ext + `adduction` 1 (individual) | Ext | Puño | Ext | 0 |
| U | `over` | Ext | Puño | Puño | Ext | 0 |
| X | `over` | F(0,15,95,30) | Puño | Puño | Puño | 0 |
| Z | `over` | Ext | Ext | Puño | Puño | 1,15 |

Textos de instrucción actualizados para describir estas poses. `validation`: `ApproximateFromReference` para A, B, D, F, I, U y X; `ProvisionalNeedsValidation` para R, T y Z. La interfaz muestra "pendiente de validación" en ambos casos.

## Comparaciones realizadas

Para cada una de las 10 letras se generaron: (1) foto + render frente + render lado (encuadre de la auditoría), y (2) foto + captura de Game View a 1920×1080 + captura a 1280×720. Las capturas del Game View se tomaron en Play Mode sobre `AlphabetHouse`, sin guardar la escena. También se capturaron L, V, W e Y.

## Incertidumbre de la T

La fotografía muestra tres dedos extendidos: dos altos y juntos, y uno más corto y separado a la izquierda; el pulgar cruza por delante de los dedos restantes. La imagen no permite identificar con certeza cuáles son:

- **Lectura adoptada (no confirmada):** índice + medio juntos, anular cerrado bajo el pulgar y meñique extendido aparte. Es la lectura más consistente con la separación observada entre el dedo corto y el par alto, y con que el pulgar quede apoyado sobre el dedo cerrado.
- **Alternativa posible:** medio + anular altos, índice cerrado y meñique extendido.

La pose sigue marcada como `ProvisionalNeedsValidation`. Hace falta que una persona usuaria de LSC confirme qué dedos están extendidos.

## Pruebas ejecutadas

- **Datos:** las 17 letras fuera de alcance son idénticas a las de antes (pose, texto, validación y movimiento); las 10 de alcance cambiaron; las trayectorias de J, Ñ y Z (tiempos, `wristEuler`, `wristOffset`) no cambiaron.
- **Compatibilidad del rig:** las poses previas siguen produciendo los mismos ángulos (prueba de la Fase 6.2).
- **Game View (1920×1080 y 1280×720):** las 14 letras probadas (10 corregidas + L, V, W, Y) producen 14 poses distintas; el contador "Letras vistas n/27" avanza y la selección sigue a la letra; flecha derecha, PageDown/PageUp y SIGUIENTE navegan; Esc cierra. Z y J conservan su movimiento.
- **Visual:** sin atravesamientos evidentes de malla ni deformaciones en el pulgar. Se observó un leve solapamiento de dedos en R (esperable en un cruce).

## Problemas que persisten

- **Orientación de la cámara:** algunas fotos están tomadas de perfil (D, X); desde la cámara frontal del juego no se ve el círculo de la D.
- **R:** el cruce solapa los dedos de forma leve.
- **B:** el pulgar cruza algo más bajo que en la foto.
- **Z, J, Ñ:** trayectorias aún provisionales.

## Letras que requieren información adicional

- **T:** identidad del dedo corto.
- **F / I:** confirmar la diferencia exacta de posición del pulgar.
- **R:** confirmar qué dedo pasa por delante.
- **P y Q** (no trabajadas): vista adicional o descripción.
- **Movimientos** de G, H, J, Ñ, S y Z.

## Letras no modificadas

C, E, G, H, J, K, L, M, N, Ñ, O, P, Q, S, V, W e Y permanecen exactamente como estaban.

---

# Fase 6.4 — Poses estáticas P2: C, E, O, K, G, H, M, N, Ñ

Todas siguen **pendientes de validación lingüística**. "Resultado" = parecido visual con la foto en Game View (1920×1080 y 1280×720), nada más. Controles de la Fase 6.2 usados: `adduction` (global e individual), `opposition` y `spreadMiddle` del pulgar, `wristEuler` y `wristOffset`. No se añadieron controles. Movimientos no modificados.

Notación: `F(spread, proximal, medio, distal)`; `Over` = pulgar (spread 42, spreadMiddle 40); `Puño` = F(0,85,100,60); `Ext` = F(0,0,0,0).

| Letra | Problema inicial | Parámetros finales | Resultado | Incertidumbre / pendiente |
|---|---|---|---|---|
| C | Dedos semicerrados tipo garra, pulgar apenas curvado | Dedos F(0,35,45,25) con aducción 1; pulgar `opposition` 1; `wristEuler` (0,90,0) (mano de canto) | Arco de la C con el pulgar enfrentado, sin parecer garra | El pulgar queda más corto/cerca que en la foto |
| E | Casi un puño, pulgar oculto | Dedos F(0,60,90,45) con aducción 1; pulgar `Over` con curl 20/20/10 | Dedos curvados sobre la palma y pulgar visible por delante | Posición real del pulgar y de los dedos ocultos no se distingue: sigue `Provisional` |
| O | Dedos separados sin cerrar el círculo | Dedos F(0,50,60,45), aducción 0,8; pulgar `opposition` 1; `wristEuler` (0,70,0) | Círculo formado entre pulgar y dedos, visto de canto | El contacto de yemas es aproximado |
| K | Mano de frente con V vertical | Índice `spread` −30, medio Ext, anular y meñique Puño, pulgar `Over`; `wristEuler` (0,0,80); `wristOffset` (0,0,0,12) | Mano de lado; índice arriba-izquierda y medio horizontal, separados | Pulgar y dedos ocultos sin confirmar; `Provisional` |
| G | Índice horizontal recto, vista frontal | Índice F(0,0,20,25), resto Puño, pulgar `Over`; `wristEuler` (0,0,60) | Mano de lado, índice hacia arriba-izquierda | El índice en la foto está curvado y la mano más cerrada; flecha (movimiento) no implementada |
| H | Índice y medio separados | Índice con aducción 1 + medio Ext, resto Puño, pulgar `Over`; `wristEuler` (0,180,90); `wristOffset` (0,0,−0,16) | Mano de lado con dos dedos juntos hacia la derecha, dentro del encuadre | En la foto la muñeca está flexionada; flecha no implementada |
| M | Puño de frente (no coincidía) | Índice, medio y anular Ext (aducción individual 1 en índice y anular), meñique Puño, pulgar recogido (0,70,60,40); `wristEuler` (0,0,180); `wristOffset` (0,−0,3,0) | Mano boca abajo con tres dedos juntos hacia abajo | Pulgar recogido sin confirmar; sin antebrazo ni flexión de muñeca como en la foto |
| N | Idéntica a M | Igual que M pero anular en Puño (dos dedos hacia abajo) | Claramente distinta de M (dos dedos frente a tres) | Pulgar y dedos recogidos sin confirmar |
| Ñ | Idéntica a N | Misma pose estática que N. Los 4 fotogramas del movimiento actual conservan sus deltas de muñeca y tiempos; solo se les sumó la orientación base de N | Estática = N; movimiento provisional intacto | Oscilación sigue **provisional** (amplitud inventada); datos: tiempos y deltas idénticos a antes |

Textos de instrucción de las 9 letras actualizados. `validation`: `ApproximateFromReference` en C y O; `ProvisionalNeedsValidation` en E, K, G, H, M, N y Ñ.

## Pruebas

- **Datos:** las otras 18 letras (incluidas A, B, D, F, I, R, T, U, X, Z, L, V, W, Y) son idénticas a antes; en J, Ñ y Z los tiempos y deltas de muñeca no cambiaron; G y H siguen sin movimiento.
- **Game View, ambas resoluciones:** 21 poses estáticas distintas entre sí; M y N distintas; N estable durante 300 fotogramas; navegación (flecha, PageDown, SIGUIENTE, Esc) correcta; la mano siempre visible; Z y J conservan su movimiento; Ñ lo conserva (medido a 1280×720; a 1920×1080 una medición cayó en la pausa del bucle y dio 0, sin relación con los datos).
- El contador "Letras vistas" cuenta también la letra A que se muestra al abrir el nivel (comportamiento previo).

## Pendiente
- Fotos de M/N/Ñ y H muestran antebrazo y muñeca flexionada que el rig no reproduce con la cámara frontal.
- Confirmar con una persona usuaria de LSC: pulgar de E, K, M, N; dedos ocultos.
- Movimientos de G, H y Ñ (flechas): sin implementar.
