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

MainMenu ──CERRAR SESIÓN──▶ ventana «¿Te vas de Lúmina?»
                              ├─Continuar jugando / Esc ─▶ sigue en MainMenu
                              └─Sí, cerrar sesión ───────▶ LoginScene
```

`LoginScene` y `RegisterScene` están al inicio de Build Settings. La navegación existente (`MainMenu → LuminaWorld → AlphabetHouse`) no cambió. Las escenas vacías `Login.unity` y `Register.unity` que ya existían en el proyecto (marcadores de posición de la Fase 1) no se tocaron ni se registraron.

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

## Limitaciones conocidas

- Las contraseñas y cuentas no existen: cerrar el juego borra la «sesión».
- «Recordar sesión» es solo visual.
- La personalización del avatar se limita a elegir entre cuatro variantes; no se envía a ningún perfil.
