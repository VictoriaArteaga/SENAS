# Señas: El mundo de las manos

> Videojuego educativo 3D para aprender y practicar la **Lengua de Señas Colombiana (LSC)** con retroalimentación en tiempo real mediante visión artificial.
---

## Descripción

**Señas: El mundo de las manos** propone una forma más activa de aprender LSC. La mayoría de los recursos disponibles (imágenes, textos y videos) permiten *ver* cómo se hace una seña, pero no dicen si el estudiante la está ejecutando bien.

En este juego el jugador:

1. Crea y personaliza su avatar.
2. Recorre distintos escenarios del mundo de **Lúmina**.
3. Ve cómo **Sena**, el personaje guía, demuestra cada seña con animaciones 3D.
4. Repite la seña frente a la cámara.
5. Recibe una **puntuación de precisión** y retroalimentación inmediata para mejorar.

## Objetivo

Desarrollar una experiencia de videojuego que combine **educación, interacción 3D y reconocimiento de movimiento** para ofrecer una alternativa más participativa al aprendizaje de la LSC.

## Características principales

- 🧍 **Avatar personalizable** (apariencia, tono de piel, cabello, ropa, accesorios y rasgos faciales).
- 🧑‍🏫 **Sena, personaje guía** que explica, demuestra, motiva y desbloquea niveles.
- 📷 **Reconocimiento de manos y rostro** en tiempo real con MediaPipe.
- 📊 **Evaluación por patrón de referencia**: no compara solo una imagen, sino posición, orientación, forma, movimiento y trayectoria.
- ⭐ **Gamificación**: estrellas, puntos de experiencia (XP), niveles desbloqueables y perfil de progreso.
- 🗺️ **Dificultad progresiva**: de señas individuales a frases y situaciones comunicativas.

## La historia

En el mundo de **Lúmina**, una antigua energía llamada **La Voz de las Manos** está desapareciendo. Esta energía representa la capacidad de las personas para comunicarse, comprenderse y conectarse.

El jugador debe recorrer las regiones de Lúmina, aprender las señas que permiten comunicarse con sus habitantes y recuperar los fragmentos de La Voz de las Manos hasta restaurarla por completo.

> *La comunicación no depende únicamente de la voz. También podemos comunicarnos con nuestras manos, nuestros gestos y nuestras expresiones.*

## Personajes

| Personaje | Rol |
|---|---|
| **Jugador** | Protagonista, con avatar personalizable |
| **Sena** | Guía: explica reglas, enseña señas, da retroalimentación y desbloquea niveles |
| **Ana** | Estudiante que necesita ayuda para encontrar un objeto |
| **Mateo** | Plantea desafíos con el vocabulario aprendido |
| **Luna** | Niña que introduce nuevas palabras y expresiones |

Los personajes secundarios funcionan con diálogos y eventos predefinidos (sin sistemas complejos de IA).

## Niveles

```
                    LÚMINA
                       │
     ┌─────────┬───────┴───────┬──────────┐
  NIVEL 1   NIVEL 2        NIVEL 3     NIVEL 4
  Primeras  Mi mundo       Situaciones Reto
  señas                    cotidianas  universitario
```

| Nivel | Público aprox. | Contenido | Señas |
|---|---|---|---|
| **1. Primeras señas** | 10–14 años | Alfabeto, números, colores, saludos, sí/no, gracias, por favor | 10 |
| **2. Mi mundo** | 15–18 años | Familia, amigos, colegio, comida, animales, lugares, emociones, actividades | 10 |
| **3. Situaciones cotidianas** | General | Combinaciones de señas: "Necesito ayuda", "Quiero agua", "¿Dónde está el colegio?" | 10 |
| **4. Reto universitario** | Universitarios | Universidad, trabajo, tecnología, salud, presentaciones, situaciones profesionales | 10 |

La progresión pedagógica es: **señas individuales → palabras → frases → situaciones comunicativas**.

> **Alcance:** el proyecto no pretende enseñar toda la LSC. Se plantea un corpus inicial de **30 a 40 señas y frases**, lo que permite desarrollar y evaluar el sistema de forma realista.

## Bucle principal del juego

```
EXPLORAR → CONOCER NPC → APRENDER SEÑA → AVATAR LA DEMUESTRA
    → JUGADOR PRACTICA → CÁMARA CAPTURA → MEDIAPIPE ANALIZA
    → EVALUACIÓN → PUNTUACIÓN → ¿LO HIZO BIEN?
                                    ├── Sí → Desbloquea siguiente
                                    └── No → Reintenta
```

## ¿Dónde entra la inteligencia artificial?

El proyecto **no entrena un modelo desde cero**: usa modelos de visión artificial preentrenados y construye encima un sistema propio de evaluación.

**Módulo 1 – Avatar (sin IA)**

```
Seña → Animación previamente creada → Animator de Unity → Avatar realiza la seña
```

**Módulo 2 – Reconocimiento (con visión artificial)**

```
Cámara → MediaPipe → Landmarks de manos y rostro
       → Procesamiento del movimiento → Comparación con patrón esperado
       → Puntuación → Retroalimentación
```

### Patrón de referencia por seña

Cada seña se describe con un patrón que considera:

```
SEÑA: "HOLA"
├── Mano derecha
│   ├── Posición inicial
│   ├── Orientación
│   ├── Forma
│   ├── Trayectoria
│   └── Posición final
└── Rostro
    ├── Expresión
    └── Orientación
```

## Sistema de puntuación

| Precisión | Resultado |
|---|---|
| 90 – 100 % | ★★★ |
| 75 – 89 % | ★★ |
| 60 – 74 % | ★ |
| < 60 % | Reintentar |

Cada seña correcta otorga **XP** (por ejemplo, `+100 XP`) que permite desbloquear nuevas zonas, ropa, accesorios, personajes y señas.

**Ejemplo de retroalimentación:**

```
Seña: HOLA       ★★★★☆   Precisión: 86 %
"¡Muy bien! Tu movimiento fue correcto.
 Intenta mantener la mano un poco más cerca del rostro."
```

En el nivel 3 el sistema evalúa cada componente de la frase:

```
HOLA        ✓ 94 %
AYUDA       ✓ 87 %
BIBLIOTECA  ~ 72 %
Puntuación: 84 %
```

## Tecnologías

| Componente | Tecnología |
|---|---|
| Motor de juego | Unity |
| Lenguaje | C# |
| Visión artificial | MediaPipe (Hands + Face Mesh) |
| Plataforma | PC (con cámara web) |
| Estilo visual | 3D estilizado, colorido y amigable |

## Estructura del repositorio

> Estructura propuesta; ajústala a medida que avance el desarrollo.

```
senas-el-mundo-de-las-manos/
├── Assets/
│   ├── Animations/      # Animaciones de las señas
│   ├── Characters/      # Avatar, Sena y NPC
│   ├── Scenes/          # Mundos y niveles
│   ├── Scripts/         # Lógica del juego, evaluación y UI
│   └── ...
├── Docs/                # Documento de concepto (GDD) y diagramas
├── Packages/
├── ProjectSettings/
└── README.md
```

## Instalación y ejecución

> El proyecto se encuentra en fase de diseño. Estas instrucciones se completarán cuando exista una primera versión funcional.

**Requisitos previos**

- Unity (versión por definir)
- Cámara web
- PC con capacidad suficiente para ejecutar Unity y MediaPipe en tiempo real

**Pasos (previstos)**

```bash
# 1. Clonar el repositorio
git clone https://github.com/<usuario>/<repositorio>.git

# 2. Abrir la carpeta con Unity Hub
# 3. Instalar el plugin de MediaPipe para Unity
# 4. Abrir la escena principal y ejecutar
```

## Hoja de ruta

- [x] Documento de concepto (GDD)
- [ ] Definición del corpus de 30–40 señas
- [ ] Prototipo de captura de manos con MediaPipe
- [ ] Sistema de patrones de referencia y puntuación
- [ ] Avatar, Sena y animaciones de señas
- [ ] Nivel 1 jugable
- [ ] Niveles 2, 3 y 4
- [ ] Sistema de progreso, XP y personalización
- [ ] Pruebas con usuarios y ajustes


## Autoría

Proyecto académico desarrollado por **María Victoria Arteaga Revelo** y **Karol Isabella Cabrera Pantoja**, estudiantes de Ingeniería de Software, Universidad Cooperativa de Colombia, Campus Pasto.
