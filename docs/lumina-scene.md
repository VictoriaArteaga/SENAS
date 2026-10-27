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
- **Zona de conversación**: la plaza empedrada en el origen (≈14 x 14 m, sin obstáculos). Sena está en `(0, 0, 2)`, mirando hacia el camino de entrada (-Z).
- El suelo es un plano con material URP Lit (el paquete de aldea no incluye superficie modular de suelo). No se añadió `Terrain`.
- Cielo: material `FS002_Day` de *Fantasy Skybox FREE*. Luz direccional cálida con sombras suaves; ambiente desde el skybox. Una sola luz direccional.

## Assets reutilizados

| Elemento | Origen |
|---|---|
| Casas, caminos, árboles, rocas, flores, vallas, bancos, faroles | `Assets/Models/OccaSoftware/Low Poly Fantasy Village` |
| Cielo | `Assets/Models/Fantasy Skybox FREE` (`FS002_Day`) |
| Sena | `Assets/Models/SunboxGames/Avatars` (`Avatar.prefab` + `AvatarCustomization`) |

Los prefabs de la aldea son grandes frente a un humano de 1.8 m (casas ≈ 12 m, árboles ≈ 8 m), por eso las casas se escalan a 0.5 y los árboles a ~0.7.

No se usó `Holotna/Mountain` (ver problemas conocidos) ni `Free Pack - VR Hands` (no se añadieron manos a la cámara).

## Materiales

### Estado tras reimportar *Low Poly Fantasy Village*

- La reimportación **no recuperó la textura de paleta**: `Textures/` sigue conteniendo solo `Gradient.png`. `Materials/Color.mat` mantiene su `_BaseMap` apuntando al GUID `6e3effe51d2b204469e0b1939c0cd8c6`, que no existe, y Unity lo resuelve como textura vacía (comprobado en batchmode: `Color.mat` → `_BaseMap = NULL`). El paquete (v0.1.0, sin textura en su `package.json` ni README) parece distribuirse sin ese archivo.
- Los modelos usan los UV de esa paleta (por ejemplo los de `Path_1` colapsan en un solo punto), así que sin la textura original no se pueden reproducir sus colores reales.
- Todos los materiales del paquete usan `Universal Render Pipeline/Lit`; no hay materiales magenta en los prefabs usados en la escena.
- La reimportación solo cambió el `assetPath` dentro de unos 120 archivos `.meta` del paquete. No se incluyeron esos cambios en los commits de esta tarea.
- `Color.mat` y los prefabs del paquete **no se modificaron**.

### Corrección mínima aplicada: colores provisionales

`Assets/Project/Editor/LuminaMaterialTint.cs` (menú **Lumina → Aplicar colores provisionales / Quitar colores provisionales**) crea copias de `Color.mat` en `Assets/Project/Materials/Lumina_Village_*.mat` con un color base por categoría y las asigna como *override* en las instancias de la escena (el `Light.mat` de los faroles se conserva):

| Categoría | Color aproximado |
|---|---|
| Casas | crema |
| Árboles / pinos | verde / verde oscuro |
| Caminos | gris arena |
| Vallas, bancos, cajas | marrón madera |
| Faroles | gris oscuro |
| Macetas / flores | terracota / rosa |

Es una **aproximación**, no el arte original: cada modelo tiene un solo material, así que techos, puertas y ventanas comparten color con el resto del modelo. Si se consigue la textura original del paquete, ejecutar **Quitar colores provisionales** para volver a `Color.mat`.

## Orientación de las casas

El frente (puerta) de los prefabs de casa mira hacia +Z local; se comprobó renderizando cada modelo desde sus cuatro lados. Las casas ya estaban rotadas para que ese frente mire hacia la plaza o el camino, por lo que no se cambió ninguna rotación. La valla del fondo deja un hueco de ~1.6 m en el centro para llegar a `House_3`.

## Jugador y cámara

Script: `Assets/Project/Scripts/Player/FirstPersonController.cs` (namespace `Lumina.Player`).

- `CharacterController`: altura 1.8 m, radio 0.3 m, `stepOffset` 0.3.
- Cámara a 1.65 m de altura (hija de `Player`); el giro horizontal rota `Player` y el vertical rota la cámara (límite ±80°).
- Lee `Keyboard.current` y `Mouse.current` (Input System); no se cambió la configuración de entrada del proyecto ni se instalaron paquetes.
- Parámetros en el Inspector: `moveSpeed` (4), `sprintMultiplier` (1.6), `gravity` (-20), `mouseSensitivity` (0.1 °/unidad), `maxLookAngle`.
- Al perder el foco de la aplicación se libera el cursor.

### Controles

| Acción | Tecla |
|---|---|
| Mover | W A S D |
| Correr | Shift izquierdo |
| Mirar | Ratón |
| Liberar el cursor | Esc |
| Volver a capturar el cursor | Clic izquierdo |
| Interactuar (cerca de Sena) | E |

Con el cursor liberado el jugador no se mueve ni gira.

## Sena

- `Assets/Project/Prefabs/Sena.prefab` es una **variante** del `Avatar.prefab` oficial (el original no se modifica). Se configuró con el sistema oficial `AvatarCustomization`: género femenino, **blusa blanca** (`ClothingItem_Blouse`, variación 1), **falda larga negra** (`ClothingItem_Skirt_01`, variación 0) y **zapatos planos negros** (`ClothingItem_Flats`, variación 0). Cuerpo, ropa y zapatos comparten el rig del paquete.
- `Assets/Project/Editor/LuminaSenaSetup.cs` (menú **Lumina → Aplicar apariencia de Sena**) reaplica estas variaciones y regenera la ropa dentro del prefab.
- **Animación**: el `Animator` usa `AvatarAnimator_Locomotion` (del paquete); en Play el estado por defecto reproduce el clip `Armature|Idle_00` (comprobado). En modo editor (sin Play) el modelo se ve en T-pose porque el Animator no se evalúa; es normal.
- `ProximityInteractable` (`Scripts/Interaction`): detecta por distancia horizontal (3 m) si el jugador está cerca y emite `onPlayerEnter`, `onPlayerExit` y `onInteract` (tecla E, una vez por pulsación). Muestra el texto "Presiona E para hablar" con `OnGUI`. No contiene lógica de lecciones.
- `SenaPresenter` (`Scripts/NPC`): solo presentación; gira suavemente hacia el jugador al acercarse y vuelve a su orientación original al alejarse.
- La lógica de lecciones debe suscribirse a los eventos de `ProximityInteractable`, sin modificar `SenaPresenter`.
- No hay diálogos, evaluación de señas ni MediaPipe, ni poses de LSC.

## Regenerar la escena

`Assets/Project/Editor/LuminaSceneBuilder.cs` reconstruye el contenido desde cero (borra `--- LUMINA ---` y `Player`, y vuelve a crearlos). **No ejecutarlo si se hicieron ajustes manuales en la escena**: se perderían, incluidos los colores provisionales (habría que volver a aplicarlos).

- Menú: **Lumina → Construir escena LuminaWorld**.
- Batchmode (con Unity cerrado): `Unity.exe -batchmode -quit -projectPath <ruta> -executeMethod Lumina.EditorTools.LuminaSceneBuilder.Build`

## Abrir y probar

1. Abrir el proyecto con Unity 6000.0.34f1.
2. Abrir `Assets/Project/Scenes/LuminaWorld.unity`.
3. Pulsar Play, caminar por el camino hacia Sena con W y el ratón; pulsar E al llegar a menos de 3 m.

`LuminaWorld` no está en *Build Settings* (la lista actual contiene solo `Menu`); añadirla si se va a cargar desde el menú.

## Pruebas realizadas

Ejecutadas con Unity 6000.0.34f1 en batchmode. La prueba en Play usó un teclado y un ratón virtuales del Input System sobre la escena real (no entrada física) y se cerró sin errores de consola.

| Prueba | Resultado |
|---|---|
| El proyecto compila sin errores de C# | Correcto |
| Una sola cámara y un solo `AudioListener` | Correcto (1 y 1) |
| Un solo suelo (`GroundPlane`) | Correcto |
| Gravedad: el jugador cae al suelo y queda apoyado | Correcto (y ≈ 0.03) |
| W: avance a 4.0 m/s | Correcto |
| W + Shift: avance a 6.4 m/s | Correcto |
| A / D / S: desplazamiento lateral y hacia atrás | Correcto |
| Ratón: +100 px en X giran +10° el jugador | Correcto |
| Ratón: +100 px en Y inclinan +10° la cámara; límite ±80° | Correcto |
| Esc libera el cursor y detiene el movimiento | Correcto |
| Clic izquierdo recupera el cursor | Correcto |
| Colisión con House_1 (se detiene en x ≈ -13.1) | Correcto |
| Colisión con Sena (se detiene en z ≈ 1.2, Sena en z = 2) | Correcto |
| `PlayerInRange` falso a 5.5 m y a 3.1 m; verdadero a 2.9 m | Correcto |
| E fuera de rango no dispara la interacción | Correcto (0) |
| E mantenida 15 frames dentro de rango dispara 1 sola interacción | Correcto |
| Dos pulsaciones separadas disparan 2 interacciones | Correcto |
| Sena gira hacia el jugador (producto escalar 1.0 tras 2 s) | Correcto |
| Sena recupera su orientación original al alejarse | Correcto |
| `Animator` de Sena con controlador válido; clip activo `Armature\|Idle_00` | Correcto |
| Sena: 4/4 `SkinnedMeshRenderer` activos en Play | Correcto |
| Errores de consola durante la prueba en Play | 0 |
| Renders (cámara del jugador y vista general): casas, plaza, camino, Sena a escala coherente | Correcto, con la salvedad indicada abajo |

Nota sobre la prueba en Play: en batchmode `Application.isFocused` es falso. El cursor aparecía capturado al empezar, por lo que la prueba no necesitó forzarlo.

## Pendientes (requieren comprobación manual en Unity con ventana)

- Confirmar visualmente en el Game view que los modelos de la aldea muestran los colores provisionales: en las capturas en batchmode el primer render de cada sesión salió todo marrón y los siguientes con los colores correctos (parece un artefacto del primer frame; no se pudo confirmar la causa).
- Ver el texto "Presiona E para hablar": se comprobó el estado `PlayerInRange` y los eventos, pero no el dibujado del `OnGUI`.
- Revisar a mano la sensación de movimiento/ratón (sensibilidad 0.1, velocidad 4) con teclado y ratón físicos.
- Comprobar que los colliders de las copas de árboles no bloquean al jugador en puntos inesperados.
- Conseguir la textura de paleta original del paquete *Low Poly Fantasy Village* y, si se obtiene, quitar los colores provisionales.
- `CharacterController.isGrounded` oscila entre verdadero y falso con el jugador en reposo (comportamiento habitual); no afecta a nada actual, pero debe tenerse en cuenta si se añade un salto.

## Problemas conocidos

- **Modelos de la aldea sin su paleta original** (ver "Materiales"); se usan colores provisionales.
- `Holotna/Mountain`: varios materiales de Shader Graph (árbol 01A, arbusto, roca) se renderizaron en magenta en batchmode; no se investigó la causa y no se usan en la escena.
- Los cambios sin commitear que ya existían en `Menu.unity` y `ProjectSettings/EditorBuildSettings.asset`, y los `.meta` modificados por la reimportación, no forman parte de esta tarea.
