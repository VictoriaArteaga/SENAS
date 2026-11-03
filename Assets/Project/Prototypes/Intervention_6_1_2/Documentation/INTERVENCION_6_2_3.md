# Intervención 6.2.3 — Paisajismo, vegetación y riqueza visual

Escena: `Scenes/Prototype_Modular_FourVillages.unity`. Recursos nuevos con prefijo `R623_`; evidencia `r623_*` en `Evidence/`.
Código: `Editor/R623Builder.cs`, `Editor/R623Meshes.cs`, `Editor/R623Verify.cs`, `Editor/R623PlayTest.cs`. Menús: `Lumina/Prototipo 6.2.3/...`.

## 1. Objetivo
Convertir las cuatro zonas decoradas de 6.2.2 en una sola región habitable: más variedad de árboles, arbustos y flores, transiciones entre aldeas, orillas del río vivas y paisajismo que refuerza la identidad de cada aldea, sin tocar aldeas, casas, monumentos, río ni puentes. No es la intervención de optimización.

## 2. Estado inicial
Rama `feat/style-ui`, HEAD `96b5787e1e3a2385f7b5f1e4adca1e09371bfbc5`, 8 stashes, árbol tracked limpio, solo `Assets/Project/Prototypes/` sin seguimiento, Unity cerrado. Base 6.2.2: 520 MeshRenderers, 176 260 triángulos visibles, 160 CapsuleCollider, 75 BoxCollider, 6 MeshCollider (barreras del río y suelo).

## 3. Cambios
- El constructor **no reconstruye** nada: abre la escena de 6.2.2, borra (si existe) y recrea el grupo `--- PAISAJE R623 ---` con semillas fijas. Ningún recurso `R621_`/`R622_` se escribe (comprobado con hashes: solo cambia el archivo de la escena).
- 10 siluetas de árbol (oak, poplar, pine_tall, sapling, blossom, willow, birch + round, cone, cluster, umbrella reutilizadas de 6.2.2 en modo lectura), arbustos (bush_low, bush_tall, bush_berry), hierba, juncos, 4 formas de flor (daisy, tulip, spike, poppy) con 7 disposiciones (dispersa, anillo, arco, curva, línea, cuadrícula, rombo) y dos parterres con borde (cuadrado y redondo).
- 16 materiales `R623_` (verdes, flores, troncos, juncos, tierra, borde); 36 mallas `R623_` en `Meshes/`.
- Reglas de colocación (`Ok`): ≥ 2 m útiles junto a los caminos, 3 m de puertas y de los ejes de puente, fuera de plazas, del corredor spawn→Sena, del agua y de lo ya colocado; además cada árbol comprueba el alcance real de su copa antes de colocarse.

## 4. Cantidad y tipos (línea base para la futura optimización)
| Elemento | Cantidad |
|---|---|
| Árboles nuevos | 47 (10 siluetas, 13 tonos de copa, escala 0,65–1,37, máx. 6,5 m) |
| Arbustos | 35 |
| Matas de hierba | 17 |
| Juncos | 19 |
| Grupos de flores | 37 |
| Parterres con borde | 6 (3 cuadrados Números, 3 redondos Meses; el 4.º redondo no encontró sitio) |
| Piezas nuevas | 161, 52 284 triángulos, 40 mallas únicas, 35 materiales usados |
| Árboles en toda la escena (6.2.1+6.2.2+6.2.3) | 155 |
| Escena completa | 681 MeshRenderers, 228 544 triángulos visibles |
| Colliders | +63 CapsuleCollider (troncos); 223 en total; 0 MeshCollider nuevos |

## 5. Distribución por aldea
- **Abecedario:** curvas y arcos de flores de muchos colores, árboles suaves (blossom rosa/lila, round, umbrella, sapling), arbustos con bayas; sin geometría rígida.
- **Números:** 3 parterres cuadrados con marco y flores en cuadrícula, hileras casi regulares de álamos y árboles redondos con escala creciente (0,85→1,10), flores en cuadrícula/rombo/línea.
- **Meses:** 4 parterres circulares con borde y flor de estación, un árbol por estación (flor rosa, copa verde densa, naranja, pino azulado), anillos de flores. Sin sistema dinámico de estaciones.
- **Saludos:** 2 robles amplios para sombra a los lados de la plaza, flores cálidas (coral, naranja, rosa, amarillo) pegadas a la plaza, césped abierto frente a la escultura. Las manos siguen siendo arte decorativo, no señas de la LSC.
- **Transiciones:** 11 grupos de 2–5 árboles con sotobosque; la especie de cada punto se sortea por cercanía ponderada a las cuatro aldeas, de modo que en las zonas entre aldeas se mezclan sus paletas. Líneas naturales de árboles/arbustos junto a los caminos (cada 5–9 m, lados alternos), árboles de referencia entre aldeas, y relleno de huecos con hierba y flores.

## 6. Río
Juncos junto al agua (≥ 2,6 m del eje), flores y arbustos en la orilla, sauces/abedules a ≥ 8,5 m. 49 piezas a < 9 m del eje. Todos los puentes mantienen una franja libre de ≥ 3,0 m desde su eje (±12 m) y 0 colliders sobre los accesos.

## 7. Colliders
Solo `CapsuleCollider` de tronco en los 47 árboles y en los arbustos altos; flores, hierba, juncos, parterres y arbustos bajos no llevan collider; 0 MeshColliders.

## 8. Pruebas
`R623Verify` (181 pasan, 0 fallan; incluye todas las de 6.2.1/6.2.2): puertas, caminos, puentes, spawn, Sena, plazas, río, suelo, borde del mundo, colisión con piezas de 6.2.2, recorridos CharacterController, sin referencias nulas. Ver `Evidence/r623_resultados.txt`.
**Identidad sin señalización** (`r623_16_sin_carteles_*`, 12 capturas: entrada, plaza y río por aldea; los carteles se ocultan solo durante la captura y se restauran): juicio visual y cualitativo del autor; letras, cifras, reloj+calendario y manos siguen siendo reconocibles. No fue validado por jugadores reales.
**Play Mode** (`r623_playmode.txt`): PRUEBA COMPLETA, ruta de 115 puntos (spawn, Sena, plaza, Abecedario, puente norte, Meses, puente centro, Saludos, puente sur, Números, regreso a la plaza), altura mínima 0,08 m y máxima 1,09 m, 12 de 12 faroles con luz de noche. La entrada de teclado/ratón es **inyectada** por el Input System; **no** se probó con periféricos físicos.
**Noche:** capturas `r623_play_noche_*` y `r623_18_general_noche_editor.png`: aldeas, plazas, caminos y puentes siguen identificables; las copas oscuras no crean siluetas confusas. Revisión visual, sin revisión completa de iluminación.

## 9. Idempotencia
`R623Builder.BuildTwiceAndCompare`: 297 nombres / 865 objetos en toda la escena, 36 mallas R623 y huella SHA-256 de posiciones/rotación/escala del paisajismo idénticas en ambas construcciones → **IDÉNTICA** (`Evidence/r623_idempotencia.txt`).

## 10. Problemas encontrados y resueltos
- Los arbustos salían rojos: las bolas usaban la submalla del fruto como hoja (corregido con `ShrubBall`).
- Copas de roble que rozaban el borde de un camino: el alcance de la copa era mayor que el radio de separación; `TreeAt` ahora rechaza cualquier árbol cuya copa invada caminos, puertas, ejes de puente o plazas.
- La prueba medía con cajas alineadas a los ejes (sobreestimaba copas giradas): ahora usa el vértice más lejano al pivote.
- Cámaras de evidencia que quedaban dentro de una copa: `ClearCam` elige la posición con menos árboles en la visual.

## 11. Pendientes
- NO VERIFICADA: prueba con teclado y ratón físicos; rendimiento en dispositivo real (no se midió FPS ni tiempo de render); validación con jugadores reales.
- Optimización (instancing/LOD/combinar mallas) queda para la intervención específica; los datos de la sección 4 son su línea base.
- El 4.º parterre redondo de Meses no encontró sitio libre (3 en lugar de 4).
- La captura cenital sale desvaída por la configuración de la cámara de edición, igual que en 6.2.2.

## 12. Archivos nuevos
`Editor/R623Builder.cs`, `R623Meshes.cs`, `R623Verify.cs`, `R623PlayTest.cs` (+ `.meta`), `Documentation/INTERVENCION_6_2_3.md`, 36 mallas `Meshes/R623_*.asset`, 16 materiales `Materials/R623_*.mat`, evidencia `Evidence/r623_*` (capturas, `r623_resultados.txt`, `r623_playmode.txt`, `r623_idempotencia.txt`). Modificado: solo `Scenes/Prototype_Modular_FourVillages.unity` (se añade `--- PAISAJE R623 ---`).

## 13. Aislamiento
Sin cambios en producción, otras escenas, prefabs ni materiales compartidos, Build Settings, `Sena.prefab`, `Bridge.prefab`, FirstPersonController, Theme*, URP, ni en recursos `R621_`/`R622_` (hashes SHA-256 comprobados). Sin commits, push, cambio de rama ni de stashes.
