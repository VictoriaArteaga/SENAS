# Matriz de validación integral de las 27 letras LSC (Fase 6.7A)

Rama `feature/one-level` · Documento de auditoría: **no se modificó ninguna pose, movimiento, rig, cámara, escena ni script**. Los valores técnicos de cada letra están leídos de `Assets/Project/Data/LscAlphabet.asset` (solo lectura).

> **Estado general:** ninguna de las 27 letras está validada lingüísticamente. Una pose visualmente parecida a una fotografía **no equivale** a una validación. Todos los parámetros numéricos de movimiento son **provisionales**. La validación debe hacerla una persona competente en Lengua de Señas Colombiana (LSC).

## Estados de validación (únicos permitidos)

| Estado | Significado |
|---|---|
| `PENDIENTE` | Aún no la ha evaluado una persona competente en LSC. |
| `VALIDADA` | La persona validadora la considera una representación adecuada de la letra. |
| `REQUIERE_AJUSTE` | La persona validadora indica que debe ajustarse (con observaciones). |
| `REFERENCIA_INSUFICIENTE` | La referencia disponible no permite determinar la configuración. |

`P` y `Q` figuran como `REFERENCIA_INSUFICIENTE` porque la auditoría de la Fase 6.1 las clasificó con referencia insuficiente; el resto figura como `PENDIENTE`.

## Fuentes consultadas

`pose-audit.md` (Fase 6.1), `pose-corrections.md` (Fases 6.3 y 6.4), `camera-visualization-report.md` (Fase 6.5), `movement-audit.md` (Fases 6.6 y 6.6-B), las 27 imágenes de `Assets/Project/Reference/LSC/Alphabet/`, `LscAlphabet.asset`, `HandPose.cs`, `HandPoseDriver.cs` y `AlphabetLevelController.cs`. Las conclusiones previas se conservan tal cual; no se añadió conocimiento externo sobre LSC.

## Limitaciones transversales (afectan a todas las letras)

- **Referencias:** 27 de 27 fotografías de una mano real, de 161–177 × 202–221 px; resolución baja para detalles finos (contacto de yemas, dedos ocultos). Flechas naranjas solo en G, H, J, Ñ, S y Z. La hoja antigua `lsc_alphabet_reference.jpeg` quedó superada.
- **Modelo:** una única mano derecha (pack «Free VR Hands»); sin antebrazo (la malla termina en un corte plano en la muñeca); sin información de expresión facial ni de otros componentes no manuales.
- **Cámara:** solo giros horizontales (FRENTE, PERFIL a −90°, DORSO a 180°); no hay vista desde arriba ni órbita libre; en DORSO la mano se ve más oscura. Las poses son idénticas en las tres vistas (cambiar de vista solo mueve la cámara).
- **Etiqueta en el juego:** todas las letras muestran «pendiente de validación»; el asset distingue «Referencia aproximada» y «Pose provisional» (columna «Etiqueta en juego» de cada letra).
- **Movimiento:** una imagen estática con flecha no permite conocer amplitud, velocidad ni número de repeticiones; todos los valores son provisionales. El bucle repite el movimiento con una pausa de 1,1 s al final de cada ciclo.

## Matriz resumida

Prioridad (a partir de las incertidumbres ya documentadas, sin interpretación nueva): **Alta** = referencia insuficiente, discrepancia documentada entre referencia y pose, o identidad de dedos sin confirmar; **Media** = al menos una incertidumbre o limitación específica documentada (elementos ocultos, pulgar, muñeca/antebrazo, círculo/cruce, movimiento provisional, referencia aproximada); **Baja** = sin incertidumbre específica documentada (igualmente pendiente de validación).

| Letra | Pose | Movimiento | Referencia | Estado LSC | Prioridad |
|---|---|---|---|---|---|
| A | Implementada | No | Disponible | PENDIENTE | Baja |
| B | Implementada | No | Disponible | PENDIENTE | Media |
| C | Implementada | No | Aproximada | PENDIENTE | Media |
| D | Implementada | No | Disponible con limitaciones | PENDIENTE | Media |
| E | Implementada | No | Disponible con limitaciones | PENDIENTE | Media |
| F | Implementada | No | Disponible | PENDIENTE | Media |
| G | Implementada | Sí (provisional) | Disponible con limitaciones | PENDIENTE | Media |
| H | Implementada | Sí (provisional) | Disponible con limitaciones | PENDIENTE | Media |
| I | Implementada | No | Disponible | PENDIENTE | Media |
| J | Implementada | Sí (provisional) | Disponible con limitaciones | PENDIENTE | Media |
| K | Implementada | No | Disponible con limitaciones | PENDIENTE | Media |
| L | Implementada | No | Disponible | PENDIENTE | Baja |
| M | Implementada | No | Disponible con limitaciones | PENDIENTE | Media |
| N | Implementada | No | Disponible con limitaciones | PENDIENTE | Media |
| Ñ | Implementada | Sí (provisional) | Disponible con limitaciones | PENDIENTE | Media |
| O | Implementada | No | Aproximada | PENDIENTE | Media |
| P | Implementada | No | Insuficiente | REFERENCIA_INSUFICIENTE | Alta |
| Q | Implementada | No | Insuficiente | REFERENCIA_INSUFICIENTE | Alta |
| R | Implementada | No | Disponible con limitaciones | PENDIENTE | Media |
| S | Implementada | Sí (provisional) | Discrepante con la pose | PENDIENTE | Alta |
| T | Implementada | No | Disponible con limitaciones | PENDIENTE | Alta |
| U | Implementada | No | Disponible | PENDIENTE | Baja |
| V | Implementada | No | Disponible | PENDIENTE | Baja |
| W | Implementada | No | Disponible | PENDIENTE | Baja |
| X | Implementada | No | Disponible con limitaciones | PENDIENTE | Media |
| Y | Implementada | No | Disponible | PENDIENTE | Baja |
| Z | Implementada | Sí (provisional) | Disponible con limitaciones | PENDIENTE | Media |

Resumen: Alta (4): P, Q, S, T · Media (17): B, C, D, E, F, G, H, I, J, K, M, N, Ñ, O, R, X, Z · Baja (6): A, L, U, V, W, Y. Letras con movimiento: G, H, J, Ñ, S, Z.

## Instrucciones para el validador LSC

Esta sesión evalúa **la representación de la LSC**, no el funcionamiento técnico en Unity: no hace falta saber si algo es correcto técnicamente.

**Cómo ver cada letra en el juego:** abrir el nivel del abecedario, elegir la letra (botones, flecha derecha o PageDown), y con **V** o el botón `VISTA: ...` alternar FRENTE → PERFIL → DORSO. Con REPETIR (o la tecla R) se reproduce de nuevo el movimiento. La mano mostrada es una mano derecha.

Para cada letra, evaluar y registrar:

1. **Pose.** ¿La configuración de los dedos y del pulgar corresponde a la letra? (cuáles dedos están extendidos o cerrados, separación o unión, posición y contacto del pulgar).
2. **Orientación.** ¿La orientación de la palma, el dorso y la muñeca corresponde? (hacia dónde mira la palma, hacia dónde apuntan los dedos).
3. **Movimiento** (solo las letras que lo tienen: G, H, J, Ñ, S, Z). ¿La trayectoria y la forma del movimiento corresponden? ¿Es adecuado el sentido? Indicar amplitud, velocidad y número de repeticiones adecuados, si procede (los valores actuales son provisionales).
4. **Vista.** Revisar en FRENTE, PERFIL y DORSO la profundidad y la orientación; indicar si alguna vista es la más adecuada para esa letra.
5. **Resultado.** Escribir uno de: `VALIDADA`, `REQUIERE_AJUSTE`, `REFERENCIA_INSUFICIENTE`, y observaciones (qué cambiaría y cómo se hace correctamente, con una descripción o una referencia mejor si la tiene).

**Limitaciones que conviene tener presentes** (no son errores de la persona evaluadora): el modelo no tiene antebrazo; las fotos de referencia son de baja resolución y algunas están tomadas de lado o de perfil; los movimientos son aproximaciones técnicas provisionales; el bucle hace una pausa de 1,1 s entre repeticiones.

Cada letra tiene una sección «Validación LSC» con los campos a completar. Mientras no se rellene, el estado se mantiene en `PENDIENTE` (o `REFERENCIA_INSUFICIENTE` en P y Q).

---

## Fichas por letra

## A

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Referencia aproximada · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Puño cerrado con el pulgar pegado al costado del índice, apuntando hacia arriba.»
- Prioridad de validación: Baja

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/A.png` (165×205 px)
- Calidad: Buena (forma general legible)
- Tipo de referencia: Fotografía de mano real, sin flecha.
- Limitaciones: Resolución baja (≈165×205 px).

### Pose

- Configuración de dedos: Índice, medio, anular y meñique en puño.
- Configuración del pulgar: Pegado al costado del índice, apuntando arriba.
- Aducción: 0 (reposo del rig).
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - Coincidencia visual buena según la Fase 6.3; solo falta validación lingüística.
  - Auditoría 6.1: el pulgar estaba ≈45° separado (corregido en 6.3).
- Valores del asset (solo lectura): Pulgar: spread -4, flexión 0/0/0, twist 1, spreadMiddle 40; Índice: spread 0, flexión 85/100/60; Medio: spread 0, flexión 85/100/60; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Pose legible de frente.
- PERFIL: Beneficia: pulgar respecto de la palma (camera-visualization-report §6).
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## B

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Referencia aproximada · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Mano abierta con los cuatro dedos extendidos y juntos, y el pulgar plegado sobre la palma.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/B.png` (171×206 px)
- Calidad: Buena
- Tipo de referencia: Fotografía de mano real, sin flecha.
- Limitaciones: Resolución baja.

### Pose

- Configuración de dedos: Cuatro dedos extendidos y juntos.
- Configuración del pulgar: Plegado sobre la palma.
- Aducción: 1,1 (dedos juntos).
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - El pulgar cruza algo más bajo que en la foto (Fase 6.3).
  - Coincidencia visual buena según la Fase 6.3.
- Valores del asset (solo lectura): Pulgar: spread 42, flexión 20/20/10, spreadMiddle 40; Índice: spread 0, flexión 0/0/0; Medio: spread 0, flexión 0/0/0; Anular: spread 0, flexión 0/0/0; Meñique: spread 0, flexión 0/0/0.
- Aducción de la mano: 1.1. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Dedos juntos legibles.
- PERFIL: Beneficia: pulgar respecto de la palma.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## C

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Referencia aproximada · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Dedos juntos y curvados en arco; el pulgar enfrentado forma la abertura de la C. Mano de canto.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/C.png` (161×206 px)
- Calidad: Aproximada (la forma se distingue; la posición del pulgar no se distingue con precisión)
- Tipo de referencia: Fotografía de mano real, mano de canto, sin flecha.
- Limitaciones: Resolución baja; mano de canto.

### Pose

- Configuración de dedos: Dedos juntos y curvados en arco.
- Configuración del pulgar: Enfrentado a los dedos (oposición 1) formando la abertura.
- Aducción: 1.
- Orientación: Mano de canto (giro axial de muñeca 90°).
- Incertidumbres:
  - Referencia aproximada.
  - Diferencia en la posición del pulgar: queda más corto/cerca que en la foto (Fase 6.4).
  - Fase 6.1: de frente parecía una garra; se corrigió con el giro de mano de canto.
- Valores del asset (solo lectura): Pulgar: spread 0, flexión 0/0/0, oposición 1; Índice: spread 0, flexión 35/45/25; Medio: spread 0, flexión 35/45/25; Anular: spread 0, flexión 35/45/25; Meñique: spread 0, flexión 35/45/25.
- Aducción de la mano: 1. Muñeca `wristEuler` (0, 90, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: De canto, el arco se ve de frente solo parcialmente.
- PERFIL: Beneficia: el arco de la C con el pulgar (camera-visualization-report §6).
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## D

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Referencia aproximada · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Índice extendido hacia arriba; los demás dedos curvados tocan la punta del pulgar formando un círculo.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/D.png` (165×208 px)
- Calidad: Buena para el índice; el círculo se ve de perfil
- Tipo de referencia: Fotografía de mano real tomada de perfil, sin flecha.
- Limitaciones: La foto está tomada de perfil; el círculo del pulgar con el medio no se aprecia de frente.

### Pose

- Configuración de dedos: Índice extendido hacia arriba; medio, anular y meñique curvados.
- Configuración del pulgar: Hacia la punta del medio formando el círculo (contacto aproximado).
- Aducción: 0.
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - De frente el círculo pulgar–medio queda oculto tras los dedos (Fase 6.3).
  - El contacto de yemas es aproximado (misma incertidumbre documentada para D y O).
  - Fase 6.1: no se formaba el círculo (corregido en 6.3).
- Valores del asset (solo lectura): Pulgar: spread 34, flexión 0/0/0, twist 10, spreadMiddle 40; Índice: spread 0, flexión 0/0/0; Medio: spread 0, flexión 50/65/45; Anular: spread 0, flexión 50/65/45; Meñique: spread 0, flexión 50/65/45.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: El círculo no se ve.
- PERFIL: Beneficia: se ve el círculo pulgar–medio (camera-visualization-report §3 y §6).
- DORSO: Aporta menos que el perfil.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## E

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Pose provisional · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Dedos curvados sobre la palma; el pulgar cruza por delante, visible. (Posición exacta del pulgar sin confirmar.)»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/E.png` (167×207 px)
- Calidad: Limitada (dedos y pulgar ocultos)
- Tipo de referencia: Fotografía de mano real, sin flecha.
- Limitaciones: Los dedos curvados ocultan la posición del pulgar.

### Pose

- Configuración de dedos: Dedos curvados sobre la palma.
- Configuración del pulgar: Cruza por delante, visible (posición exacta sin confirmar).
- Aducción: 1.
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - Posición real del pulgar y de los dedos ocultos no se distingue en la referencia (Fase 6.4).
  - Pose marcada como provisional en el asset.
- Valores del asset (solo lectura): Pulgar: spread 42, flexión 20/20/10, spreadMiddle 40; Índice: spread 0, flexión 60/90/45; Medio: spread 0, flexión 60/90/45; Anular: spread 0, flexión 60/90/45; Meñique: spread 0, flexión 60/90/45.
- Aducción de la mano: 1. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Beneficia: pulgar y dedos (camera-visualization-report §6).
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## F

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Referencia aproximada · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Índice extendido hacia arriba, los demás dedos curvados y el pulgar pegado al costado del índice.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/F.png` (167×207 px)
- Calidad: Buena
- Tipo de referencia: Fotografía de mano real, sin flecha.
- Limitaciones: Resolución baja.

### Pose

- Configuración de dedos: Solo el índice extendido; medio, anular y meñique curvados.
- Configuración del pulgar: Pegado al costado del índice.
- Aducción: 0.
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - F e I se parecen: se distinguen por la posición del pulgar; confirmar con una persona usuaria de LSC (Fase 6.3).
  - Fase 6.1: la configuración anterior era equivocada (corregida en 6.3).
- Valores del asset (solo lectura): Pulgar: spread -4, flexión 0/0/0, twist 1, spreadMiddle 40; Índice: spread 0, flexión 0/0/0; Medio: spread 0, flexión 70/85/50; Anular: spread 0, flexión 70/85/50; Meñique: spread 0, flexión 70/85/50.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Sin hallazgos específicos documentados.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## G

### Estado actual

- Pose: implementada
- Movimiento: sí (provisional)
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Pose provisional · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Mano de lado: índice extendido apuntando hacia arriba y a la izquierda; los demás dedos cerrados.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/G.png` (171×208 px)
- Calidad: Limitada (índice curvado, mano más cerrada que la pose)
- Tipo de referencia: Fotografía de mano real de lado, con flecha naranja curva.
- Limitaciones: La imagen estática no da amplitud, velocidad ni repeticiones del movimiento.

### Pose

- Configuración de dedos: Índice extendido/ligeramente curvado (20°/25° en las falanges media y distal); medio, anular y meñique en puño.
- Configuración del pulgar: Cruzado por delante.
- Aducción: 0.
- Orientación: Mano de lado: inclinación de muñeca (eje Z) 60°; índice hacia arriba-izquierda.
- Incertidumbres:
  - Incertidumbre sobre la curvatura/cierre del índice: en la foto el índice está curvado y la mano más cerrada (Fase 6.4).
  - Pulgar sin confirmar.
  - Movimiento provisional (ver Movimiento).
- Valores del asset (solo lectura): Pulgar: spread 42, flexión 0/0/0, spreadMiddle 40; Índice: spread 0, flexión 0/20/25; Medio: spread 0, flexión 85/100/60; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 60), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: sí
- Dirección: Hacia el lado del índice (izquierda de pantalla) y retorno.
- Trayectoria: Giro de muñeca +15° y +8° combinado con un arco corto: 0 s base → 0,45 s (giro Z +15°, desplazamiento Z −0,02 m) → 0,90 s (giro +8°, desplazamiento Y +0,025 m, Z −0,01 m) → 1,35 s base.
- Duración: 1,35 s + pausa de 1,1 s del driver.
- Ciclos: 1 trazo por ciclo de bucle.
- Parámetros provisionales: Todos los valores numéricos (amplitud, tiempos, combinación giro + desplazamiento). Todos los valores numéricos son provisionales.
- Limitaciones de la referencia: La referencia solo muestra una flecha curva a la izquierda con retorno; no permite fijar amplitud, velocidad, repeticiones ni si el movimiento es de muñeca o antebrazo.
- Pendiente de validación LSC (movimiento).

### Perspectivas

- FRENTE: Mano de lado: se reconoce la orientación.
- PERFIL: Beneficia: mano de lado (camera-visualization-report §6).
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: pendiente
- Orientación: pendiente
- Observaciones:

---

## H

### Estado actual

- Pose: implementada
- Movimiento: sí (provisional)
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Pose provisional · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Mano de lado: índice y medio extendidos y juntos, apuntando a la derecha; los demás dedos cerrados.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/H.png` (171×215 px)
- Calidad: Limitada (muñeca flexionada y antebrazo no reproducidos)
- Tipo de referencia: Fotografía de mano real de lado con antebrazo, con flecha naranja curva.
- Limitaciones: La foto muestra antebrazo y muñeca flexionada que el rig no reproduce.

### Pose

- Configuración de dedos: Índice y medio extendidos y juntos; anular y meñique en puño.
- Configuración del pulgar: Cruzado por delante.
- Aducción: Aducción individual 1 en el índice (juntos con el medio).
- Orientación: Mano de lado, dedos hacia la derecha (giro axial 180° e inclinación 90°); desplazada 0,16 m hacia la izquierda para entrar en el encuadre.
- Incertidumbres:
  - En la foto la muñeca está flexionada; no reproducida (Fase 6.4).
  - Limitación de muñeca/antebrazo: el modelo termina en un corte plano en la muñeca (camera-visualization-report §4).
  - Movimiento provisional; se implementó solo traslación porque el componente de inclinación de muñeca no se pudo razonar sin inspección visual de signos.
- Valores del asset (solo lectura): Pulgar: spread 42, flexión 0/0/0, spreadMiddle 40; Índice: spread 0, flexión 0/0/0, aducción individual 1; Medio: spread 0, flexión 0/0/0; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 180, 90), `wristOffset` (0, 0, -0.16).

### Movimiento

- Existe movimiento: sí
- Dirección: De izquierda a derecha (hacia donde apuntan los dedos), terminando hacia abajo.
- Trayectoria: Arco de traslación respecto de la pose: 0,5 s (Y −0,02 m, Z +0,03 m) → 1,0 s (Y 0, Z +0,06 m) → 1,5 s (Y +0,03 m, Z +0,08 m) → 2,0 s base.
- Duración: 2,0 s + pausa de 1,1 s.
- Ciclos: 1 arco por ciclo de bucle.
- Parámetros provisionales: Amplitud (≈8 cm), tiempos y altura del arco; ausencia del componente de inclinación de muñeca. Todos los valores numéricos son provisionales.
- Limitaciones de la referencia: La foto muestra una flecha curva sobre los dedos; no da amplitud, velocidad ni repeticiones, y no aclara si el gesto implica giro de muñeca.
- Pendiente de validación LSC (movimiento).

### Perspectivas

- FRENTE: Mano de lado, dedos hacia la derecha.
- PERFIL: Beneficia (camera-visualization-report §6).
- DORSO: Beneficia: dorso.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: pendiente
- Orientación: pendiente
- Observaciones:

---

## I

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Referencia aproximada · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Índice extendido hacia arriba, los demás dedos cerrados y el pulgar cruzado por delante.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/I.png` (171×211 px)
- Calidad: Buena
- Tipo de referencia: Fotografía de mano real, sin flecha.
- Limitaciones: Resolución baja.

### Pose

- Configuración de dedos: Índice extendido hacia arriba; los demás en puño.
- Configuración del pulgar: Cruzado por delante.
- Aducción: 0.
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - F e I se parecen: confirmar la diferencia de posición del pulgar (Fase 6.3).
  - Fase 6.1: la pose anterior extendía el dedo equivocado (corregida en 6.3).
- Valores del asset (solo lectura): Pulgar: spread 42, flexión 0/0/0, spreadMiddle 40; Índice: spread 0, flexión 0/0/0; Medio: spread 0, flexión 85/100/60; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Sin hallazgos específicos documentados.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## J

### Estado actual

- Pose: implementada
- Movimiento: sí (provisional)
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Pose provisional · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Como la I, trazando una J en el aire con la mano.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/J.png` (169×221 px)
- Calidad: Limitada (trayectoria y lado del gancho no determinables)
- Tipo de referencia: Fotografía de mano real con flecha naranja en gancho.
- Limitaciones: No permite conocer amplitud, velocidad ni el lado en que se curva el gancho con certeza.

### Pose

- Configuración de dedos: Meñique extendido; índice, medio y anular en puño.
- Configuración del pulgar: Curvado sobre los dedos cerrados.
- Aducción: 0.
- Orientación: Palma hacia la cámara, sin giro de muñeca; en la foto el dedo extendido aparece inclinado a la izquierda (inclinación no reproducida en la pose estática).
- Incertidumbres:
  - La foto muestra un dedo extendido inclinado a la izquierda que "parece el meñique" (auditoría 6.1); la pose usa el meñique.
  - Observación documental de la Fase 6.7A: el texto de instrucción que ve la persona usuaria dice "Como la I" (índice) mientras la pose extiende el meñique; no se corrigió (esta fase no modifica el asset).
  - Trayectoria provisional (ver Movimiento).
- Valores del asset (solo lectura): Pulgar: spread 0, flexión 45/40/25; Índice: spread 0, flexión 85/100/60; Medio: spread 0, flexión 85/100/60; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 0/0/0.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: sí
- Dirección: Hacia abajo y gancho hacia la izquierda con inclinación.
- Trayectoria: Respecto de la pose: 0,3 s (Y −0,04 m, Z +0,01 m) → 1,0 s (Y +0,05 m, Z +0,01 m) → 1,5 s (Y +0,07 m, Z −0,04 m, giro Z 40°) → 2,1 s retorno a la pose. Inicio y retorno añadidos en la Fase 6.6-B solo para cerrar el bucle (0,3 s de aproximación y 0,6 s de retorno).
- Duración: 2,1 s + pausa de 1,1 s.
- Ciclos: 1 trazo por ciclo de bucle.
- Parámetros provisionales: Toda la trayectoria, tiempos de aproximación y retorno, y el lado del gancho. Todos los valores numéricos son provisionales.
- Limitaciones de la referencia: La flecha de la foto indica un movimiento curvo; no determina trayectoria exacta, amplitud ni velocidad.
- Pendiente de validación LSC (movimiento).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Sin hallazgos específicos documentados.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: pendiente
- Orientación: pendiente
- Observaciones:

---

## K

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Pose provisional · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Mano de lado: índice hacia arriba y a la izquierda, medio horizontal hacia la izquierda, separados; pulgar cruzado.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/K.png` (169×210 px)
- Calidad: Limitada (pulgar y dedos ocultos)
- Tipo de referencia: Fotografía de mano real de lado, sin flecha.
- Limitaciones: Mano de lado; el pulgar y los dedos cerrados no se distinguen.

### Pose

- Configuración de dedos: Índice separado hacia arriba-izquierda (spread −30) y medio extendido; anular y meñique en puño.
- Configuración del pulgar: Cruzado por delante (sin confirmar).
- Aducción: 0.
- Orientación: Mano de lado (inclinación de muñeca 80°, desplazada 0,12 m hacia el lado del pulgar).
- Incertidumbres:
  - Pulgar y dedos ocultos sin confirmar (Fase 6.4).
  - Pose marcada como provisional en el asset.
- Valores del asset (solo lectura): Pulgar: spread 42, flexión 0/0/0, spreadMiddle 40; Índice: spread -30, flexión 0/0/0; Medio: spread 0, flexión 0/0/0; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 80), `wristOffset` (0, 0, 0.12).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Beneficia: mano de lado.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## L

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Referencia aproximada · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Índice extendido hacia arriba y pulgar abierto, formando una L.»
- Prioridad de validación: Baja

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/L.png` (164×209 px)
- Calidad: Buena
- Tipo de referencia: Fotografía de mano real, sin flecha.
- Limitaciones: Resolución baja.

### Pose

- Configuración de dedos: Índice extendido hacia arriba; los demás en puño.
- Configuración del pulgar: Abierto (spread −40), formando una L.
- Aducción: 0.
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - Coincidencia visual con la foto (auditoría 6.1, clase `correct`, sin cambios); solo falta validación lingüística.
- Valores del asset (solo lectura): Pulgar: spread -40, flexión 0/0/0; Índice: spread 0, flexión 0/0/0; Medio: spread 0, flexión 85/100/60; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Sin hallazgos específicos documentados.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## M

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Pose provisional · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Mano boca abajo: índice, medio y anular extendidos y juntos hacia abajo; meñique y pulgar recogidos.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/M.png` (166×207 px)
- Calidad: Limitada (antebrazo y muñeca flexionada no reproducidos; pulgar oculto)
- Tipo de referencia: Fotografía de mano real con antebrazo horizontal, sin flecha.
- Limitaciones: La foto muestra el antebrazo y la muñeca flexionada.

### Pose

- Configuración de dedos: Índice, medio y anular extendidos y juntos hacia abajo; meñique en puño.
- Configuración del pulgar: Recogido (flexión 70/60/40), sin confirmar.
- Aducción: Aducción individual 1 en índice y anular.
- Orientación: Mano boca abajo (inclinación de muñeca 180°), desplazada 0,3 m hacia el antebrazo para entrar en el encuadre.
- Incertidumbres:
  - Pulgar recogido sin confirmar (Fase 6.4).
  - Sin antebrazo ni flexión de muñeca como en la foto.
  - Se confirmó que M y N son distintas (tres dedos frente a dos).
  - Pose marcada como provisional en el asset.
- Valores del asset (solo lectura): Pulgar: spread 0, flexión 70/60/40; Índice: spread 0, flexión 0/0/0, aducción individual 1; Medio: spread 0, flexión 0/0/0; Anular: spread 0, flexión 0/0/0, aducción individual 1; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 180), `wristOffset` (0, -0.3, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Mano boca abajo.
- PERFIL: Beneficia: dedos hacia abajo de canto; en perfil la mano queda cerca del borde superior (camera-visualization-report §8; el encuadre se ajustó en la Fase 6.6-B).
- DORSO: Beneficia: dorso.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## N

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Pose provisional · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Mano boca abajo: índice y medio extendidos y juntos hacia abajo; los demás dedos recogidos.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/N.png` (168×212 px)
- Calidad: Limitada (antebrazo y muñeca flexionada no reproducidos; pulgar oculto)
- Tipo de referencia: Fotografía de mano real con antebrazo horizontal, sin flecha.
- Limitaciones: Igual que M.

### Pose

- Configuración de dedos: Índice y medio extendidos y juntos hacia abajo; anular y meñique recogidos.
- Configuración del pulgar: Recogido (flexión 70/60/40), sin confirmar.
- Aducción: Aducción individual 1 en el índice.
- Orientación: Mano boca abajo (inclinación de muñeca 180°), desplazada 0,3 m hacia el antebrazo.
- Incertidumbres:
  - Pulgar y dedos recogidos sin confirmar (Fase 6.4).
  - Sin antebrazo ni flexión de muñeca como en la foto.
  - Pose marcada como provisional en el asset.
- Valores del asset (solo lectura): Pulgar: spread 0, flexión 70/60/40; Índice: spread 0, flexión 0/0/0, aducción individual 1; Medio: spread 0, flexión 0/0/0; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 180), `wristOffset` (0, -0.3, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Mano boca abajo.
- PERFIL: Beneficia: dedos hacia abajo de canto.
- DORSO: Beneficia: dorso.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## Ñ

### Estado actual

- Pose: implementada
- Movimiento: sí (provisional)
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Pose provisional · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Como la N (índice y medio hacia abajo). El movimiento lateral actual es provisional.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/Ñ.png` (173×206 px)
- Calidad: Limitada (amplitud del movimiento no determinable; antebrazo no reproducido)
- Tipo de referencia: Fotografía de mano real con antebrazo, con flecha naranja doble horizontal bajo las puntas.
- Limitaciones: Como N; la flecha doble no da amplitud, ciclos ni velocidad, ni si gira la muñeca o la mano.

### Pose

- Configuración de dedos: Igual que N: índice y medio extendidos y juntos hacia abajo; anular y meñique recogidos.
- Configuración del pulgar: Recogido (flexión 70/60/40), sin confirmar.
- Aducción: Aducción individual 1 en el índice.
- Orientación: Igual que N: mano boca abajo (inclinación de muñeca 180°), desplazada 0,3 m.
- Incertidumbres:
  - Pose estática idéntica a N (por decisión documentada en la Fase 6.4); conserva su propio movimiento.
  - Pulgar y dedos recogidos sin confirmar; sin antebrazo ni flexión de muñeca.
  - Movimiento provisional: amplitud inventada, asimétrica.
- Valores del asset (solo lectura): Pulgar: spread 0, flexión 70/60/40; Índice: spread 0, flexión 0/0/0, aducción individual 1; Medio: spread 0, flexión 0/0/0; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 180), `wristOffset` (0, -0.3, 0).

### Movimiento

- Existe movimiento: sí
- Dirección: Oscilación lateral de las puntas de los dedos.
- Trayectoria: Balanceo de la muñeca en el plano de la palma: giro Z 180° → 190° (0,35 s) → 174° (0,70 s) → 180° (1,0 s).
- Duración: 1,0 s + pausa de 1,1 s.
- Ciclos: Una oscilación de ida y vuelta (+10° / −6°) por ciclo de bucle.
- Parámetros provisionales: Amplitud (+10°/−6°), duración, número de oscilaciones. Todos los valores numéricos son provisionales.
- Limitaciones de la referencia: La flecha doble horizontal no da amplitud, ciclos ni velocidad, ni si gira la muñeca o la mano.
- Pendiente de validación LSC (movimiento).

### Perspectivas

- FRENTE: Mano boca abajo.
- PERFIL: Beneficia; la mano ya no supera el borde superior desde la Fase 6.6-B.
- DORSO: Beneficia: dorso.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: pendiente
- Orientación: pendiente
- Observaciones:

---

## O

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Referencia aproximada · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Dedos curvados y juntos que se acercan al pulgar formando un círculo. Mano de canto.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/O.png` (168×206 px)
- Calidad: Aproximada (contacto de yemas con el pulgar no se distingue con precisión)
- Tipo de referencia: Fotografía de mano real de canto, sin flecha.
- Limitaciones: Resolución baja; mano de canto.

### Pose

- Configuración de dedos: Dedos curvados y juntos (aducción 0,8).
- Configuración del pulgar: Enfrentado a los dedos (oposición 1), formando el círculo.
- Aducción: 0,8.
- Orientación: Mano de canto (giro axial de muñeca 70°).
- Incertidumbres:
  - Referencia aproximada.
  - El contacto de yemas es aproximado (Fase 6.4).
  - Fase 6.1: no se cerraba el círculo (corregido en 6.4).
- Valores del asset (solo lectura): Pulgar: spread 0, flexión 0/0/0, oposición 1; Índice: spread 0, flexión 50/60/45; Medio: spread 0, flexión 50/60/45; Anular: spread 0, flexión 50/60/45; Meñique: spread 0, flexión 50/60/45.
- Aducción de la mano: 0.8. Muñeca `wristEuler` (0, 70, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Beneficia: el círculo (camera-visualization-report §6).
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## P

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: REFERENCIA_INSUFICIENTE
- Etiqueta en juego: «Pose provisional · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Índice y medio extendidos con el pulgar abierto, apuntando hacia abajo.»
- Prioridad de validación: Alta

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/P.png` (173×208 px)
- Calidad: Insuficiente
- Tipo de referencia: Fotografía de mano real con muñeca flexionada, sin flecha.
- Limitaciones: No se distingue qué dedos intervienen ni la posición del pulgar (auditoría 6.1, clase `insufficient_reference`).

### Pose

- Configuración de dedos: Índice y medio extendidos (separados ±8°); anular y meñique en puño.
- Configuración del pulgar: Abierto (spread −25).
- Aducción: 0.
- Orientación: Mano girada −60° en el eje de flexión de la muñeca; dedos hacia abajo.
- Incertidumbres:
  - Referencia insuficiente: se ven solo dos dedos y no se distingue el pulgar.
  - La pose no se modificó desde la Fase 5; la auditoría 6.1 la clasifica como distinta de la foto (mano girada con abanico de dedos y pulgar).
  - Requiere una vista adicional (frente/lateral) o una descripción (auditoría 6.1, §8).
- Valores del asset (solo lectura): Pulgar: spread -25, flexión 0/0/0; Índice: spread -8, flexión 0/0/0; Medio: spread 8, flexión 0/0/0; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 0. Muñeca `wristEuler` (-60, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Sin hallazgos específicos documentados.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `REFERENCIA_INSUFICIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## Q

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: REFERENCIA_INSUFICIENTE
- Etiqueta en juego: «Pose provisional · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Índice extendido y pulgar abierto, apuntando hacia abajo.»
- Prioridad de validación: Alta

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/Q.png` (166×206 px)
- Calidad: Insuficiente
- Tipo de referencia: Fotografía de mano real vista de canto, sin flecha.
- Limitaciones: Dedos y pulgar juntos en cono/pellizco visto de canto; no se distingue qué dedos intervienen (auditoría 6.1).

### Pose

- Configuración de dedos: Índice extendido; medio, anular y meñique en puño.
- Configuración del pulgar: Abierto (spread −30).
- Aducción: 0.
- Orientación: Mano girada −60° en flexión y 80° de inclinación; apuntando hacia abajo.
- Incertidumbres:
  - Referencia insuficiente: no se distingue qué dedos intervienen ni la posición del pulgar.
  - La pose no se modificó desde la Fase 5; la auditoría 6.1 la clasifica como distinta de la foto (índice horizontal frente a cono/pellizco).
  - Requiere una vista adicional o una descripción (auditoría 6.1, §8).
- Valores del asset (solo lectura): Pulgar: spread -30, flexión 0/0/0; Índice: spread 0, flexión 0/0/0; Medio: spread 0, flexión 85/100/60; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 0. Muñeca `wristEuler` (-60, 0, 80), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Sin hallazgos específicos documentados.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `REFERENCIA_INSUFICIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## R

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Pose provisional · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Índice y medio extendidos y cruzados; los demás dedos cerrados.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/R.png` (163×216 px)
- Calidad: Buena (cruce no se aprecia con detalle)
- Tipo de referencia: Fotografía de mano real, sin flecha.
- Limitaciones: El cruce y qué dedo pasa por delante no se distinguen con precisión.

### Pose

- Configuración de dedos: Índice y medio extendidos y cruzados en las puntas (spread ±8°); anular y meñique en puño.
- Configuración del pulgar: Cruzado por delante.
- Aducción: 1.
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - Hay un solapamiento leve entre dedos (Fase 6.3).
  - Confirmar qué dedo pasa por delante (Fase 6.3).
  - Pose marcada como provisional en el asset.
- Valores del asset (solo lectura): Pulgar: spread 42, flexión 0/0/0, spreadMiddle 40; Índice: spread 8, flexión 0/0/0; Medio: spread -8, flexión 0/0/0; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 1. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Sin hallazgos específicos documentados.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## S

### Estado actual

- Pose: implementada
- Movimiento: sí (provisional)
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Pose provisional · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Puño cerrado con el pulgar cruzado por delante.»
- Prioridad de validación: Alta

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/S.png` (166×207 px)
- Calidad: Discrepante con la pose actual
- Tipo de referencia: Fotografía de mano real con flecha naranja en forma de S.
- Limitaciones: La foto muestra el índice extendido; la pose estática actual es un puño con pulgar cruzado. No da tamaño ni velocidad del trazo.

### Pose

- Configuración de dedos: Puño cerrado (los cuatro dedos con flexión 90/100/65).
- Configuración del pulgar: Cruzado por delante (flexión 55/50/35).
- Aducción: 0.
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - Discrepancia entre la referencia disponible (índice extendido, resto curvado, pulgar cruzado) y la pose actual (puño) que no se corrigió en las Fases 6.3 y 6.4 (movement-audit §2).
  - Movimiento provisional (ver Movimiento).
- Valores del asset (solo lectura): Pulgar: spread 0, flexión 55/50/35; Índice: spread 0, flexión 90/100/65; Medio: spread 0, flexión 90/100/65; Anular: spread 0, flexión 90/100/65; Meñique: spread 0, flexión 90/100/65.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: sí
- Dirección: Traza una S: arriba a la derecha → izquierda por arriba → cruce del centro → derecha por abajo → abajo a la izquierda (sentido de la flecha).
- Trayectoria: Solo traslación respecto de la pose (Y abajo, Z derecha): 0,35 s (−0,040; +0,020) → 0,60 s (−0,050; 0) → 0,85 s (−0,025; −0,022) → 1,10 s (0; 0) → 1,35 s (+0,025; +0,022) → 1,60 s (+0,050; 0) → 1,85 s (+0,040; −0,020) → 2,40 s retorno a la pose.
- Duración: 2,4 s + pausa de 1,1 s.
- Ciclos: 1 trazo por ciclo de bucle.
- Parámetros provisionales: Tamaño (≈10 cm de alto), tiempos, aproximación de la S con 8 puntos y suavizado, retorno a la base. Todos los valores numéricos son provisionales.
- Limitaciones de la referencia: La referencia da la forma y el sentido; no da tamaño ni velocidad, ni si la mano traza la S o solo el dedo/muñeca.
- Pendiente de validación LSC (movimiento).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Sin hallazgos específicos documentados.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: pendiente
- Orientación: pendiente
- Observaciones:

---

## T

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Pose provisional · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Índice y medio extendidos y juntos, meñique extendido aparte, anular cerrado bajo el pulgar. (Identidad del dedo corto sin confirmar.)»
- Prioridad de validación: Alta

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/T.png` (167×208 px)
- Calidad: Limitada (identidad del dedo corto sin confirmar)
- Tipo de referencia: Fotografía de mano real, sin flecha.
- Limitaciones: Tres dedos extendidos (dos altos juntos y uno corto a la izquierda); no permite identificar con certeza cuáles son.

### Pose

- Configuración de dedos: Índice y medio extendidos y juntos; meñique extendido aparte; anular cerrado.
- Configuración del pulgar: Cruzado por delante, sobre el dedo cerrado.
- Aducción: Aducción individual 1 en el índice.
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - Identidad del dedo corto sin confirmar. Lectura adoptada (no confirmada): índice + medio juntos, anular cerrado bajo el pulgar y meñique extendido aparte.
  - Alternativa posible: medio + anular altos, índice cerrado y meñique extendido (Fase 6.3).
  - Pose marcada como provisional en el asset.
- Valores del asset (solo lectura): Pulgar: spread 42, flexión 0/0/0, spreadMiddle 40; Índice: spread 0, flexión 0/0/0, aducción individual 1; Medio: spread 0, flexión 0/0/0; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 0/0/0.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Sin hallazgos específicos documentados.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## U

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Referencia aproximada · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Índice y meñique extendidos; medio y anular cerrados bajo el pulgar.»
- Prioridad de validación: Baja

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/U.png` (170×207 px)
- Calidad: Buena
- Tipo de referencia: Fotografía de mano real, sin flecha.
- Limitaciones: Resolución baja.

### Pose

- Configuración de dedos: Índice y meñique extendidos; medio y anular cerrados.
- Configuración del pulgar: Cruzado por delante, sobre los dedos cerrados.
- Aducción: 0.
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - Coincidencia visual buena según la Fase 6.3; solo falta validación lingüística.
  - Fase 6.1: la pose anterior tenía dedos equivocados (corregida en 6.3).
- Valores del asset (solo lectura): Pulgar: spread 42, flexión 0/0/0, spreadMiddle 40; Índice: spread 0, flexión 0/0/0; Medio: spread 0, flexión 85/100/60; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 0/0/0.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Sin hallazgos específicos documentados.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## V

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Referencia aproximada · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Índice y medio extendidos y separados en V.»
- Prioridad de validación: Baja

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/V.png` (167×204 px)
- Calidad: Buena
- Tipo de referencia: Fotografía de mano real, sin flecha.
- Limitaciones: Resolución baja.

### Pose

- Configuración de dedos: Índice y medio extendidos y separados en V (−14°/+10°); anular y meñique en puño.
- Configuración del pulgar: Flexionado sobre los dedos anular y meñique.
- Aducción: 0.
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - Auditoría 6.1: clase `correct` con ángulo de separación similar; sin cambios posteriores. Solo falta validación lingüística.
- Valores del asset (solo lectura): Pulgar: spread 0, flexión 45/40/25; Índice: spread -14, flexión 0/0/0; Medio: spread 10, flexión 0/0/0; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Sin hallazgos específicos documentados.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## W

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Referencia aproximada · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Índice, medio y anular extendidos y separados; el pulgar sujeta el meñique.»
- Prioridad de validación: Baja

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/W.png` (165×202 px)
- Calidad: Buena
- Tipo de referencia: Fotografía de mano real, sin flecha.
- Limitaciones: Resolución baja.

### Pose

- Configuración de dedos: Índice, medio y anular extendidos con separación leve (−16°/0°/+16°); meñique en puño.
- Configuración del pulgar: Flexionado sobre el meñique.
- Aducción: 0.
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - Auditoría 6.1: clase `correct`; indica reducir algo la separación (diferencia menor); no se modificó. Solo falta validación lingüística.
- Valores del asset (solo lectura): Pulgar: spread 0, flexión 45/40/25; Índice: spread -16, flexión 0/0/0; Medio: spread 0, flexión 0/0/0; Anular: spread 16, flexión 0/0/0; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Sin hallazgos específicos documentados.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## X

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Referencia aproximada · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Índice en gancho; los demás dedos cerrados y el pulgar cruzado.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/X.png` (169×203 px)
- Calidad: Buena (foto de perfil)
- Tipo de referencia: Fotografía de mano real tomada de perfil, sin flecha.
- Limitaciones: Al estar de perfil, el gancho no se aprecia de frente.

### Pose

- Configuración de dedos: Índice en gancho (flexión 15/95/30); los demás en puño.
- Configuración del pulgar: Cruzado por delante.
- Aducción: 0.
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - Coincidencia visual razonable (la foto es de perfil) (Fase 6.3).
  - De frente no se aprecia el gancho; se ve mejor en PERFIL.
- Valores del asset (solo lectura): Pulgar: spread 42, flexión 0/0/0, spreadMiddle 40; Índice: spread 0, flexión 15/95/30; Medio: spread 0, flexión 85/100/60; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: El gancho se aprecia poco.
- PERFIL: Beneficia: se ve el gancho (camera-visualization-report §3 y §6).
- DORSO: Aporta menos que el perfil.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## Y

### Estado actual

- Pose: implementada
- Movimiento: no
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Referencia aproximada · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Pulgar y meñique extendidos; los demás dedos cerrados.»
- Prioridad de validación: Baja

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/Y.png` (166×202 px)
- Calidad: Buena
- Tipo de referencia: Fotografía de mano real, sin flecha.
- Limitaciones: Resolución baja.

### Pose

- Configuración de dedos: Meñique extendido; índice, medio y anular en puño.
- Configuración del pulgar: Extendido (spread −40).
- Aducción: 0.
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - Auditoría 6.1: clase `correct` con diferencia mínima; sin cambios posteriores. Solo falta validación lingüística.
- Valores del asset (solo lectura): Pulgar: spread -40, flexión 0/0/0; Índice: spread 0, flexión 85/100/60; Medio: spread 0, flexión 85/100/60; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 0/0/0.
- Aducción de la mano: 0. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: no (la referencia no muestra flecha ni indicio de movimiento; no aplica).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Sin hallazgos específicos documentados.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: no aplica
- Orientación: pendiente
- Observaciones:

---

## Z

### Estado actual

- Pose: implementada
- Movimiento: sí (provisional)
- Validación LSC: PENDIENTE
- Etiqueta en juego: «Pose provisional · pendiente de validación»
- Texto de instrucción mostrado en el juego: «Índice y medio extendidos y juntos trazando una Z en el aire.»
- Prioridad de validación: Media

### Referencia

- Archivo de referencia: `Assets/Project/Reference/LSC/Alphabet/Z.png` (177×210 px)
- Calidad: Limitada (tamaño, velocidad y mano que traza no determinables)
- Tipo de referencia: Fotografía de mano real con el glifo Z en naranja y flecha.
- Limitaciones: Muestra la forma y el sentido; no da tamaño, velocidad ni qué parte de la mano traza.

### Pose

- Configuración de dedos: Índice y medio extendidos y juntos; anular y meñique en puño.
- Configuración del pulgar: Cruzado por delante.
- Aducción: 1,15.
- Orientación: Palma hacia la cámara, sin giro de muñeca.
- Incertidumbres:
  - Movimiento provisional (ver Movimiento).
  - Fase 6.1: la pose anterior extendía solo el índice (corregida en 6.3).
- Valores del asset (solo lectura): Pulgar: spread 42, flexión 0/0/0, spreadMiddle 40; Índice: spread 0, flexión 0/0/0; Medio: spread 0, flexión 0/0/0; Anular: spread 0, flexión 85/100/60; Meñique: spread 0, flexión 85/100/60.
- Aducción de la mano: 1.15. Muñeca `wristEuler` (0, 0, 0), `wristOffset` (0, 0, 0).

### Movimiento

- Existe movimiento: sí
- Dirección: Barra superior hacia la derecha, diagonal hacia abajo-izquierda, barra inferior hacia la derecha (como el glifo y la flecha).
- Trayectoria: Respecto de la pose (Y abajo, Z derecha): 0,35 s (−0,05; −0,05) → 0,85 s (−0,05; +0,05) → 1,45 s (+0,05; −0,05) → 1,95 s (+0,05; +0,05) → 2,55 s retorno a la pose. Inicio y retorno añadidos en la Fase 6.6-B solo para cerrar el bucle (0,35 s de aproximación y 0,6 s de retorno).
- Duración: 2,55 s + pausa de 1,1 s.
- Ciclos: 1 trazo por ciclo de bucle.
- Parámetros provisionales: Amplitud (≈10 × 10 cm), tiempos, mano que traza, tiempos de aproximación y retorno. Todos los valores numéricos son provisionales.
- Limitaciones de la referencia: La referencia da la forma y el sentido; no da tamaño, velocidad ni qué parte de la mano traza.
- Pendiente de validación LSC (movimiento).

### Perspectivas

- FRENTE: Sin hallazgos específicos documentados; se entiende bien de frente (camera-visualization-report §6).
- PERFIL: Sin hallazgos específicos documentados.
- DORSO: Sin hallazgos específicos documentados.

### Validación LSC

- Estado: `PENDIENTE`
- Validador:
- Fecha:
- Pose: pendiente
- Movimiento: pendiente
- Orientación: pendiente
- Observaciones:

---

## Información adicional que sigue faltando (de las auditorías previas)

- **P y Q:** vista adicional (frente/lateral) o descripción de qué dedos y qué posición del pulgar.
- **T:** confirmar qué dedo es el corto de la izquierda.
- **K, M, N, E, O:** confirmar posición/contacto del pulgar y dedos ocultos.
- **F / I:** confirmar la diferencia exacta de posición del pulgar.
- **R:** confirmar qué dedo pasa por delante.
- **G, H, J, Ñ, S, Z:** amplitud, velocidad, repeticiones y qué parte de la mano se mueve (descripción de una persona experta o vídeo).
- **S:** la foto muestra el índice extendido; la pose estática actual es un puño.
- **H, M, N, Ñ:** el antebrazo y la flexión de muñeca de las fotos no están reproducidos.
- **Validación lingüística final de las 27 letras** con una persona usuaria de LSC.
