# Cámara, perspectivas y antebrazo (Fase 6.5)

Todas las letras siguen **pendientes de validación lingüística**. Esta fase solo cambia cómo se mira la mano; no cambia ninguna pose, trayectoria ni dato del alfabeto.

## 1. Diagnóstico de la cámara original
- `AlphabetHouse`: la cámara `LearningCamera` (FOV 38°, fondo turquesa liso) está fija en (1,91; 1,075; 4,40) mirando +Z. `AlphabetLevelController` la activa al abrir el nivel y apaga la del jugador.
- La mano de demostración (`ManoDemostracion`, rotación (0,90,180), delante de un telón turquesa) siempre se veía de frente, con la palma hacia la cámara.
- Nota técnica: el objeto raíz de la mano **no** coincide con la malla visible (la muñeca real, `J_Right`, está ~0,27 m más lejos en Z). Hay que orbitar alrededor del hueso de la muñeca, no del objeto raíz.

## 2. Limitaciones identificadas
- De frente no se aprecian la profundidad de los dedos, el círculo de D, el gancho de X ni la posición del pulgar respecto de la palma.
- H, M, N y Ñ (mano de lado o boca abajo) se leen mejor con otro ángulo.
- El modelo termina en la muñeca con un corte plano: no hay antebrazo.

## 3. Perspectivas
| Vista | Estado | Justificación |
|---|---|---|
| FRENTE | Implementada (original, por defecto) | Posición exacta de la cámara original. |
| PERFIL | Implementada (giro −90° alrededor de la muñeca) | Muestra profundidad, el círculo de D, el gancho de X, M/N/Ñ de canto y H. |
| DORSO | Implementada (giro 180°) | Útil para ver el dorso en M, N y Ñ y los dedos hacia abajo; para algunas letras (D, X) aporta menos que el perfil. |

Las vistas laterales acercan la cámara (×0,75) y colocan un panel liso (`FondoVista`) detrás de la mano; sin él se veía la habitación (y en el dorso, el reverso del telón). Cambiar de vista **solo mueve la cámara**: no se toca el rig, la pose, el movimiento ni la letra seleccionada. Al cerrar el nivel o reabrirlo vuelve a FRENTE.

**Cómo se cambia:** botón `VISTA: FRENTE/PERFIL/DORSO` (arriba a la derecha, mismo estilo turquesa) o tecla **V**. No se tocaron las teclas existentes.

## 4. Antebrazo
No es posible con el modelo actual: la malla termina en un corte plano en la muñeca. Opciones (**requieren autorización**; no implementadas):
1. Un manguito/puño de tela como objeto separado, hijo del hueso de la muñeca (sin tocar la malla ni el rig).
2. Importar un modelo de mano con antebrazo.
La opción 1 es la más barata y taparía el corte plano que se ve en perfil y dorso.

## 5. Componentes modificados
- `AlphabetLevelController.cs`: campos de vista (`viewYaws`, `viewNames`, `sideViewDistanceScale`, `viewBackdrop`, `viewButton`, `viewLabel`), `NextView()`/`SetView()`, tecla V, restablecer FRENTE al abrir/cerrar.
- `AlphabetHouse.unity`: botón `VistaBoton` y panel `FondoVista` (añadidos; nada eliminado), referencias asignadas.
- Sin cambios: `HandPose.cs`, `HandPoseDriver.cs`, `LscAlphabet.asset` (sus cambios son de fases anteriores), movimientos.

## 6. Letras que se benefician
- **Perfil:** D (el círculo pulgar–medio se ve), X (el gancho), H, M, N, Ñ (dedos hacia abajo de canto), K, G (mano de lado), C y O (el arco y el círculo), E, A, B (pulgar respecto de la palma).
- **Dorso:** M, N, Ñ, H.
- Las demás letras se entienden bien de frente.

## 7. Pruebas (Play Mode, Game View 1920×1080 y 1280×720; input simulado)
- 23 letras (A–I, K–O, R, T–Z con Ñ) × 3 vistas, capturadas y revisadas por muestreo (D, X, H, M, N, Ñ comparadas con la foto): la mano siempre es visible y la pose no cambia entre vistas (firma de huesos idéntica).
- Cada letra cicla FRENTE→PERFIL→DORSO→FRENTE y la cámara vuelve exactamente a su posición original.
- Estabilidad: N durante 300 fotogramas en PERFIL y DORSO sin cambios de pose ni de cámara.
- J, Z y Ñ se mueven en vista de perfil; G y H siguen sin movimiento.
- Flecha derecha, PageDown, SIGUIENTE y Esc conservan su comportamiento; Esc deja la cámara en FRENTE.
- 105 comprobaciones en cada resolución, 0 fallos.

## 8. Limitaciones que persisten
- Sin antebrazo (ver 4); en M/N/Ñ de perfil la mano queda cerca del borde superior.
- En DORSO la mano queda más oscura (la luz viene de frente).
- Solo hay giros horizontales; no hay vista desde arriba ni órbita libre.
- Aún no se probó con teclado y ratón físicos.

## 9. Recomendaciones
- Autorizar el manguito (opción 1 del antebrazo).
- Subir la luz/relleno en la vista de dorso.
- Valorar una vista guardada por letra (p. ej. D y X abren en perfil) una vez validadas las poses por una persona usuaria de LSC.
