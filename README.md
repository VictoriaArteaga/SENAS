# Señas: El mundo de las manos

> Videojuego educativo 3D para aprender y practicar la **Lengua de Señas Colombiana (LSC)** con retroalimentación en tiempo real mediante visión artificial.

---

## Descripción

La mayoría de los recursos para aprender LSC (imágenes, textos y videos) permiten *ver* cómo se hace una seña, pero no dicen si el estudiante la ejecuta bien. En **Señas** el jugador:

1. Crea una cuenta y elige su avatar.
2. Explora el mundo de **Lúmina** en primera persona.
3. Habla con **Sena**, la guía, y entra a las casas temáticas.
4. Ve la demostración de cada seña.
5. La repite frente a la cámara y recibe una **puntuación de precisión** con estrellas.

## Estado actual del desarrollo

### Ya funciona

| Área | Qué hay |
|---|---|
| **Cuentas** | Registro, inicio de sesión y cierre de sesión contra el backend real (JWT con refresh tokens rotativos). Pantallas de login y registro animadas. |
| **Avatares** | Selección de avatar al registrarse y cambio posterior desde el perfil. Catálogo de avatares migrado al paquete Avatars2. |
| **Mundo Lúmina** | Escena explorable en primera persona con plaza central, caminos, casas, vegetación y cielo. Control de jugador, interacción por proximidad y salida protegida de las casas. |
| **Sena** | Personaje guía con diálogos de bienvenida y burbujas de conversación. |
| **Casas de aprendizaje** | Cuatro casas jugables: **Abecedario**, **Números (0–30)**, **Meses** y **Saludos y frases**. Cada una tiene selector de elementos, pantalla de demostración, práctica y avisos de nivel anterior/siguiente. |
| **Reconocimiento** | Captura de cámara y reconocimiento de manos con MediaPipe. Reconocedores propios para números y saludos (secuencias de manos con referencias de cabeza y torso). |
| **Práctica y resultados** | Flujo Práctica → Resultados con puntuación, estrellas, XP y reintento. |
| **Progreso** | Perfil con estadísticas, progreso por nivel y desbloqueo progresivo de niveles guardado en el servidor. |
| **Menú y ajustes** | Menú principal, ajustes (idioma, audio) y tema visual unificado. |
| **Idiomas** | Interfaz y textos de Sena en **español e inglés**. |
| **Backend** | API REST (NestJS + PostgreSQL/Supabase) con usuarios, perfiles, catálogo (4 niveles, 81 señas), progreso, XP, desbloqueos e historial. El backend solo recibe el *resultado* de cada evaluación, nunca imágenes. |

### Pendiente

- Personajes secundarios (Ana, Mateo y Luna) y sus diálogos.
- Contenido completo de los niveles 2, 3 y 4 (familia, frases y situaciones, reto universitario).
- Personalización avanzada del avatar (ropa, accesorios, rasgos) y tienda de desbloqueables con XP.
- Evaluación por frases con puntuación por componente.
- Pruebas con usuarios y ajuste de umbrales de puntuación.
- Build de distribución.

## La historia

En el mundo de **Lúmina**, una antigua energía llamada **La Voz de las Manos** está desapareciendo. El jugador recorre la región, aprende las señas que permiten comunicarse con sus habitantes y recupera los fragmentos de esa energía hasta restaurarla.

> *La comunicación no depende únicamente de la voz. También podemos comunicarnos con nuestras manos, nuestros gestos y nuestras expresiones.*

## Niveles de diseño

| Nivel | Contenido previsto |
|---|---|
| **1. Primeras señas** | Abecedario, números, saludos, meses *(desarrollado)* |
| **2. Mi mundo** | Familia, colegio, comida, animales, emociones |
| **3. Situaciones cotidianas** | Frases como "Necesito ayuda" o "¿Dónde está el colegio?" |
| **4. Reto universitario** | Universidad, trabajo, tecnología, salud |

La progresión pedagógica es **señas individuales → palabras → frases → situaciones comunicativas**. El corpus inicial apunta a 30–40 señas y frases; el catálogo del backend ya contiene 81 entradas.

## Bucle principal del juego

```
EXPLORAR → HABLAR CON SENA → ENTRAR A UNA CASA → VER LA SEÑA
    → PRACTICAR → CÁMARA CAPTURA → MEDIAPIPE ANALIZA
    → PUNTUACIÓN → ¿LO HIZO BIEN?
                      ├── Sí → Estrellas + XP + desbloquea siguiente
                      └── No → Reintenta
```

## Sistema de puntuación

| Precisión | Resultado |
|---|---|
| 90 – 100 % | ★★★ |
| 75 – 89 % | ★★ |
| 60 – 74 % | ★ |
| < 60 % | Reintentar |

## Arquitectura

```
Unity (cliente)                              Backend (NestJS)
├─ UI y escenas                              ├─ auth (JWT + refresh rotativo, argon2id)
├─ MediaPipe → landmarks → evaluador   ───►  ├─ profiles / users
│  (la IA corre dentro de Unity)             ├─ levels / signs (catálogo)
└─ Api/ (ApiClient, ProgressStore, …)        ├─ evaluations (resultados)
                                             └─ progress (XP, desbloqueos, historial)
                                                        │
                                                   PostgreSQL (Supabase)
```

El proyecto **no entrena un modelo desde cero**: usa modelos de visión artificial preentrenados (MediaPipe) y construye encima un sistema propio de evaluación por patrón de referencia (posición, orientación, forma y trayectoria).

## Tecnologías

| Componente | Tecnología |
|---|---|
| Motor de juego | Unity **6000.0.34f1**, URP, Input System |
| Lenguaje cliente | C# |
| Visión artificial | MediaPipe (manos, rostro y torso) |
| Backend | NestJS · TypeScript · TypeORM · PostgreSQL (Supabase) · Swagger · Jest |
| Plataforma | PC con cámara web |

## Estructura del repositorio

```
├── Assets/
│   └── Project/
│       ├── Scenes/      # Login, Register, MainMenu, LuminaWorld, casas, Practice, Results, Profile, Settings
│       └── Scripts/     # Alphabet, Api, Auth, Core, Interaction, NPC, Player, Signs, UI
├── backend/             # API REST (NestJS)
├── docs/                # Documentación técnica y capturas
├── Packages/
├── ProjectSettings/
└── README.md
```

## Instalación y ejecución

**Requisitos:** Unity 6000.0.34f1, Node.js 22+, un proyecto de Supabase y una cámara web.

```bash
# 1. Clonar
git clone https://github.com/VictoriaArteaga/<repositorio>.git

# 2. Backend
cd backend
npm ci
cp .env.example .env      # completar variables (ver backend/README.md)
npm run start:dev         # http://localhost:3000/api/v1
```

3. Abrir la carpeta raíz con Unity Hub.
4. Abrir **siempre la escena `LoginScene`**, registrarse o iniciar sesión y jugar.

Guías detalladas:

- [Ejecutar el juego con el backend en local](docs/unity-con-backend.md)
- [Documentación del backend](backend/README.md) y [contrato de integración](backend/docs/integration-contract.md)
- [Escena Lúmina](docs/lumina-scene.md) · [UI de autenticación](docs/auth-ui.md)

## Hoja de ruta

- [x] Documento de concepto (GDD)
- [x] Prototipo de captura de manos con MediaPipe
- [x] Sistema de puntuación por referencia (números y saludos)
- [x] Backend con cuentas, progreso, XP y desbloqueos
- [x] Avatares, Sena y mundo Lúmina explorable
- [x] Nivel 1 jugable (abecedario, números, meses, saludos)
- [x] Interfaz bilingüe (ES/EN)
- [ ] Niveles 2, 3 y 4
- [ ] Personajes secundarios y personalización avanzada
- [ ] Pruebas con usuarios y ajustes

## Autoría

Proyecto académico desarrollado por **María Victoria Arteaga Revelo** y **Karol Isabella Cabrera Pantoja**, estudiantes de Ingeniería de Software, Universidad Cooperativa de Colombia, Campus Pasto.
