# Intervención 6.2.2 — Identidad visual de las cuatro aldeas

Escena de trabajo: `Scenes/Prototype_Modular_FourVillages.unity` (reconstruida con `Lumina/Prototipo 6.2.2`).
Recursos nuevos con prefijo `R622_`; evidencia con prefijo `r622_` en `Evidence/`.
Código: `Editor/R622Builder.cs`, `Editor/R622Meshes.cs`, `Editor/R622Verify.cs`, `Editor/R622PlayTest.cs`.

## 1. Corrección previa: empalme en «V» del puente centro-sur
El camino del puente centro-sur y el de Saludos formaban una «V» en el extremo este. Cambios en `R621Layout.cs`:
- El puente conecta con un punto `q` situado 3 m dentro de la dirección de Saludos, con suavizado Chaikin (×3).
- La ribera este se une al camino de Saludos en «T» (desvío 0,00 m, ángulo de unión 44°).
- Resultado medido: giro máximo entre tramos de 0,5 m = 18,4° en ±9 m del extremo (sin ángulos vivos).

## 2. Identidad de cada aldea
| Aldea | Monumento central | Elementos propios | Jardines |
|---|---|---|---|
| Abecedario | Letras A, B, C sobre peana (A de 2,9 m) | Letra L alta (4,2 m), letras de juego repartidas | Libres, mezcla de flores |
| Números | Columna ascendente de cifras | Cifra 7 alta (3,9 m), cifras, formas geométricas | Parterres ordenados |
| Meses | Torre del reloj (6,6 m) | Calendario 3D (3,2 m) con 12 meses y casillas por estación; plaza circular | Colores de estación |
| Saludos | Escultura de manos (2,4 m) | Manos decorativas sobre peanas, plaza social con bancos | Flores y arbustos |

Las manos son únicamente elementos artísticos estilizados: no representan ninguna seña de LSC y no se inventó contenido de lengua de señas.

## 3. Vegetación
Árboles de varias especies (redondo, cónico, ovalado, copa en nubes, otoñal), arbustos y parches de flores, con semillas deterministas. Cantidades por aldea (resultados de verificación):
- Abecedario: 28 piezas, 10 712 triángulos (3 árboles, 4 parches de flores)
- Números: 38 piezas, 4 340 triángulos (1 árbol)
- Meses: 60 piezas, 12 196 triángulos (4 árboles, 8 arbustos, 12 parches)
- Saludos: 35 piezas, 18 388 triángulos (4 árboles, 3 arbustos, 9 parches)
- **Total decoración:** 243 piezas, 68 112 triángulos, 81 mallas únicas, 40 materiales.

## 4. Colisiones y rendimiento
Solo cápsulas (86) y cajas (32) en la decoración; 0 MeshColliders decorativos. Los MeshColliders de los bancos de producción se desactivan en las instancias y se sustituyen por cajas. Escena completa: 520 MeshRenderers, 176 260 triángulos visibles. El tiempo de render por cuadro **no se midió**.

## 5. Pruebas (todas en `Evidence/r622_resultados.txt`)
- 156 pruebas verificadas pasan, 0 fallan: caminos sin cruces confusos, puertas y corredores de puerta libres (0 solapes), 4 puentes (±9 m) libres de decoración, empalme, barrera del río, spawn, Sena sin tocar.
- **Identidad sin carteles** (12 capturas `r622_16_sin_carteles_*`: entrada, plaza y río por aldea): juicio visual subjetivo del autor; letras, cifras, reloj+calendario y manos se distinguen claramente. No es una prueba con usuarios.
- **Idempotencia**: construir dos veces produce escena idéntica (274 nombres, 694 objetos, 68 mallas R622).
- **Play Mode** (`r622_playmode.txt`): PRUEBA COMPLETA, ruta de 115 puntos, 4 giros de fachada, Sena Wave→Idle, 12/12 luces de farol de noche, alturas 0,08–1,09 m. Los eventos de teclado/ratón son inyectados por Input System, no de periféricos físicos.

## 6. NO VERIFICADA
- Prueba manual con teclado y ratón físicos.
- Rendimiento en dispositivo real / FPS.
- Legibilidad de la identidad con jugadores reales.

## 7. Aislamiento
No se modificaron escenas de producción, `Prototype_Modular`, `Prototype_Terrain`, `Prototype_Modular_RiverCentral`, prefabs/materiales/scripts compartidos, `Sena.prefab`, `Bridge.prefab`, FirstPersonController, Theme*, URP ni Build Settings. Sin commits, sin push.
