# Escena Lúmina (primera versión explorable)

Escena: `Assets/Project/Scenes/LuminaWorld.unity`
Unity 6000.0.34f1 · URP 17.0.3 · Input System 1.11.2 (`activeInputHandler: 2`, ambos sistemas activos).

## Estructura de la escena

```
Main Camera            (hija de Player; única cámara y único AudioListener)
Directional Light
--- LUMINA ---
├── Environment
│   ├── Ground         GroundPlane (Plane 150 x 150 m, material Lumina_Ground)
│   ├── Paths          Plaza (3x3 Path_2) + MainRoad (4x Path_1)
│   ├── Buildings      5 casas (House_1/2/3), escala 0.5
│   ├── Vegetation     ~45 árboles (escala ~0.7), flores y rocas
│   └── Props          bancos, faroles, vallas, cajas, macetas
├── Characters
│   └── Sena           Collider + ProximityInteractable + SenaPresenter
│       └── Model      variante de prefab del Avatar del paquete
└── PlayerSpawn        (0, 0.1, -20)
Player                 CharacterController + FirstPersonController
```

- **Área de inicio**: el jugador aparece en `(0, 0, -20)` mirando hacia +Z, al final del camino principal.
- **Zona de conversación**: la plaza empedrada en el origen (≈14 x 14 m, sin obstáculos). Sena está en `(0, 0, 2)`, mirando hacia el camino de entrada.
- El suelo es un plano con material URP Lit (el paquete de aldea no incluye superficie modular de suelo). No se añadió `Terrain`.
- Cielo: material `FS002_Day` de *Fantasy Skybox FREE*. Luz direccional cálida con sombras suaves; ambiente desde el skybox.

## Assets reutilizados

| Elemento | Origen |
|---|---|
| Casas, caminos, árboles, rocas, flores, vallas, bancos, faroles | `Assets/Models/OccaSoftware/Low Poly Fantasy Village` |
| Cielo | `Assets/Models/Fantasy Skybox FREE` (`FS002_Day`) |
| Sena | `Assets/Models/SunboxGames/Avatars` (`Avatar.prefab` + `AvatarCustomization`) |

Los prefabs de la aldea son grandes frente a un humano de 1.8 m (casas ≈ 12 m, árboles ≈ 8 m), por eso las casas se escalan a 0.5 y los árboles a ~0.7.

No se usó `Holotna/Mountain`: sus materiales de Shader Graph se renderizaron en magenta en las pruebas de esta tarea (ver pendientes). No se usó `Free Pack - VR Hands`: no se añadieron manos a la cámara.

## Jugador y cámara

Script: `Assets/Project/Scripts/Player/FirstPersonController.cs` (namespace `Lumina.Player`).

- `CharacterController`: altura 1.8 m, radio 0.3 m, `stepOffset` 0.3.
- Cámara a 1.65 m de altura (hija de `Player`); el giro horizontal rota `Player` y el vertical rota la cámara (límite ±80°).
- Lee `Keyboard.current` y `Mouse.current` (Input System); no se cambió la configuración de entrada del proyecto ni se instalaron paquetes.
- Parámetros en el Inspector: `moveSpeed` (4), `sprintMultiplier` (1.6), `gravity` (-20), `mouseSensitivity` (0.1 °/unidad), `maxLookAngle`.

### Controles

| Acción | Tecla |
|---|---|
| Mover | W A S D |
| Correr | Shift izquierdo |
| Mirar | Ratón |
| Liberar el cursor | Esc |
| Volver a capturar el cursor | Clic izquierdo |
| Interactuar (cerca de Sena) | E |

## Sena

- `Assets/Project/Prefabs/Sena.prefab` es una **variante** del `Avatar.prefab` oficial (el original no se modifica). Se configuró con el sistema oficial `AvatarCustomization`: género femenino, blusa (`ClothingItem_Blouse`), falda (`ClothingItem_Skirt_01`) y zapatos planos (`ClothingItem_Flats`). Cuerpo, ropa y zapatos comparten el rig del paquete.
- `ProximityInteractable` (`Scripts/Interaction`): detecta por distancia (3 m) si el jugador está cerca y emite `onPlayerEnter`, `onPlayerExit` y `onInteract` (tecla E). Muestra el texto "Presiona E para hablar". No contiene lógica de lecciones.
- `SenaPresenter` (`Scripts/NPC`): solo presentación; gira hacia el jugador al acercarse y vuelve a su orientación al alejarse.
- La lógica de lecciones debe suscribirse a los eventos de `ProximityInteractable`, sin modificar `SenaPresenter`.
- No hay diálogos, evaluación de señas ni MediaPipe, ni poses de LSC.

### Limitaciones

- La prenda superior sale en su variación 0 (negra); no se eligió una paleta definitiva.
- No se verificó la animación en Play: en la captura en modo editor Sena aparece en T-pose porque el Animator no se evalúa fuera de Play. El controlador del avatar debería reproducir el idle al ejecutar, pero **no está comprobado**.
- `AvatarCustomization` aplica la personalización en `Start()`; el peinado/rasgos mostrados en el editor pueden diferir ligeramente de Play.
- Los materiales del paquete `SunboxGames/Avatars` eran del pipeline Built-in (magenta en URP); se convirtieron a URP Lit con el actualizador oficial de URP (cambia los `.mat` del paquete).

## Regenerar la escena

`Assets/Project/Editor/LuminaSceneBuilder.cs` reconstruye el contenido de forma repetible (borra `--- LUMINA ---` y `Player` y los vuelve a crear; conserva la Main Camera original y la luz direccional):

- Menú: **Lumina → Construir escena LuminaWorld**.
- Batchmode (con Unity cerrado): `Unity.exe -batchmode -quit -projectPath <ruta> -executeMethod Lumina.EditorTools.LuminaSceneBuilder.Build`

Cualquier edición manual dentro de `--- LUMINA ---` se perderá al regenerar.

## Abrir y probar

1. Abrir el proyecto con Unity 6000.0.34f1.
2. Abrir `Assets/Project/Scenes/LuminaWorld.unity`.
3. Pulsar Play, caminar por el camino hacia Sena con W y el ratón; pulsar E al llegar a menos de 3 m.

`LuminaWorld` no está en *Build Settings* (la lista actual contiene solo `Menu`); añadirla si se va a cargar desde el menú.

## Comprobaciones

Realizadas (Unity en batchmode, simulación en modo editor, sin ejecutar Play):

- El proyecto compila sin errores de C#.
- `LuminaWorld.unity` se genera y guarda; el log no reporta referencias perdidas.
- Una única cámara, un único `AudioListener` y un único `GroundPlane`.
- `CharacterController` cae desde 3 m y queda apoyado en el suelo (y ≈ 0.03).
- Caminando hacia `House_1` el controlador se detiene en x ≈ -13.1 (colisiona con la casa).
- Caminando hacia Sena desde el spawn se detiene en z ≈ 1.2 (collider de Sena).
- Renders de la escena desde la cámara del jugador: casas, plaza, camino y Sena visibles y a escala coherente.

Pendientes (requieren ejecutar la escena con Unity abierto):

- Probar en Play: movimiento con WASD, giro con ratón, Esc / clic para el cursor, sensibilidad y velocidad.
- Confirmar que el Animator de Sena reproduce idle y que su personalización se aplica en `Start()`.
- Confirmar el texto "Presiona E para hablar" y el giro de Sena al acercarse.
- Revisar la orientación (frente/puerta) de las casas; se rotaron hacia la plaza sin ver el frente real del modelo.
- Comprobar que los colliders de copas de árboles no bloquean al jugador en puntos inesperados.

## Problemas conocidos

- **Los modelos de la aldea salen blancos**: `Materials/Color.mat` referencia una textura de paleta (GUID `6e3effe51d2b204469e0b1939c0cd8c6`) que no existe en el repositorio ni en el historial de Git (solo está `Gradient.png`). Hay que volver a importar el paquete *Low Poly Fantasy Village* completo o añadir esa textura.
- `Holotna/Mountain`: varios materiales de Shader Graph (árbol 01A, arbusto, roca) se renderizaron en magenta en batchmode; no se investigó la causa.
- Los cambios sin commitear que ya existían en `Menu.unity` y `ProjectSettings/EditorBuildSettings.asset` no forman parte de esta tarea.
