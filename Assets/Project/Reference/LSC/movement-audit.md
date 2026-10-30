# Auditoría e implementación de movimientos LSC (Fase 6.6)

Letras: **G, H, J, Ñ, S, Z** · Rama `feature/one-level` · Datos: `Assets/Project/Data/LscAlphabet.asset` · Referencias: `Assets/Project/Reference/LSC/Alphabet/`

> **Estado de validación:** ningún movimiento está validado por una persona usuaria de LSC. Una imagen fija con una flecha no permite conocer amplitud, velocidad ni número de repeticiones; todo lo que aparece como "provisional" es una aproximación técnica conservadora, **no una conclusión lingüística**.

## 1. Cómo se representan los movimientos (sin sistema nuevo)

El sistema ya existía y **no se modificó ningún script**:

- `LetterEntry.motion` (`PoseKey[]`, en `LscAlphabet.cs`/`HandPose.cs`): lista de fotogramas `{time, pose}`. Cada fotograma es una `HandPose` completa (dedos + `wristEuler` + `wristOffset`).
- `HandPoseDriver.EvaluateMotion`: interpola entre fotogramas con `SmoothStep`, mantiene el último fotograma `loopPauseSeconds` (1,1 s) y repite en bucle. Al cambiar de letra mezcla 0,35 s hacia `entry.pose`.
- La trayectoria se expresa **solo con `wristOffset`** (metros, espacio del padre: X = dorso/profundidad, Y = abajo, Z = lado del pulgar/derecha de pantalla) y **`wristEuler`** (grados). Los dedos de cada fotograma se copian de la pose estática, de modo que el movimiento no altera la configuración manual.
- La cámara (Fase 6.5) solo mueve la cámara, así que el movimiento se ve igual en FRENTE, PERFIL y DORSO.

**Separación pose / movimiento:** `entry.pose` (estática) no se tocó en ninguna de las 27 letras. En G, H y S solo se pasó `motion: []` → fotogramas nuevos (el primero y el último son idénticos a la pose estática, de modo que no hay salto al entrar ni al reiniciar el bucle). J y Z recibieron en la Fase 6.6-B un key inicial y uno final de retorno; Ñ no se modificó.

## 2. Lo que la referencia permite afirmar (flechas de las fotos)

| Letra | Qué se ve en la foto |
|---|---|
| G | Flecha curva a la izquierda de la punta del índice: sale hacia la izquierda y vuelve en curva por debajo. |
| H | Flecha curva sobre los dedos: arco que avanza hacia la derecha (hacia donde apuntan los dedos) y termina descendiendo. |
| J | Flecha en gancho bajo el meñique: baja y se curva hacia arriba/derecha. |
| Ñ | Flecha doble horizontal bajo las puntas (oscilación lateral). |
| S | Trazo en forma de S con la punta de flecha abajo a la izquierda. |
| Z | Glifo Z con flecha hacia la derecha en la barra superior. |

Notas de las referencias: la foto de S muestra el índice extendido, pero la pose estática actual de S es un puño con pulgar cruzado (no se corrigió en 6.3/6.4 y la fase 6.6 exige conservarla); esa discrepancia sigue **pendiente** de una fase de poses.

## 3. Letra por letra

Convención de signos de las tablas: `wristOffset` (Y+ = abajo, Z+ = derecha de pantalla), `wristEuler` Z+ = inclina los dedos hacia el lado cubital (izquierda de pantalla con palma a cámara). Offsets/ángulos de los fotogramas son **relativos a la pose estática** de la letra.

### G — implementado (nuevo)
- **Pose utilizada:** la actual (índice F(0,0,20,25), resto puño, pulgar `Over`, `wristEuler` (0,0,60)); sin cambios.
- **Movimiento:** el índice (mano de lado) se desplaza hacia la izquierda y vuelve describiendo un arco corto por debajo, como la flecha curva.
- **Dirección:** hacia el lado del índice (izquierda de pantalla) y retorno.
- **Trayectoria (fotogramas):** t 0 base → 0,45 s: giro Z +15°, offset (Y 0, Z −0,02) → 0,90 s: giro Z +8°, offset (Y +0,025, Z −0,01) → 1,35 s: base.
- **Duración / ciclos:** 1,35 s + pausa de 1,1 s del driver (ciclo 2,45 s), 1 ciclo por repetición del bucle.
- **Medido:** recorrido de la punta del índice 5,2 × 4,7 cm; paso máximo 0,38 cm/fotograma a 60 fps; sin salto de entrada ni de reinicio.
- **Provisional:** todos los valores numéricos (amplitud 15°/2,5 cm, tiempos, un ciclo, la combinación giro + desplazamiento).
- **Respaldado por la referencia:** que existe un movimiento curvo hacia la izquierda con retorno.
- **Requiere validación LSC:** si el movimiento es de muñeca o de antebrazo, amplitud real, velocidad, repeticiones y sentido exacto de la curva.

### H — implementado (nuevo)
- **Pose utilizada:** la actual (índice+medio juntos y extendidos, resto puño, `wristEuler` (0,180,90), `wristOffset` (0,0,−0,16)); sin cambios.
- **Movimiento:** arco de traslación de la mano hacia donde apuntan los dedos (derecha), subiendo ligeramente al inicio y descendiendo al final.
- **Dirección:** izquierda → derecha, terminando hacia abajo.
- **Trayectoria:** 0 base → 0,5 s: (Y −0,02, Z +0,03) → 1,0 s: (Y 0, Z +0,06) → 1,5 s: (Y +0,03, Z +0,08) → 2,0 s: base.
- **Duración / ciclos:** 2,0 s + pausa 1,1 s (ciclo 3,1 s); 1 ciclo.
- **Medido:** recorrido de la punta 8,0 × 5,0 cm; paso máximo 0,43 cm/fotograma; sin saltos de entrada ni reinicio; la mano permanece dentro del encuadre en las tres vistas.
- **Provisional:** amplitud (8 cm), tiempos, altura del arco.
- **Respaldado por la referencia:** arco hacia la derecha y abajo.
- **No implementado a propósito:** componente de flexión/inclinación de la muñeca que sugiere la flecha. La pose usa una composición de ejes (Y 180°, Z 90°) en la que un delta de muñeca no se puede razonar sin inspección visual de signos; se usó solo traslación (espacio del padre, sin ambigüedad de ejes).
- **Requiere validación LSC:** si el gesto implica giro de muñeca, amplitud, velocidad, repeticiones.

### J — revisado; trayectoria conservada, bucle cerrado (Fase 6.6-B)
- **Pose utilizada:** la actual (meñique extendido, puño, pulgar curvado 45/40/25); sin cambios.
- **Trayectoria original (conservada):** 0 s (Y −0,04, Z 0,01) → 0,7 s (Y +0,05, Z 0,01) → 1,2 s (Y +0,07, Z −0,04, giro Z 40°). Baja y termina en gancho hacia la izquierda. Coincide en lo esencial con la foto ("baja y se curva"); el lado del gancho depende de la vista de la foto y la imagen no lo resuelve, así que no se cambió su forma.
- **Salto anterior:** 4,1 cm al entrar y 16,8 cm al reiniciar el bucle.
- **Causa:** (1) el primer key tenía `wristOffset` (0, −0,04, 0,01) y la pose estática (0, 0, 0), así que al terminar la mezcla de entrada la mano saltaba al primer key; (2) el último key (0,07, −0,04, giro 40°) estaba lejos del primero, y tras la pausa del driver `EvaluateMotion` vuelve al key 0 de golpe.
- **Solución (solo datos `motion`):** se añadió un key inicial idéntico a la pose estática (t = 0), se desplazaron +0,3 s los tiempos originales sin tocar sus offsets ni giros y se añadió un key final igual a la pose estática (retorno de 0,6 s). Nuevos tiempos: 0 / 0,3 / 1,0 / 1,5 / 2,1 s. No se tocó `EvaluateMotion` ni `HandPoseDriver`.
- **Salto posterior (medido, 60 fps simulados con el driver):** entrada 0,00 cm, reinicio 0,00 cm (offset 0 cm, giro 0°). Paso máximo 0,65 cm/fotograma (39 cm/s, igual que antes).
- **Costo:** el trazo dura 2,1 s en vez de 1,2 s (0,3 s de aproximación + 0,6 s de retorno); la pausa del bucle sigue siendo la del driver (1,1 s).
- **Provisional:** toda la trayectoria y los tiempos de aproximación/retorno. **Requiere validación LSC.**

### Ñ — revisado, **conservado** (sin cambios de datos)
- **Pose utilizada:** la de N (índice y medio hacia abajo, `wristEuler` (0,0,180), `wristOffset` (0,−0,3,0)); sin cambios. No se convirtió en copia estática de N: conserva su `motion`.
- **Movimiento existente:** balanceo de la muñeca en el plano de la palma: Z 180° → 190° (0,35 s) → 174° (0,7 s) → 180° (1,0 s). Corresponde a la flecha doble horizontal (oscilación lateral de las puntas); no contradice la referencia. Empieza y termina en la pose estática: saltos de entrada y reinicio 0,00 cm.
- **Encuadre (Fase 6.6-B):** el borde superior se superaba en PERFIL/DORSO (y máx. 1,04–1,06) por la distancia reducida de esas vistas (×0,75). **Corregido solo con la cámara** (ver "Encuadre de las vistas laterales"): con ×1,0 el máximo es 0,90–0,91 en las tres vistas. Trayectoria y pose sin tocar.
- **Provisional:** amplitud (+10° / −6°, asimétrica), duración, número de oscilaciones. **Requiere validación LSC.**

### S — implementado (nuevo)
- **Pose utilizada:** la actual (puño con pulgar cruzado); sin cambios (ver la discrepancia de la sección 2).
- **Movimiento:** la mano traza una S en el aire, empezando arriba a la derecha, curvando por arriba hacia la izquierda, cruzando el centro y terminando abajo a la izquierda (sentido de la flecha de la foto). Solo traslación; sin giro de muñeca.
- **Trayectoria** (desplazamientos Y abajo+, Z derecha+, relativos a la pose; la mano parte y regresa a la posición base):
  0 s (0,0) → 0,35 s (−0,040, +0,020) → 0,60 s (−0,050, 0) → 0,85 s (−0,025, −0,022) → 1,10 s (0, 0) → 1,35 s (+0,025, +0,022) → 1,60 s (+0,050, 0) → 1,85 s (+0,040, −0,020) → 2,40 s (0,0).
- **Duración / ciclos:** 2,4 s + pausa 1,1 s (ciclo 3,5 s); 1 trazo por ciclo. El último tramo (0,55 s) devuelve la mano a la posición base para que el bucle no tenga salto.
- **Medido:** recorrido 4,4 cm (horizontal) × 10,0 cm (vertical); paso máximo 0,33 cm/fotograma; 0 cm de salto de entrada y reinicio; dentro del encuadre en las tres vistas.
- **Provisional:** tamaño (~10 cm de alto, similar al Z existente), tiempos, aproximación de la S con 8 puntos y suavizado `SmoothStep` por tramo, retorno a la base.
- **Respaldado por la referencia:** forma S y sentido de la flecha.
- **Requiere validación LSC:** tamaño, velocidad, si la mano traza la S o solo el dedo/muñeca, repeticiones. Además la pose estática de S (puño) difiere de la foto (índice extendido).

### Z — revisado; trayectoria conservada, bucle cerrado y encuadre corregido (Fase 6.6-B)
- **Pose utilizada:** la corregida en 6.3 (índice y medio extendidos y juntos, aducción 1,15, anular/meñique en puño, pulgar `Over`); sin cambios. Sus keys usan la configuración de dedos nueva (desviación de dedos 0° durante todo el movimiento).
- **Trayectoria original (conservada):** (Y −0,05, Z −0,05) → (−0,05, +0,05) → (+0,05, −0,05) → (+0,05, +0,05): barra superior hacia la derecha, diagonal hacia abajo-izquierda, barra inferior hacia la derecha. Coincide con el glifo y la flecha de la referencia.
- **Salto anterior:** 7,1 cm al entrar y 14,1 cm al reiniciar.
- **Causa:** la misma que en J: el key 0 (−0,05, −0,05) no coincidía con la pose estática (offset 0) y el último key (+0,05, +0,05) queda en la esquina opuesta; tras la pausa el driver salta al key 0.
- **Solución (solo datos `motion`):** key inicial = pose estática (t = 0), tiempos originales desplazados +0,35 s (offsets y giros idénticos) y key final = pose estática (retorno de 0,6 s). Nuevos tiempos: 0 / 0,35 / 0,85 / 1,45 / 1,95 / 2,55 s.
- **Salto posterior (medido):** entrada 0,00 cm, reinicio 0,00 cm; paso máximo 0,59 cm/fotograma (35 cm/s, igual que antes); recorrido de la punta 10 × 10 cm (igual).
- **Encuadre anterior:** en PERFIL (y máx. 1,075) y DORSO (1,01) la mano superaba el borde superior durante la barra superior.
- **Solución de encuadre (solo cámara):** `sideViewDistanceScale` de la escena `AlphabetHouse` pasó de 0,75 a **1,0** (las vistas laterales usan la misma distancia que FRENTE). El valor está serializado en la escena; no se cambió código. No se modificó la trayectoria de Z.
- **Resultado:** Z dentro del encuadre en las tres vistas (PERFIL y máx. 0,92; DORSO 0,88; FRENTE 0,88).
- **Provisional:** amplitud, tiempos (aproximación y retorno incluidos), mano que traza. **Requiere validación LSC.**

### Encuadre de las vistas laterales (Fase 6.6-B)
Sondeo (27 letras, pose estática o ciclo completo de movimiento, malla horneada, vistas laterales) de la coordenada vertical máxima de la mano en el viewport (1,0 = borde superior):

| `sideViewDistanceScale` | PERFIL (máx. y, letra) | DORSO (máx. y, letra) |
|---|---|---|
| 0,75 (anterior) | 1,075 (Z) | 1,040 (Ñ) |
| 0,90 | 0,968 (Z) | 0,948 (Ñ) |
| **1,00 (elegido)** | **0,917 (Z)** | **0,902 (Ñ)** |
| 1,10 | 0,876 (Z) | 0,864 (Ñ) |

Se eligió 1,0 por dejar ≥8 % de margen con todas las letras y por ser coherente con FRENTE. Contrapartida: en PERFIL y DORSO la mano se ve más pequeña que antes (misma distancia que en FRENTE). El panel `FondoVista` sigue cubriendo la habitación (revisado en las capturas de Z y Ñ).

## 4. Tabla resumen

| Letra | Pose | Movimiento | Parámetros provisionales | Validación LSC |
|---|---|---|---|---|
| G | Implementada | Implementado | Sí | Pendiente |
| H | Implementada | Implementado | Sí | Pendiente |
| J | Implementada | Revisado | Sí | Pendiente |
| Ñ | Implementada | Revisado | Sí | Pendiente |
| S | Implementada | Implementado | Sí | Pendiente |
| Z | Implementada | Revisado | Sí | Pendiente |

## 5. Pruebas de Fase 6.6 (primera pasada, simuladas)

La primera pasada evaluó el movimiento con `EvaluateMotion`/`Apply` del driver a 60 fps simulados. Sus resultados de J y Z (saltos y encuadre) quedaron superados por la Fase 6.6-B (arriba).

## Pruebas finales de Fase 6.6

Unity 6000.0.34f1, Play Mode, escena `AlphabetHouse` (sin guardar). Game View fijada a cada resolución con la API del editor. **Entrada física del sistema operativo** (Win32 `keybd_event`/`mouse_event` con la ventana del editor en primer plano; el ratón se calibró contra `Mouse.current.position` y los clics se hicieron sobre el centro real de cada botón). Los fotogramas son los reales del bucle de Play Mode (no simulados). Las letras se seleccionaron con PageDown real.

**Interacción (entrada física)**

| Resolución | Flecha derecha | PageDown | Botón SIGUIENTE (ratón) | Tecla V | Botón VISTA (ratón) | Esc |
|---|---|---|---|---|---|---|
| 1920×1080 | OK (0→1) | OK (1→2) | OK (2→3) | OK (FRENTE→PERFIL) | OK (PERFIL→DORSO) | OK (cierra; vista = FRENTE) |
| 1280×720 | OK (0→1) | OK | OK | OK | OK | OK |

Nota: en una pasada intermedia a 1280×720 la prueba empezó en Z (última letra) y la flecha derecha pasó a la letra 22 por la navegación de la rejilla; no es un fallo de la función y se repitió empezando en A (resultado en la tabla). Un primer intento de teclado falló porque `keybd_event` se enviaba sin código de escaneo; era un defecto del arnés de prueba, no del juego.

**Estabilidad (300 fotogramas reales por celda; G y H en PERFIL/DORSO, 120)**

Todas las celdas, a 1920×1080 y 1280×720: **0 errores de consola**, desviación de dedos 0,000°, mano dentro del encuadre y velocidad máxima de la punta igual a la esperada del trayecto (G 23, H 26, J 39, Ñ 25, S 20, Z 35 cm/s), es decir, sin saltos.

| Letra | FRENTE (y máx.) | PERFIL (y máx.) | DORSO (y máx.) | Observaciones |
|---|---|---|---|---|
| G | 0,65 | 0,62 | 0,64 | Estable |
| H | 0,49 | 0,49 | 0,49 | Estable |
| J | 0,80 | 0,78 | 0,78 | Sin saltos de entrada ni de reinicio |
| Ñ | 0,90 | 0,91 | 0,90 | Movimiento sin cambios; ya no roza el borde |
| S | 0,71 | 0,71 | 0,71 | Estable |
| Z | 0,88 | 0,92 | 0,88 | Sin saltos; dentro del encuadre en las tres vistas |

Observaciones y limitaciones de la prueba:
- Los fotogramas reales corrieron a ~50–100 fps; 300 fotogramas equivalen a 3–7 s, es decir, ≥1 ciclo de movimiento en casi todas las celdas (ciclos: G 2,45 s, H 3,1 s, J 3,2 s, Ñ 2,1 s, S 3,5 s, Z 3,65 s); en Z DORSO a 1280×720 el ciclo completo queda justo. Los saltos de reinicio se verificaron además con el análisis simulado (0,00 cm).
- El contador de "reinicios" del arnés no era fiable (el driver no reinicia `_motionTime`) y no se usa como evidencia.
- Capturas de Game View con interfaz de J, Ñ y Z en las tres vistas y ambas resoluciones; revisada por muestreo la de Z en PERFIL a 1920×1080 (mano completa dentro del encuadre).
- No se probó con otro hardware de entrada.

**Poses:** comparación de `LscAlphabet.asset` antes/después de la Fase 6.6-B y contra la copia previa a la Fase 6.6: las 27 entradas (letra, texto, validación y `pose`) son idénticas; solo difieren los `motion` de G, H y S (Fase 6.6) y de J y Z (Fase 6.6-B). Los scripts (`HandPose.cs`, `HandPoseDriver.cs`, `AlphabetLevelController.cs`) no se tocaron en esta fase; en la escena solo cambió `sideViewDistanceScale`.

> Los movimientos continúan siendo provisionales y requieren validación por una persona competente en LSC.

## 6. Pendiente

- Validación lingüística de las 27 letras y de todos los movimientos (persona usuaria de LSC).
- H: posible componente de inclinación de muñeca; verificarlo con una persona experta.
- S: la foto muestra el índice extendido; la pose estática actual es un puño.
- Antebrazo (Fase 6.5) sigue sin implementarse.
- Vistas laterales más pequeñas que antes (distancia ×1,0); si se desea la mano más grande, habría que reencuadrar por letra.
- J y Z duran más (2,1 s y 2,55 s) por los tramos de aproximación y retorno; si se consideran lentos, pueden acortarse (solo datos de `motion`).
