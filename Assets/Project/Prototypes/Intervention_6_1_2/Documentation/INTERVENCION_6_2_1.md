# Intervención 6.2.1 — Cuatro aldeas y río natural con cuatro puentes

Escena: `Scenes/Prototype_Modular_FourVillages.unity` (nueva; fuera de Build Settings). Las escenas `Prototype_Modular`, `Prototype_Modular_RiverCentral` y `Prototype_Terrain` no se tocaron.
Reconstruir: menú *Lumina ▸ Prototipo 6.2.1 ▸ Construir cuatro aldeas* (`Editor/R621Builder.cs`). Verificar: *Verificar y capturar* (`R621Verify.cs`). Play Mode automatizado: `R621PlayTest.Run`.

> Las pruebas de Play Mode usan **teclado y ratón inyectados en el Input System**, no periféricos físicos. La prueba con teclado y ratón físicos es la del final de este documento y la hace la usuaria.

## 1. Decisiones de diseño
- **Mapa** de 90 × 90 m (±45), modular (mallas), sin Unity Terrain. La altura sale de una sola función (`R621Layout.Height`) con relieve suave, lomas de prueba en dos esquinas y zonas planas bajo plazas, casas, caminos y cauce.
- **Río natural**: 19 puntos de control suavizados (Chaikin ×3) de norte a sur por el centro; anchura variable (multiplicador 0,78–1,22); orillas de arena con borde irregular (±0,9 m). Perfil: lecho a −1,2 m, agua a −0,35 m, orilla suave hasta 3,5·W m del eje. Barrera invisible a 1,75·W + 0,5 m del eje con huecos solo del ancho del tablero en los puentes.
- **Cuatro aldeas** (casas conservadas, sin tocar fachadas): NO Abecedario, NE Meses, SO Números, SE Saludos. Cada una tiene: casa, camino de llegada recto de 7 m perpendicular a la fachada hasta la **puerta real** del prefab, plaza temática reservada (disco, r = 4,2 m), 3 reservas de monumentos (anillos en el suelo, r ≈ 1,15–1,65 m, sin objetos ni colliders) y un marcador provisional.
- **Plaza común** en la orilla oeste (no es una quinta aldea: sin casa ni reservas). Los cuatro caminos principales parten de ella o de sus puentes.
- **Cuatro puentes** con el `Bridge.prefab` original (collider original, sin modificar), cada uno con su tono de madera; ubicados donde el cauce es más claro y perpendiculares al cauce local.
- **Vegetación**: se conservó lo existente (62 árboles, 20 rocas, 50 flores) apartado de reservas, caminos, puertas, puentes y plazas, con colliders simples (cápsulas/cajas). La expansión paisajística es de 6.2.3.
- **Señalización provisional** (13 postes, TextMeshPro, paleta Cream/Ink/Turquoise/Coral/Sand): 4 carteles de casa, 1 poste de orientación de la plaza («NO · ABECEDARIO / NE · MESES / SO · NÚMEROS / SE · SALUDOS»), 4 carteles de puente y 4 marcadores de aldea con su reserva temática (LETRAS, CIFRAS, RELOJ Y CALENDARIO, MANOS).

## 2. Coordenadas finales (X, Z; norte = +Z)
| Elemento | Posición |
|---|---|
| Casas (centro de huella; sin cambios) | Abecedario (−22, 14) · Números (−22, −14) · Meses (22, 14) · Saludos (22, −14) |
| Plaza común | centro (−10, −2), r = 6,5 |
| Plazas temáticas | Abecedario (−16, 24) · Números (−16, −24) · Meses (15, 24) · Saludos (15, −24), r = 4,2 |
| Spawn / Sena | (−15,5, 0,1, −2,5) mirando al este (yaw 90°) / (−7, 0, −3,3) de cara al spawn |
| Puente norte | centro (−5,7, 29,1), yaw 89° |
| Puente centro-norte | centro (3,8, 9,0), yaw 61° |
| Puente centro-sur | centro (−1,8, −12,0), yaw 127° |
| Puente sur | centro (4,4, −31,0), yaw 61° |
| Río (puntos de control) | (2,45) (−2,40) (−5,34) (−6,28) (−4,22) (−1,16) (2,12) (5,7) (7,1) (5,−4) (1,−9) (−3,−13) (−4,−18) (−2,−23) (2,−27) (5,−32) (7,−37) (5,−41) (3,−45) |
| Reservas de monumentos | Abecedario (−24,2; 24,0) (−18,1; 31,9) (−21,8; 29,8) · Números (−20,1; −31,1) (−24,2; −24,0) (−16,0; −32,2) · Meses (17,1; 31,9) (7,9; 19,9) (23,2; 24,0) · Saludos (23,2; −24,0) (15,0; −32,2) (20,8; −29,8) |

Puertas reales (medidas de las puertas de producción): desplazamiento lateral respecto al centro de la fachada de −2,48 (Alphabet), −0,38 (Numbers) y −0,77 m (Months y Greetings).

## 3. Medidas
- **Río**: longitud 101,9 m (sinuosidad 1,13 frente a 90 m en línea recta), giro acumulado 318°, agua de 2,9 a 3,9 m de ancho (media 3,5 m); casas a ≥ 18,5 m del eje.
- **Puente** (medido en 6.1.2 con colliders reales): 4,24 × 2,63 × 12,37 m; MeshCollider original de 1 840 triángulos; tablero en arco de −0,72 m (extremos) a +0,27 m (centro) en coordenadas locales; se coloca +0,72 m para que los extremos queden a ras del suelo (altura máxima del jugador al cruzar y = 1,09 m, pendiente ≈ 12°).
- **Caminos**: 13 cintas de 2,5 m de ancho; llegada recta de 7 m a cada puerta.
- **Escena**: 442 objetos, 277 MeshRenderers (108 188 triángulos), colliders activos: 43 caja, 74 cápsula, 6 MeshCollider (24 856 triángulos: suelo, barrera del río y 4 puentes).

## 4. Cambios respecto a 6.1.4
| Elemento | 6.1.4 | 6.2.1 |
|---|---|---|
| Distribución | 2 casas por orilla, 2 puentes | 4 aldeas (1 casa + plaza temática + reservas), 4 puentes |
| Río | diagonal suave, ancho 3,5 m constante | sinuoso, anchura 2,9–3,9 m, orillas irregulares |
| Plaza común | centro (−13, 0) | centro (−10, −2) |
| Spawn | (−18, 0,1, −1,5) | (−15,5, 0,1, −2,5) — la plaza se movió y el banco de la ruta salía del eje |
| Sena | (−10,5, 0, 2,2) | (−7, 0, −3,3) — de frente al spawn y fuera de las rutas |
| Explanada este | sí | no (los puentes centrales aterrizan en caminos hacia Meses y Saludos y hay una ribera este) |
| Faroles con luz nocturna | 8 | 12 (`maxDecorativeLights = 12`, solo en la instancia del prototipo) |
| Señales | 8 | 13 |

Las casas conservan posición, orientación, puertas y huellas de 6.1.4.

## 5. Pruebas (todas automatizadas; `Evidence/r621_resultados.txt`: **137 pasan, 0 fallan**)
- **Geometría**: río continuo (205 muestras, paso máx. 0,50 m), sin pliegues (separación mínima entre tramos no vecinos 20,9 m), orillas continuas (3 485 vértices = muestras × 17), 4 puentes perpendiculares (desvío 0,0°) con suelo a ras en ambos extremos, sin caminos sobre el agua ni terminados en ella, sin caminos por huellas de casa, sin cruces confusos (solo empalmes en los extremos), llegada recta a las 4 puertas reales (desvío ≤ 0,16 m), reservas a ≥ 5,7 m de caminos y ≥ 9 m del río.
- **Accesibilidad**: del spawn a las 4 aldeas por los caminos reales y regreso a la plaza; los 13 caminos de extremo a extremo; 4 puentes × 2 cruces completos (ida y vuelta); 9 intentos de cruzar el río fuera de los puentes (1 en el cauce y 8 a ±4,5 m de cada puente), todos frenados en la barrera; 4 salidas del mapa bloqueadas.
- **Colisiones**: casas sólidas (se frena a 0,38 m); 10 m frente a cada puerta sin objetos ni colliders; 300 caídas al azar (incluidas orillas) sin atravesar el suelo; nada bloquea el eje de ningún puente (mín. 2,5 m); los únicos colliders sin renderer son los límites y la barrera del río.
- **Jugador**: CharacterController y FirstPersonController activos, una cámara principal, un AudioListener; spawn sin solapamiento; Sena a 8,5 m, a 5° del eje de la cámara, línea de visión libre.
- **Calidad**: 0 scripts faltantes; 0 errores de compilación; 0 excepciones en Play (solo aparece en el log de Unity una línea de IPC del Editor al arrancar y mensajes del cliente de licencias, ajenos al proyecto).
- **Idempotencia**: `BuildTwiceAndCompare` construye dos veces: 177 nombres distintos, 442 objetos, segunda construcción idéntica (`Evidence/r621_idempotencia.txt`).
- **Play Mode** (`Evidence/r621_playmode.txt`, entrada inyectada): Sena saluda (Wave) y vuelve a Idle; ruta de 141 puntos por las 4 casas y los 4 puentes en ambos sentidos con giro final hacia la fachada; altura mínima y = 0,08; noche con `GameSettings.DarkMode`: 12 de 12 faroles con luz.

## 6. Pendientes y limitaciones
- **No verificado**: prueba con teclado y ratón físicos; rendimiento en PC y móvil (no se midió); cómo se ve en pantallas y calidades distintas.
- **Carteles**: de lejos el texto es pequeño (se leen bien a < 12 m); son provisionales.
- **Puentes**: comparten arquitectura; la diferencia es solo el tono de madera y el cartel (refinamiento previsto en 6.2.4).
- **Plazas temáticas**: discos lisos sin diseño; los anillos de reserva son discretos y solo se ven de cerca.
- **Noche**: la periferia queda muy oscura y la iluminación está concentrada en plaza y puentes (6.2.5).
- **Empalme este del puente centro-sur**: el camino de la ribera y el de Saludos salen en «V»; es transitable, pero conviene suavizarlo.
- **Sinuosidad moderada** (1,13): está limitada por la posición de las aldeas y los puentes aprobados.
- **Sena**: presencia y saludo, sin diálogos ni interacciones (fuera de alcance).
- El conmutador día/noche y las luces reales de los faroles solo existen en Play (en el Editor las capturas nocturnas usan luces temporales).

## 7. Prueba manual con teclado y ratón físicos (para la usuaria)
Abrir la escena, pulsar Play. Controles: WASD · ratón · Shift · Esc. Atajos del prototipo (`R621TestHelper`): **0** spawn · **1** Abecedario · **2** Números · **3** Meses · **4** Saludos · **5** puente norte · **6** centro-norte · **7** centro-sur · **8** puente sur · **N** día/noche · **H** ayuda.
1. [ ] Desde el spawn se ven Sena, el poste de las cuatro aldeas, y los puentes centrales a los lados.
2. [ ] Llegar a pie a las cuatro puertas; ¿se ve de frente la fachada y el cartel?
3. [ ] Cruzar los 4 puentes en ambos sentidos; ¿algún escalón o atasco en los extremos?
4. [ ] Intentar entrar al agua fuera de los puentes (debe frenar).
5. [ ] Recorrer plazas temáticas y ver las reservas en el suelo; ¿se entienden como espacios futuros?
6. [ ] Lomas de las esquinas NE (suave) y SO (≈ 22°), límites del mapa.
7. [ ] Pulsar N: noche; ¿se orienta uno por faroles y caminos?
No corregir en la escena: anotar punto y expectativa.
