<p align="center">
  <a href="http://nestjs.com/" target="blank"><img src="https://nestjs.com/img/logo-small.svg" width="120" alt="Nest Logo" /></a>
</p>

[circleci-image]: https://img.shields.io/circleci/build/github/nestjs/nest/master?token=abc123def456
[circleci-url]: https://circleci.com/gh/nestjs/nest

  <p align="center">A progressive <a href="http://nodejs.org" target="_blank">Node.js</a> framework for building efficient and scalable server-side applications.</p>
    <p align="center">
<a href="https://www.npmjs.com/~nestjscore" target="_blank"><img src="https://img.shields.io/npm/v/@nestjs/core.svg" alt="NPM Version" /></a>
<a href="https://www.npmjs.com/~nestjscore" target="_blank"><img src="https://img.shields.io/npm/l/@nestjs/core.svg" alt="Package License" /></a>
<a href="https://www.npmjs.com/~nestjscore" target="_blank"><img src="https://img.shields.io/npm/dm/@nestjs/common.svg" alt="NPM Downloads" /></a>
<a href="https://circleci.com/gh/nestjs/nest" target="_blank"><img src="https://img.shields.io/circleci/build/github/nestjs/nest/master" alt="CircleCI" /></a>
<a href="https://discord.gg/G7Qnnhy" target="_blank"><img src="https://img.shields.io/badge/discord-online-brightgreen.svg" alt="Discord"/></a>
<a href="https://opencollective.com/nest#backer" target="_blank"><img src="https://opencollective.com/nest/backers/badge.svg" alt="Backers on Open Collective" /></a>
<a href="https://opencollective.com/nest#sponsor" target="_blank"><img src="https://opencollective.com/nest/sponsors/badge.svg" alt="Sponsors on Open Collective" /></a>
  <a href="https://paypal.me/kamilmysliwiec" target="_blank"><img src="https://img.shields.io/badge/Donate-PayPal-ff3f59.svg" alt="Donate us"/></a>
    <a href="https://opencollective.com/nest#sponsor"  target="_blank"><img src="https://img.shields.io/badge/Support%20us-Open%20Collective-41B883.svg" alt="Support us"></a>
  <a href="https://twitter.com/nestframework" target="_blank"><img src="https://img.shields.io/twitter/follow/nestframework.svg?style=social&label=Follow" alt="Follow us on Twitter"></a>
</p>
  <!--[![Backers on Open Collective](https://opencollective.com/nest/backers/badge.svg)](https://opencollective.com/nest#backer)
  [![Sponsors on Open Collective](https://opencollective.com/nest/sponsors/badge.svg)](https://opencollective.com/nest#sponsor)-->

## Description

[Nest](https://github.com/nestjs/nest) framework TypeScript starter repository.

## Project setup

```bash
$ npm install
```

## Conexión con PostgreSQL (Supabase)

**Arquitectura:** Unity (C#) → API REST NestJS → Prisma ORM 7 → PostgreSQL en Supabase. Unity nunca accede a la base de datos directamente.

**Tecnologías:** NestJS 12, TypeScript (NodeNext), Prisma 7.10.1, `@prisma/adapter-pg`, `pg`, `@nestjs/config`.

**Estructura:**

- `prisma/schema.prisma`: generator `prisma-client` (salida `src/generated/prisma/`, ignorada por Git) y datasource PostgreSQL sin URL.
- `prisma7.config.ts`: configuración de Prisma 7; lee `DATABASE_URL`.
- `src/prisma/prisma.service.ts`: `PrismaClient` con `PrismaPg`; conecta en `onModuleInit` y desconecta en `onModuleDestroy`.
- `src/prisma/prisma.module.ts`: exporta `PrismaService` para inyectarlo en otros módulos.
- `src/prisma/check-connection.ts`: verificación de solo lectura (`SELECT 1`).

**Variables de entorno** (archivo `.env`, nunca versionado):

```
DATABASE_URL="postgresql://USUARIO:CONTRASENA@HOST:5432/postgres"
```

**Comandos:**

```bash
npx prisma validate
npx prisma generate     # genera el cliente en src/generated/prisma
npm run build
npm run db:check        # SELECT 1 contra la base de datos (requiere build previo)
npm run start:dev
```

**Estado:** conexión verificada contra Supabase con `SELECT 1`. Aún no hay modelos, migraciones ni tablas; el esquema se diseñará en una fase posterior.

## Autenticación y sesiones

Implementada en NestJS (sin Supabase Auth).

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| POST | `/auth/register` | — (límite 5/min/IP) | Crea Usuario + Perfil en una transacción (201). No inicia sesión. |
| POST | `/auth/login` | — (límite 10/min/IP) | Access token JWT (15 min) + refresh token y una fila en `sesiones` (200). |
| POST | `/auth/refresh` | refresh token (límite 30/min/IP) | Rota el refresh token y emite un access token nuevo (200). |
| POST | `/auth/logout` | refresh token | Revoca esa sesión. Idempotente: siempre 204. |
| POST | `/auth/logout-all` | `Bearer` access token | Revoca todas las sesiones activas del usuario del JWT (200). |
| GET | `/auth/me` | `Bearer` access token | Datos públicos de la cuenta y su perfil (200). |

**Refresh token:** `<sessionId>.<secreto>`. `sessionId` es el UUID de la fila de `sesiones` (no es secreto; permite
localizar la sesión aunque se presente un token antiguo); el secreto son 48 bytes aleatorios. En la base solo se guarda el
SHA-256 del token completo. La contraseña usa Argon2id.

**Rotación y reutilización:** cada `/auth/refresh` cambia el token mediante un único `UPDATE` condicional
(`id`, hash anterior, no revocada, no expirada), por lo que dos solicitudes simultáneas con el mismo token nunca consiguen
dos rotaciones. Si se presenta un token cuyo hash no es el vigente de su sesión —uno ya rotado (reutilización) o la solicitud
perdedora de una carrera— se **revoca esa sesión** y se responde 401. No se revocan las demás sesiones del usuario: cada una
es un dispositivo con su propio secreto, y revocar todo permitiría escalar el daño.
*Consecuencia para el cliente:* hay que **serializar las renovaciones** (una a la vez). Si la respuesta de un refresh se
pierde por la red y se reintenta con el token anterior, la sesión se cierra y el jugador debe iniciar sesión de nuevo.

**Expiración:** **absoluta**, 30 días desde el login (`REFRESH_TOKEN_EXPIRES_IN`). Renovar no la extiende.

**Revocación:** `logout`/`logout-all`/reutilización ponen `fecha_revocacion`; nunca se borran filas (el rol `senas_app` no
tiene `DELETE`). Un refresh token revocado o vencido no renueva. **Alcance:** revocar una sesión **no invalida** el access
token ya emitido, que sigue siendo válido hasta que expira (15 min). Lo único con efecto inmediato sobre los access tokens
es desactivar la cuenta (`is_active = false`): `JwtStrategy` comprueba en cada petición que la cuenta exista y esté activa.

**Rate limiting** (`@nestjs/throttler`): por IP y por ruta, solo en `login`, `register` y `refresh`; excedido devuelve `429`
con `Retry-After`. Configurable con `RATE_LIMIT_*`. Limitaciones: el contador está **en memoria de cada instancia** (con
varias instancias el límite efectivo se multiplica y se reinicia al reiniciar; para compartirlo haría falta un almacén común,
p. ej. Redis, que no existe aún); no hay límite por cuenta (evita bloquear a terceros) y detrás de un proxy/balanceador la IP
es la del proxy hasta configurar `trust proxy`. Variables obligatorias: `DATABASE_URL`, `JWT_ACCESS_SECRET` (mín. 32
caracteres, aleatoria): la API no arranca si faltan o son inseguras. Ver `.env.example`.

### Pruebas

```bash
npm test                      # unitarias (no necesitan base de datos)
# Integración: PostgreSQL local temporal y aislado, con roles, migración y seguridad reales
url=$(test/support/provision-test-db.sh up /ruta/temporal)   # requiere PG_BIN (carpeta bin de PostgreSQL)
TEST_DATABASE_URL="$url" npm run test:e2e
test/support/provision-test-db.sh down /ruta/temporal
```

Las pruebas nunca leen `.env` ni se conectan a Supabase. Sin `TEST_DATABASE_URL`, las de integración se omiten.
Jest se ejecuta con `--experimental-vm-modules` porque NestJS 12 se distribuye como ESM.

## Catálogo educativo (niveles y señas)

Módulos `levels` y `senas`: **solo consulta** del contenido que Unity necesita para presentar los niveles. El rol
`senas_app` solo tiene `SELECT` sobre `niveles`, `senas` y `nivel_senas`, así que no hay endpoints para crear, editar ni
borrar (el contenido se carga con el rol de migraciones). Todos los endpoints exigen `Authorization: Bearer <accessToken>`;
la identidad nunca se recibe por parámetros. Las respuestas son `{ "data": ... }`.

| Método | Ruta | Descripción | Códigos |
|---|---|---|---|
| GET | `/levels` | Niveles ordenados por `orden`, con la disponibilidad del jugador autenticado (ver «Desbloqueo de niveles») | 200, 401, 404 (perfil inexistente) |
| GET | `/levels/:id` | Detalle de un nivel | 200, 400 (UUID inválido), 401, 404 |
| GET | `/levels/:id/senas` | Señas del nivel en el orden de `NivelSena` (lista vacía si no tiene), **solo si el nivel está desbloqueado** para el jugador | 200, 400, 401, **403** (`LEVEL_LOCKED`), 404 (nivel o perfil inexistente) |
| GET | `/senas/:id` | Detalle de una seña, **solo si pertenece a algún nivel desbloqueado** para el jugador | 200, 400, 401, **403** (`LEVEL_LOCKED`), 404 (seña sin niveles, inexistente o perfil inexistente) |

Ejemplos (valores ilustrativos):

```jsonc
// GET /levels/:id
{ "data": { "id": "uuid", "numero": 1, "nombre": "…", "descripcion": null, "orden": 1, "xpRequerida": 0, "precisionMinima": 60 } }

// GET /levels/:id/senas  (cada elemento = campos de la seña + orden y esObligatoria del nivel)
{ "data": [ { "id": "uuid", "nombre": "…", "descripcion": null, "tipo": "SENA", "dificultad": "FACIL",
              "animacion": "…", "patronReferencia": { /* JSON tal como está almacenado */ },
              "orden": 1, "esObligatoria": true } ] }

// 404
{ "statusCode": 404, "message": "Nivel no encontrado.", "error": "Not Found" }
```

`precisionMinima` se entrega como número (en la base es `DECIMAL(5,2)`). `patronReferencia` y los demás campos JSON no se
transforman. No se exponen fechas internas ni relaciones. El catálogo es pequeño (30–40 señas), por eso no hay paginación; si
creciera, se añadiría `?limit=&cursor=` sobre `orden`. Cada petición usa un número constante de sentencias SQL (sin N+1).

**Pruebas de integración del catálogo:** como `senas_app` no puede escribir en esas tablas, `test/catalog.e2e-spec.ts` inserta
sus datos con el rol propietario `senas_migrator` (solo en la base local; rechaza cualquier otro host) y los borra al terminar;
la API siempre se ejecuta como `senas_app`. Se lanzan igual que el resto (ver «Pruebas»).

### Acceso al contenido de los niveles bloqueados

El bloqueo calculado en «Desbloqueo de niveles» se **aplica en el servidor**: solicitando directamente el id de un nivel o de
una seña no se obtiene contenido que el jugador (el del JWT) todavía no tiene desbloqueado. Las dos rutas reutilizan el mismo
cálculo que `GET /levels`; no hay una segunda implementación de las reglas. Para decidir el acceso hacen falta datos del perfil y
del progreso del jugador, por eso ambas pueden responder `404` si el usuario no tiene perfil.

- **`GET /levels/:id/senas`:** `400` si el UUID es inválido; `404` si el nivel no existe; `403` `LEVEL_LOCKED` si no lo tiene
  desbloqueado (no se consulta ni se entrega ninguna seña); `200` con las señas, o con `{ "data": [] }` si tiene acceso y el
  nivel no tiene señas.
- **`GET /senas/:id`:** una seña puede pertenecer a varios niveles (`NivelSena`). Se permite si **al menos uno** de sus niveles
  está desbloqueado; si todos están bloqueados, `403` `LEVEL_LOCKED`. Una seña que existe pero **no pertenece a ningún nivel** se
  trata como no disponible: `404` «Seña no encontrada.» (falla cerrado; ningún flujo del diseño usa señas fuera de un nivel).
- **`GET /levels` y `GET /levels/:id`** siguen mostrando los niveles bloqueados (nombre, requisitos, estado y motivos), porque el
  mapa del juego los necesita; lo que se protege es el contenido de las señas.

Respuesta `403` (el `code` es estable; el texto puede cambiar). No incluye motivos ni datos internos: se consultan en `GET /levels`:

```json
{ "statusCode": 403, "code": "LEVEL_LOCKED", "message": "El nivel todavía no está desbloqueado.", "error": "Forbidden" }
```

Para una seña, el mensaje es «La seña solo pertenece a niveles que todavía no están desbloqueados.».

| Código | Cuándo en estas dos rutas |
|---|---|
| 400 | El identificador de la ruta no es un UUID (`El identificador no es un UUID válido.`) |
| 401 | Sin JWT válido, token alterado o expirado, refresh token usado como access token, o cuenta inactiva |
| 403 | `LEVEL_LOCKED`: el nivel (o todos los niveles de la seña) no está desbloqueado para el jugador del JWT |
| 404 | Nivel o seña inexistente, seña sin niveles, o usuario sin perfil |

Ejemplos (valores ilustrativos):

```
GET /levels/<id-de-un-nivel-bloqueado>/senas        Authorization: Bearer <access token>   →  403  { "code": "LEVEL_LOCKED", … }
GET /senas/<seña-que-está-en-un-nivel-abierto>      Authorization: Bearer <access token>   →  200  { "data": { … } }
GET /senas/<seña-solo-de-niveles-bloqueados>        Authorization: Bearer <access token>   →  403  { "code": "LEVEL_LOCKED", … }
```

La identidad sale solo del JWT: `usuarioId` por la ruta, la consulta, el cuerpo o cabeceras se ignora. Las consultas son de
solo lectura y de número constante (el cálculo de acceso más las señas pedidas).

## Desbloqueo de niveles

`GET /levels` devuelve, además de los campos del catálogo, la disponibilidad de cada nivel **para el jugador del JWT**
(`estado`, `desbloqueado`, `motivosBloqueo`). Se calcula en cada petición a partir de datos existentes (`Perfil`, `Nivel`,
`NivelSena`, `ProgresoSena`); no se guarda ningún estado nuevo y la consulta es de solo lectura (no otorga XP, no registra
progreso y no cambia `Perfil.nivelActual`).

**Regla.** Un nivel se desbloquea cuando el jugador tiene la XP que ese nivel exige y demostró la precisión mínima en
**cada** seña obligatoria del nivel anterior:

```
desbloqueado(N₁) = XP(N₁)
desbloqueado(Nᵢ) = XP(Nᵢ) ∧ OBLIGATORIAS(Nᵢ₋₁) ∧ PRECISIÓN(Nᵢ₋₁) ∧ desbloqueado(Nᵢ₋₁)

XP(N)         ⇔ Perfil.totalXp ≥ N.xpRequerida                       (la XP exigida es la del nivel que se abre)
OBLIGATORIAS  ⇔ cada seña del nivel anterior con NivelSena.esObligatoria = true tiene ProgresoSena.completada = true
PRECISIÓN     ⇔ para cada una de esas señas, ProgresoSena.mejorPrecision ≥ precisionMinima DEL NIVEL ANTERIOR
```

- La precisión se comprueba **seña por seña**: un 100 % en una no compensa a otra por debajo del umbral, y
  `Perfil.precisionPromedio` no interviene. El umbral exacto cumple (`≥`).
- Las señas no obligatorias no cuentan, se hayan practicado o no. Un nivel anterior sin señas obligatorias solo exige XP.
- El primer nivel no tiene anterior: solo exige su propia XP.
- No se pueden saltar niveles: un nivel sigue bloqueado si el anterior lo está, aunque sus propias condiciones se cumplan.

**Estados** (`estado`; `desbloqueado` es `true` salvo en `BLOQUEADO`):

| Estado | Significado |
|---|---|
| `ACTUAL` | Nivel desbloqueado cuyo `numero` coincide con `Perfil.nivelActual` |
| `DESBLOQUEADO` | El jugador puede acceder, pero no es su nivel actual |
| `BLOQUEADO` | No cumple los requisitos; `motivosBloqueo` los lista todos |

**Motivos de bloqueo** (códigos estables, siempre en este orden; úsalos como identificadores, no los textos):

| Código | Cuándo |
|---|---|
| `XP_INSUFICIENTE` | `totalXp` es menor que la `xpRequerida` del nivel |
| `SENAS_OBLIGATORIAS_PENDIENTES` | Alguna seña obligatoria del nivel anterior no tiene registro de progreso o no está completada |
| `PRECISION_INSUFICIENTE` | Alguna seña obligatoria del nivel anterior tiene `mejorPrecision` por debajo de su `precisionMinima` |
| `NIVEL_ANTERIOR_BLOQUEADO` | El nivel anterior está bloqueado (progresión secuencial) |

Una seña obligatoria con registro pero sin completar y con precisión baja incumple las dos condiciones y aparecen ambos motivos.

**Interpretaciones del esquema** (no hay restricciones en la base de datos que las impongan):

- El nivel «anterior» es el que precede por **`orden`**, no por `numero`, por id ni por orden de inserción. Los huecos en `orden`
  o `numero` no importan. Si `numero` no crece con `orden`, la progresión sigue `orden` y el servidor lo anota en el log
  (`Catálogo inconsistente`, una vez por inconsistencia): conviene corregir la numeración del contenido.
- `Perfil.nivelActual` es un entero sin clave foránea; se interpreta como `Nivel.numero`. Si no coincide con ningún nivel, no hay
  `ACTUAL`; si apunta a un nivel bloqueado, ese nivel aparece `BLOQUEADO` con sus motivos reales.
- El backend todavía no modifica `nivelActual` (no hay módulo de evaluaciones), así que `ACTUAL` solo cambia si ese valor se actualiza.

Ejemplo (valores ilustrativos):

```json
{
  "data": [
    { "id": "uuid", "numero": 1, "nombre": "…", "descripcion": null, "orden": 1, "xpRequerida": 0, "precisionMinima": 60,
      "estado": "ACTUAL", "desbloqueado": true, "motivosBloqueo": [] },
    { "id": "uuid", "numero": 2, "nombre": "…", "descripcion": null, "orden": 2, "xpRequerida": 100, "precisionMinima": 75,
      "estado": "BLOQUEADO", "desbloqueado": false, "motivosBloqueo": ["XP_INSUFICIENTE", "PRECISION_INSUFICIENTE"] }
  ]
}
```

Errores: `401` sin JWT válido o con la cuenta inactiva; `404` `{ "statusCode": 404, "message": "Perfil de jugador no encontrado." }`
si el usuario no tiene perfil (no se crea automáticamente).

**Para el cliente (Unity):**

- Decide el acceso con `desbloqueado`; usa `estado` solo para presentar (`ACTUAL` se resalta) y traduce `motivosBloqueo` con tu propia
  tabla de textos por código.
- Vuelve a pedir `GET /levels` después de terminar un nivel o al abrir el menú: el resultado depende del progreso y de la XP actuales.
- Trata los códigos desconocidos como un bloqueo genérico (el servidor podría añadir motivos).
- Trata `403` con `code: "LEVEL_LOCKED"` en `GET /levels/:id/senas` y `GET /senas/:id` como «contenido aún bloqueado»: no reintentes;
  vuelve a pedir `GET /levels` para conocer los motivos. No dependas de ocultar el contenido en el cliente: el servidor lo impide.

## Compile and run the project

```bash
# development
$ npm run start

# watch mode
$ npm run start:dev

# production mode
$ npm run start:prod
```

## Run tests

```bash
# unit tests
$ npm run test

# e2e tests
$ npm run test:e2e

# test coverage
$ npm run test:cov
```

## Deployment

When you're ready to deploy your NestJS application to production, there are some key steps you can take to ensure it runs as efficiently as possible. Check out the [deployment documentation](https://docs.nestjs.com/deployment) for more information.

If you are looking for a cloud-based platform to deploy your NestJS application, check out [Mau](https://mau.nestjs.com), our official platform for deploying NestJS applications on AWS. Mau makes deployment straightforward and fast, requiring just a few simple steps:

```bash
$ npm install -g @nestjs/mau
$ mau deploy
```

With Mau, you can deploy your application in just a few clicks, allowing you to focus on building features rather than managing infrastructure.

## Observability

In production applications, observability is essential for understanding how your system behaves, detecting issues early, and maintaining reliable performance.

[NestJS Observe](https://observe.nestjs.com) automatically instruments your NestJS application, giving you deep visibility into your system with minimal setup:

- **Distributed tracing:** Follow requests across services and understand how they flow through your system.
- **Waterfall analysis:** Visualize request execution and identify slow operations, bottlenecks, and unexpected delays.
- **Performance analysis:** Analyze application performance in real time and quickly pinpoint areas that need optimization.
- **Metrics:** Track key application and infrastructure metrics to understand system health and performance trends.
- **Logging:** Centralize and correlate logs with traces and other telemetry to make debugging easier.
- **Error tracking:** Detect errors quickly and investigate their root causes with the surrounding context.
- **SLA monitoring:** Track service-level objectives and identify when your application is approaching or exceeding defined thresholds.
- **Alarms and alerts:** Set up alerts for critical errors, performance degradation, SLA violations, and other anomalies so your team can react quickly.

## Resources

Check out a few resources that may come in handy when working with NestJS:

- Visit the [NestJS Documentation](https://docs.nestjs.com) to learn more about the framework.
- For questions and support, please visit our [Discord channel](https://discord.gg/G7Qnnhy).
- To dive deeper and get more hands-on experience, check out our official video [courses](https://courses.nestjs.com/).
- Deploy your application to AWS with the help of [NestJS Mau](https://mau.nestjs.com) in just a few clicks.
- Auto-instrument your application with [NestJS Observer](https://observer.nestjs.com). Distributed tracing, metrics, and logging made easy. Error tracking and performance monitoring for your NestJS applications.
- Visualize your application graph and interact with the NestJS application in real-time using [NestJS Devtools](https://devtools.nestjs.com).
- Need help with your project (part-time to full-time)? Check out our official [enterprise support](https://enterprise.nestjs.com).
- To stay in the loop and get updates, follow us on [X](https://x.com/nestframework) and [LinkedIn](https://linkedin.com/company/nestjs).
- Looking for a job, or have a job to offer? Check out our official [Jobs board](https://jobs.nestjs.com).

## Support

Nest is an MIT-licensed open source project. It can grow thanks to the sponsors and support by the amazing backers. If you'd like to join them, please [read more here](https://docs.nestjs.com/support).

## Stay in touch

- Author - [Kamil Myśliwiec](https://twitter.com/kammysliwiec)
- Website - [https://nestjs.com](https://nestjs.com/)
- Twitter - [@nestframework](https://twitter.com/nestframework)

## License

Nest is [MIT licensed](https://github.com/nestjs/nest/blob/master/LICENSE).
