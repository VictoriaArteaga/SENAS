# MediaPipe hand capture

This module uses MediaPipe Unity Plugin v0.16.3 for Windows x64 CPU inference.

## Setup

Attach `Senas.AI.MediaPipe.HandLandmarkerCapture` to a dedicated GameObject owned by the AI module. The component starts the first available webcam when enabled and loads `MediaPipe/hand_landmarker.bytes` from `StreamingAssets`.

Subscribe to `LandmarksUpdated` to receive snapshots on Unity's main thread. Each snapshot contains zero to two hands, normalized image landmarks, world landmarks, handedness, and a monotonic timestamp in milliseconds. This module only captures landmarks; it does not classify signs or write gameplay state.

The bundled Windows native library is x64 and CPU-only. Windows support for the plugin is experimental; the plugin's native code can terminate the Unity Editor on fatal errors.

## Evaluacion inicial del alfabeto

`AlphabetLandmarkEvaluator` consume `LandmarksUpdated` y compara cada mano con referencias estaticas configuradas en un asset `AlphabetLandmarkTemplateSet`. Normaliza las 21 coordenadas por la muneca y la distancia entre la muneca y el MCP del dedo medio; pide varios fotogramas consecutivos antes de informar `Recognized`. El resultado tambien entrega `Similarity`, que es una proximidad geometrica, no una probabilidad calibrada.

La escena aislada `Assets/IA/Test_Webcam_MediaPipe.unity` ya tiene `AlphabetLandmarkEvaluator` conectado a `MediaPipeCapture` y referencia `AlphabetLandmarkTemplates.asset`. El asset contiene muestras estaticas capturadas para 21 letras; esto no significa que las 27 letras esten cubiertas ni que las muestras esten validadas. El umbral inicial de distancia es orientativo y requiere calibracion con muestras reales. La similitud mostrada es proximidad geometrica, no una probabilidad ni una medida de precision validada.

Las letras dinamicas usan secuencias separadas de las plantillas de postura. El comparador temporal aplica DTW sobre landmarks normalizados y trayectoria de muñeca, con una ventana limitada y una comprobacion minima de movimiento. Los umbrales son iniciales; aun requieren calibracion y validacion con muestras reales.

## Captura y evaluacion temporal

No reutilizar `AddStaticTemplate` para una seña dinamica: promediar sus fotogramas elimina el trazo que la distingue. Mantener las referencias temporales separadas de las estaticas para no cambiar el formato ni el comportamiento de las muestras ya capturadas.

### Lista de letras

La referencia LSC validada por el equipo confirma como dinamicas **G, H, J, Ñ, S y Z**. No inferir la lista a partir de alfabetos de otros paises ni fijarla en codigo. Registrar en el asset esta lista y la fuente/version de referencia.

### Captura de secuencias

- En Play Mode, abre `Window > SENAS > LSC Motion Capture`, selecciona letra, mano y umbral; inicia y ejecuta el movimiento completo. La ventana guarda al detectar 12 fotogramas de quietud tras el movimiento, o puedes finalizar manualmente.
- Se guardan landmarks normalizados por muñeca y escala de palma, trayectoria de muñeca y tiempo relativo. No se guarda webcam ni imagenes.
- La captura exige al menos 6 fotogramas durante 0,15 s y se cancela si se pierde el tracking por mas de 250 ms. Cada ejecucion se conserva como muestra independiente.
- Captura varias ejecuciones por letra, incluyendo variaciones de velocidad y mano dominante. 90 fotogramas es solo el maximo de seguridad, no la longitud requerida.

### Evaluacion temporal

- El evaluador conserva hasta 90 observaciones, reinicia ante una perdida de tracking larga y compara ventanas de duracion variable mediante DTW con banda limitada.
- La distancia combina forma de mano y trayectoria de muñeca; se requiere evidencia minima de movimiento antes de aceptar una coincidencia.
- `Similarity` sigue siendo proximidad geometrica, no probabilidad ni medida de precision. No conectar al gameplay hasta evaluar falsos positivos y negativos con distintas personas y velocidades.

La lista dinamica ya fue confirmada por el equipo contra la referencia aprobada: G, H, J, Ñ, S y Z. El asset conserva las muestras estaticas de G como referencia de postura y actualmente incluye 14 secuencias temporales: G (3), H (1), J (2), Ñ (3), S (3) y Z (2). Captura mas variantes antes de calibrar el umbral para evitar sobreajuste a una sola persona o velocidad.

## Capturar referencias estaticas

1. Abre la escena `Assets/IA/Test_Webcam_MediaPipe.unity` y entra en Play Mode.
2. Abre `Window > SENAS > LSC Template Capture`; la ventana carga `AlphabetLandmarkTemplates.asset` automaticamente.
3. Selecciona la letra y la mano para la muestra. Sigue la demostracion aprobada del juego fuera de esta herramienta.
4. Con la postura estatica visible en la webcam, pulsa `Capturar muestra estatica` y mantenla hasta completar el promedio.
5. Repite capturas en condiciones variadas antes de calibrar el umbral. No uses la salida como evaluacion de gameplay hasta validar falsos positivos y negativos.

El asset guarda solo los puntos promediados. El evaluador ya esta conectado en la escena de prueba y otros componentes podrian suscribirse a `EvaluationUpdated`; actualmente no hay conexion a gameplay ni retroalimentacion didactica.

Referencia de las plantillas: video del alfabeto LSC validado por el equipo: https://www.youtube.com/watch?v=7SmEeoNeZXY.
