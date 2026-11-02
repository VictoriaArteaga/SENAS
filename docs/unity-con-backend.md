# Ejecutar el juego con el backend en local

El juego (Unity) ya no guarda nada en el equipo: cuentas, perfil, progreso y resultados viven en el backend
(`backend/`, NestJS + Supabase). Para jugar hace falta tenerlo encendido.

## Pasos

1. **Backend** (una vez): `cd backend`, `npm ci`, y crea `backend/.env` a partir de `backend/.env.example`
   (ver `backend/README.md`; la contraseña de Supabase y el secreto JWT van solo en ese archivo).
2. **Arrancarlo:** `npm run start:dev` en `backend/`. Comprueba `http://localhost:3000/api/v1/health` → `"status":"ok"`.
   Si otro programa usa el puerto 3000, cambia `PORT` en `backend/.env` y la URL en el paso 3.
3. **URL del juego:** `Assets/Project/Data/Resources/ApiConfig.asset` → `developmentBaseUrl`
   (por defecto `http://localhost:3000/api/v1`). Ver `backend/docs/integration-contract.md`, sección 10.3.
4. **Unity 6000.0.34f1:** abre el proyecto y empieza **siempre desde la escena `LoginScene`** (las demás escenas necesitan
   sesión para guardar progreso). Regístrate o inicia sesión y juega.

## Si algo falla

| Síntoma | Causa probable |
| --- | --- |
| «No hay conexión con el servidor» al entrar | El backend no está encendido o la URL/puerto de `ApiConfig` no coincide. |
| «El juego no sabe dónde está el servidor» | `developmentBaseUrl` vacía (o `productionBaseUrl` vacía en una build de release). |
| Error de HTTP bloqueado en Unity | `Player Settings → Allow downloads over HTTP` debe ser *Allowed in development builds*. |
| «Tu sesión terminó» y vuelve a Login | La sesión se cerró en otro lado, expiró o se detectó reutilización de un refresh token. |
| El abecedario solo deja ver la A | No se pudo cargar tu progreso (sin sesión o sin conexión). |
| Quiero trabajar en la interfaz sin backend | Activa `useMockAuthentication` en `ApiConfig`. El progreso y las prácticas no se guardan en ese modo. |

## Pruebas

- **Unity (EditMode):** Window → General → Test Runner → EditMode → Run All (69 pruebas), o en consola:
  `Unity.exe -batchmode -nographics -projectPath <ruta> -runTests -testPlatform EditMode -testResults results.xml`
  (con el Editor cerrado).
- **Backend:** `cd backend && npm test && npm run test:e2e` (el e2e necesita Docker; ver `backend/README.md`).
