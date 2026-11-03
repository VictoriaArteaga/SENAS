# Prueba manual del prototipo (6.1.4)

Escena: `Assets/Project/Prototypes/Intervention_6_1_2/Scenes/Prototype_Modular_RiverCentral.unity`
(abrirla desde el Project; no está en Build Settings). Pulsa Play.

> Esta lista es para **ti, con teclado y ratón físicos**. Las pruebas automatizadas (`Evidence/r4_resultados.txt` y
> `Evidence/r4_playmode.txt`) inyectan eventos del Input System y no sustituyen esta revisión.

## Controles
| Tecla | Acción |
|---|---|
| WASD · ratón · Shift | mover · mirar · correr (controlador original de Lúmina, sin modificar) |
| Esc / clic | soltar / recuperar el cursor |
| 0 | volver al spawn |
| 1 · 2 · 3 · 4 | ir frente a la puerta de Alphabet · Numbers · Months · Greetings |
| 5 · 6 | ir al extremo oeste del puente principal · del puente norte |
| 7 | ir junto a Sena |
| N | alternar día/noche (usa `GameSettings.DarkMode`, el mismo mecanismo del juego) |
| H | mostrar/ocultar la ayuda en pantalla |

Los atajos 0–7 y N son una ayuda del prototipo (`Proto614TestHelper`); sirven para llegar rápido, no sustituyen al recorrido a pie.

## Estado que debe verse al pulsar Play
- Aparece en la plaza (−18, 0,1, −1,5) mirando al este: Sena de frente, el puente principal al fondo, el río y los carteles.
- Sena te saluda (animación «Wave», ~4 s) y vuelve a Idle; gira con suavidad hacia ti si te mueves cerca.
- Arriba a la izquierda, la ayuda de teclas.

## Lista de comprobación
1. [ ] Movimiento desde el spawn (WASD, Shift).
2. [ ] Giro de cámara con el ratón (sin saltos ni inercia rara).
3. [ ] Acercarte a Sena (¿tapa el camino? ¿se orienta hacia ti?).
4. [ ] Visitar las cuatro casas (tecla 1–4 o a pie por los caminos).
5. [ ] Llegar a cada puerta: ¿se identifica la fachada, la puerta y el cartel de la casa (ABECEDARIO · NÚMEROS · MESES · SALUDOS)?
6. [ ] Cruzar el puente principal y el puente norte, en los dos sentidos.
7. [ ] Intentar cruzar el río fuera de los puentes (debe frenarte antes del agua).
8. [ ] Caminar por caminos, plaza y explanada (bancos y faroles no deben estorbar).
9. [ ] Pendientes: lomas de las esquinas noreste (suave) y suroeste (≈22°).
10. [ ] Límites del mapa (muros invisibles a ±45 m) y colisiones con casas, árboles, bancos y carteles.
11. [ ] Señalización: «PLAZA CENTRAL · SECTOR OESTE», «SECTOR ESTE · MESES · SALUDOS», «PUENTE PRINCIPAL», «PUENTE NORTE».
12. [ ] Pulsar N: noche. ¿Se reconocen casas, caminos, puentes y río? ¿Los faroles iluminan sin deslumbrar?

## Si algo falla
No lo corrijas en la escena: anota el punto (tecla de atajo o coordenadas) y qué esperabas. Toda la escena se regenera desde
`Editor/Proto612BuilderV2.cs` (menú *Lumina ▸ Prototipo 6.1.2 ▸ Construir modular con río central*).
