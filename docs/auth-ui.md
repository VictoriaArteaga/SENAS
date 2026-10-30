# Acceso a Lúmina: LoginScene, RegisterScene y cierre de sesión

> **Alcance:** solo interfaz y navegación **local**. No hay autenticación real, Firebase, Firestore, backend, APIs ni base de datos. Nada se guarda en disco ni en `PlayerPrefs`.

Rama de trabajo: `feature/one-level` · Unity 6000.0.34f1 · URP · Input System · TextMeshPro (sin paquetes nuevos).

## Flujo

```
LoginScene ──Crear cuenta──────────▶ RegisterScene
    │  ▲                                  │
    │  └────────Volver────────────────────┘
    ├─Iniciar sesión (simulado)─────────▶ MainMenu ◀──Crear cuenta (simulado) + Bienvenida
    └─¿Olvidé mi contraseña? ─▶ aviso simulado

MainMenu ──JUGAR──▶ LuminaWorld ──E con Sena──▶ presentación ──COMENZAR──▶ Nivel 1 (AlphabetHouse)
                      └ el avatar elegido en el registro es el cuerpo del jugador

MainMenu ──CERRAR SESIÓN──▶ ventana «¿Te vas de Lúmina?»
                              ├─Continuar jugando / Esc ─▶ sigue en MainMenu
                              └─Sí, cerrar sesión ───────▶ LoginScene
```

`LoginScene` y `RegisterScene` están al inicio de Build Settings. La navegación existente (`MainMenu → LuminaWorld → AlphabetHouse`) no cambió. Las escenas vacías `Login.unity` y `Register.unity` que ya existían en el proyecto (marcadores de posición de la Fase 1) no se tocaron ni se registraron.

## Avatar elegido → LuminaWorld

1. En `RegisterScene`, **SELECCIONAR** llama a `AuthSession.SelectAvatar(id)` (`luna`, `nube`, `rio`, `sol`). Al terminar el registro `AuthSession.SignIn(nombre, id)` lo confirma.
2. `AuthSession.AvatarId` vive **solo en memoria** y sobrevive a «cerrar sesión» (no hay cuentas persistentes, así el flujo Registro → Login → Mundo conserva la elección hasta cerrar el juego o elegir otro).
3. `LuminaWorld` tiene `PlayerAvatar` en el objeto `Player`. Al cargar, busca `AuthSession.AvatarId` en `Data/AvatarCatalog.asset` e instancia el prefab `Prefabs/Avatars/Avatar_<id>` como hijo del jugador (0,2 m detrás de la cámara). El controlador en primera persona **no cambió**: el cuerpo se ve al mirar hacia abajo y por su sombra.
4. Sin avatar elegido, o con un id desconocido, no se instancia nada (comportamiento anterior) y no hay excepciones.

## Sena → COMENZAR → Nivel 1

El último botón de la presentación de Sena decía «¡LISTO!» y solo cerraba el diálogo; el Nivel 1 se abría únicamente por la puerta de la casa (E). Ahora el botón dice **COMENZAR** y `SenaWelcomeInteraction.onWelcomeCompleted` llama a `SceneEntrance.Enter` (escena `AlphabetHouse`, la misma que usa la puerta). La puerta sigue funcionando.

## Datos de prueba controlados

Cualquier dato con formato válido entra (correo con forma `a@b.co` y contraseña de 6 o más caracteres). Valores reservados para ensayar errores:

| Dato | Efecto |
|---|---|
| correo `error@lumina.test` | Inicio de sesión: «credenciales inválidas». Registro: «correo en uso». |
| usuario `usado` (cualquier caso) | Registro: «nombre de usuario ya tomado». |

Todo está en `AuthenticationMockService`, que **no guarda contraseñas ni cuentas**.

## Arquitectura

| Capa | Archivos (`Assets/Project/Scripts/…`) | Responsabilidad |
|---|---|---|
| Servicio (sin Unity) | `Auth/IAuthenticationService.cs`, `Auth/AuthenticationMockService.cs`, `Auth/AuthServices.cs`, `Auth/AuthValidation.cs` | Contrato, simulación local, validaciones y sesión en memoria. Ensamblado propio `Lumina.Auth` (`noEngineReferences`). |
| Interfaz | `UI/Auth/LoginUIController.cs`, `RegisterUIController.cs`, `AvatarSelectorController.cs`, `LogoutModalController.cs` | Coordinan los controles; solo conocen `AuthServices.Current`. |
| Componentes | `UIButtonAnimator`, `AuthField`, `StatusMessage`, `LoadingDots`, `SenaSpeaker`, `AmbientFloater`, `ScreenFader`, `LogoutSenaStage` | Hover/Pressed, campos con error, estado, carga, Sena, adornos, fundido, escenario de Sena. |
| Editor | `Editor/LuminaUiKit.cs`, `Editor/LuminaAuthSceneBuilder.cs` | Construyen las escenas de forma repetible (`Lumina ▸ Autenticación ▸ Construir todo`). |

**Cambiar a un servicio real:** implementar `IAuthenticationService` (p. ej. en `Scripts/Firebase`) y asignarlo una vez al arrancar: `AuthServices.Current = new MiServicioReal();`. Las pantallas no cambian.

## Recursos reutilizados

- Cielo, prefabs de aldea (`Low Poly Fantasy Village`) y los **mismos materiales tintados** `Lumina_Village_*` que usa LuminaWorld (se añaden `Rock` y `Mountain`).
- **Sena** (`Prefabs/Sena.prefab`) con su Animator (`Wave`, `Clap`).
- **Burbuja de diálogo de Sena** copiada de LuminaWorld (`WelcomeBubble`) a `Prefabs/SenaSpeechBubble.prefab` sin su botón ni contador. La escena de origen no se modificó.
- Avatares de **Sunbox Avatars** (paquete existente): cuatro variantes en `Prefabs/Avatars/` (Luna, Nube, Río, Sol).
- Sprites `RoundedRect`/`Circle`, tipografía **Liberation Sans SDF** y paleta del menú principal.

## Prefabs y materiales nuevos

`SenaSpeechBubble`, `Avatars/Avatar_{luna,nube,rio,sol}`, `LogoutModal`, `LogoutSenaStage`; materiales en `Materials/Auth/` y `Lumina_Village_Rock/Mountain`. **Modelos 3D nuevos: ninguno.**

## Pruebas

- **EditMode (Unity Test Framework):** `Assets/Project/Tests/EditMode/AuthTests.cs` (validaciones y servicio simulado). Ejecutar con *Window ▸ General ▸ Test Runner ▸ EditMode*, o en consola:
  `Unity.exe -projectPath <ruta> -batchmode -runTests -testPlatform EditMode -testResults editmode.xml`.
- **Recorrido manual (≈5 min):**
  1. Abrir `LoginScene`, Play. Pulsar *INICIAR SESIÓN* vacío → errores y Sena dice «¡Ups!».
  2. Escribir `abc` como correo → error de formato; contraseña `123` → «al menos 6». *VER/OCULTAR* alterna la contraseña.
  3. `ana@correo.com` / `secreto1` → carga, éxito, entra a MainMenu.
  4. *CERRAR SESIÓN* → *CONTINUAR JUGANDO* (o Esc) cierra; *SÍ, CERRAR SESIÓN* vuelve a Login.
  5. *CREAR CUENTA* → completar los 5 campos, **SELECCIONAR** un avatar (flechas, clic o ← →) → *CREAR CUENTA* → bienvenida → *IR AL MENÚ PRINCIPAL*.
  6. *JUGAR* en MainMenu → LuminaWorld sigue funcionando.

## Mensajes de validación y layout

Los campos (`AuthField`) apilan etiqueta, cuadro y mensaje con un `VerticalLayoutGroup` dentro de un formulario (`Form`, otro layout vertical). El mensaje está desactivado mientras no hay error, así que no ocupa espacio; al aparecer, lo que hay debajo se desplaza, y al desaparecer el espacio se recupera. El texto del error es de una línea y se reduce para caber (autoajuste), por lo que no puede desbordar sobre el campo siguiente. Mensajes del correo: «Escribe tu correo electrónico.», «El correo debe tener un @.» y «Revisa el formato: nombre@correo.com».

## Limitaciones conocidas

- Las contraseñas y cuentas no existen: cerrar el juego borra la «sesión».
- «Recordar sesión» es solo visual.
- La personalización del avatar se limita a elegir entre cuatro variantes; no se envía a ningún perfil.
