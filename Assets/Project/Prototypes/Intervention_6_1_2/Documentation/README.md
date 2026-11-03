# Intervención 6.1.2 — Prototipo comparativo de terreno

Prototipo independiente: dos escenas con **el mismo diseño** (misma función de altura, caminos, río, puentes, casas y vegetación) y distinta tecnología de suelo. No toca producción y ninguna escena está en Build Settings.

| Escena | Suelo | Caminos / plaza / orillas |
|---|---|---|
| `Scenes/Prototype_Modular.unity` | 1 malla facetada de 1 m de celda (16 200 triángulos, 1 MeshCollider) | mallas de cinta superpuestas |
| `Scenes/Prototype_Terrain.unity` | Unity Terrain 90×90 m, heightmap 257², 4 TerrainLayers propias | pintados en el mapa de pesos (512²) |

Común: río (agua transparente + barrera invisible con huecos en los puentes), 2 puentes (`Bridge.prefab` de producción, sin modificar), 4 casas (prefabs `House_*` con la paleta `Lumina_HousePalette`), 70 árboles, 16 rocas, 50 flores, 4 bancos, 8 faroles, Sena (solo visual), límites del mundo, luz y cielo de LuminaWorld (`ThemeAmbient` en el sol) y un jugador con los parámetros de producción y el `FirstPersonController` existente (solo referenciado).

## Reconstruir y verificar (menú Lumina ▸ Prototipo 6.1.2)
- `Construir ambas escenas` → `Proto612Builder.BuildAll`
- `Verificar y capturar` → `Proto612Verify.Run` (pruebas de colisión, estadísticas, capturas en `Evidence/`, no guarda escenas)
- `Proto612Measure.Run` mide los prefabs y la carga de los recursos de Holotna; `Proto612Debug.Run` diagnostica el mapa de pesos del Terrain.

## Decisiones
- Río desplazado al oeste respecto a la referencia del encargo (el original pasaba a ~3 m del muro de la casa de los números).
- Los colliders de malla de los prefabs se desactivan en la instancia (override) y se sustituyen por cápsulas/cajas; los prefabs originales no se tocan.
- El puente se sube 0,72 m para que su tablero (con forma de arco) quede a ras de las orillas.

## Pendiente (no verificable aquí)
Rendimiento en dispositivo (PC real y móvil), aspecto de los Shader Graph de Holotna en URP 17 (cargan y son «soportados», pero no se renderizaron), y revisión manual en Play con teclado y ratón reales.

---

## Intervención 6.1.3: río central (`Scenes/Prototype_Modular_RiverCentral.unity`)

Escena nueva (las de la 6.1.2 no se tocan). El río cruza el mapa de norte a sur y separa dos sectores: **oeste** (AlphabetHouse, NumbersHouse) y **este** (MonthsHouse, GreetingsHouse). La plaza está en la orilla oeste, junto al puente principal; una explanada recibe ese puente en la orilla este; un segundo puente, al norte, une las dos casas del norte.

- Layout: `Editor/Proto612LayoutV2.cs` · Construcción: `Editor/Proto612BuilderV2.cs` (menú «Construir modular con río central») · Verificación: `Editor/Proto612VerifyV2.cs` · Play Mode con entrada inyectada: `Editor/Proto612PlayTest.cs` + `Scripts/Proto612PlayDriver.cs`.
- Resultados en `Evidence/rc_resultados.txt` (52 pruebas) y `Evidence/rc_playmode.txt` (ruta de 80 puntos con el controlador real).
- Reutiliza los materiales `Proto_*` ya existentes sin recrearlos (así no cambian sus GUID y no se rompe `Prototype_Terrain`).

---

## Intervención 6.1.4: refinamiento (misma escena `Prototype_Modular_RiverCentral.unity`)

La distribución espacial aprobada (casas, río, puentes, plaza y explanada) no cambia. Cambios: caminos de las casas con tramo final recto y perpendicular a la fachada (llegan a la puerta real de cada prefab), 8 postes de señalización con TextMeshPro, Sena con animación real (variante `Prefabs/Sena_Proto.prefab`, presencia `Scripts/Proto614SenaGreeter.cs`), 8 faroles con luz nocturna real (`ThemeLights` con `maxDecorativeLights = 10` en la instancia del prototipo), bancos y faroles recolocados con despeje medido, y atajos de prueba manual (`Scripts/Proto614TestHelper.cs`).

- Prueba manual: `Documentation/PRUEBA_MANUAL.md`.
- Resultados automatizados: `Evidence/r4_resultados.txt` (70 pruebas de Editor) y `Evidence/r4_playmode.txt` (Play Mode con entrada inyectada). Capturas: `Evidence/r4_*.png`.
- Diagnóstico de Sena: `Editor/Proto614SenaInspect.cs` (solo lectura).
