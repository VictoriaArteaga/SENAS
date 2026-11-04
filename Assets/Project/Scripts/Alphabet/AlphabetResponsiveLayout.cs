using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lumina.UI;

namespace Lumina.Alphabet
{
    /// <summary>
    /// Acomoda la interfaz del Abecedario por proporciones de pantalla y con layouts automáticos, igual que los paneles de las demás
    /// casas: el panel de letras a la izquierda, la letra grande y el botón de vista arriba a la derecha, y la tarjeta de práctica abajo.
    /// Trabaja sobre los objetos ya existentes de la escena (por nombre), sin cambiar sus referencias ni su comportamiento.
    /// </summary>
    internal static class AlphabetResponsiveLayout
    {
        public static void Apply(GameObject uiRoot)
        {
            if (uiRoot == null) return;
            Transform root = uiRoot.transform.Find("Root");
            if (root == null) root = uiRoot.transform;

            ArrangeLettersPanel(root.Find("PanelLetras") as RectTransform);
            ArrangeRightColumn(root);
            Anchor(root.Find("PanelInstruccion") as RectTransform, new Vector2(0.40f, 0.03f), new Vector2(0.98f, 0.27f));
            ContainTexts(uiRoot.transform);
        }

        /// <summary>
        /// Ningún texto sobresale de su caja: todos se reducen (hasta un mínimo) para caber, y los textos de botones se ajustan al botón con
        /// un margen. Se puede volver a llamar tras construir piezas nuevas; respeta los textos que ya se ajustan solos.
        /// </summary>
        internal static void ContainTexts(Transform root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.GetComponentInParent<Button>(true) is Button button && text.transform != button.transform)
                {
                    // Texto de un botón: ocupa el botón con un margen y no se sale aunque el botón sea estrecho.
                    var rect = text.rectTransform;
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.offsetMin = new Vector2(10f, 4f); rect.offsetMax = new Vector2(-10f, -4f);
                    text.alignment = TextAlignmentOptions.Center;
                }
                if (!text.enableAutoSizing) Fit(text, 9f, text.textWrappingMode != TextWrappingModes.NoWrap);
                text.fontSizeMin = Mathf.Min(text.fontSizeMin, 9f);
                text.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        // ---------- Panel de letras (izquierda) ----------

        private static void ArrangeLettersPanel(RectTransform panel)
        {
            if (panel == null) return;
            Anchor(panel, new Vector2(0.02f, 0.04f), new Vector2(0.385f, 0.96f));

            var layout = Ensure<VerticalLayoutGroup>(panel);
            layout.padding = new RectOffset(28, 28, 30, 28);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;

            var ring = panel.Find("Aro");
            if (ring != null)
            {
                Ensure<LayoutElement>(ring).ignoreLayout = true;
                ring.SetAsFirstSibling();
            }

            var chip = Child(panel, "TituloChip");
            var subtitle = Child(panel, "Subtitulo");
            var grid = Child(panel, "Cuadricula");
            var progress = Child(panel, "Progreso");
            var previous = Child(panel, "Anterior");
            var next = Child(panel, "Siguiente");
            var back = Child(panel, "Volver");

            var navigation = panel.Find("FilaNavegacion") as RectTransform;
            if (navigation == null)
            {
                var go = new GameObject("FilaNavegacion", typeof(RectTransform));
                navigation = (RectTransform)go.transform;
                navigation.SetParent(panel, false);
                var row = go.AddComponent<HorizontalLayoutGroup>();
                row.spacing = 14f; row.childAlignment = TextAnchor.MiddleCenter;
                row.childControlWidth = row.childControlHeight = true;
                row.childForceExpandWidth = row.childForceExpandHeight = true;
            }
            foreach (var button in new[] { previous, next })
            {
                if (button == null) continue;
                button.SetParent(navigation, false);
                Size(button, minW: 120f, minH: 64f, prefH: 80f, flexW: 1f);
            }
            if (previous != null) previous.SetAsFirstSibling();

            Size(chip, minH: 64f, prefH: 84f);
            Size(subtitle, minH: 30f, prefH: 40f);
            Size(grid, minH: 240f, flexH: 1f);
            Size(progress, minH: 32f, prefH: 44f);
            Size(navigation, minH: 64f, prefH: 80f);
            Size(back, minH: 64f, prefH: 80f);

            // Orden de arriba abajo: título, subtítulo, letras, progreso, anterior/siguiente, volver.
            foreach (var item in new[] { chip, subtitle, grid, progress, navigation, back })
                if (item != null) item.SetAsLastSibling();

            if (grid != null)
            {
                var gridLayout = grid.GetComponent<GridLayoutGroup>();
                if (gridLayout != null)
                {
                    gridLayout.childAlignment = TextAnchor.UpperCenter;
                    if (grid.GetComponent<AutoCellGrid>() == null) grid.gameObject.AddComponent<AutoCellGrid>();
                }
                foreach (var text in grid.GetComponentsInChildren<TMP_Text>(true)) Fit(text, 16f, false);
            }

            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
                if (grid == null || !text.transform.IsChildOf(grid)) Fit(text, 14f, true);
        }

        // ---------- Columna derecha: letra grande, etiqueta y botón de vista ----------

        private static void ArrangeRightColumn(Transform root)
        {
            var bigLetter = Child(root, "LetraGrande");
            var label = Child(root, "LetraEtiqueta");
            var view = Child(root, "VistaBoton");
            if (bigLetter == null && label == null && view == null) return;

            var column = root.Find("ColumnaDerecha") as RectTransform;
            if (column == null)
            {
                var go = new GameObject("ColumnaDerecha", typeof(RectTransform));
                column = (RectTransform)go.transform;
                column.SetParent(root, false);
                var layout = go.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 16f; layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            }
            Anchor(column, new Vector2(0.77f, 0.30f), new Vector2(0.98f, 0.97f));

            if (bigLetter != null) { bigLetter.SetParent(column, false); Size(bigLetter, minW: 120f, minH: 120f, prefW: 230f, prefH: 230f); }
            if (label != null) { label.SetParent(column, false); Size(label, minW: 120f, minH: 40f, prefW: 230f, prefH: 60f); }
            if (view != null) { view.SetParent(column, false); Size(view, minW: 140f, minH: 64f, prefW: 310f, prefH: 84f); }

            if (label != null) foreach (var text in label.GetComponentsInChildren<TMP_Text>(true)) Fit(text, 16f, false);
            if (view != null) foreach (var text in view.GetComponentsInChildren<TMP_Text>(true)) Fit(text, 16f, false);
            if (bigLetter != null) foreach (var text in bigLetter.GetComponentsInChildren<TMP_Text>(true)) Fit(text, 40f, false);
        }

        // ---------- Utilidades ----------

        private static RectTransform Child(Transform parent, string name) => parent.Find(name) as RectTransform;

        private static T Ensure<T>(Component target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.gameObject.AddComponent<T>();
        }

        /// <summary>Estira el rectángulo entre dos anclas (proporciones de pantalla), sin desplazamientos fijos.</summary>
        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            if (rect == null) return;
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Size(Component target, float minW = -1f, float minH = -1f, float prefW = -1f, float prefH = -1f, float flexW = -1f, float flexH = -1f)
        {
            if (target == null) return;
            var e = Ensure<LayoutElement>(target);
            e.minWidth = minW; e.minHeight = minH; e.preferredWidth = prefW; e.preferredHeight = prefH;
            e.flexibleWidth = flexW; e.flexibleHeight = flexH;
        }

        /// <summary>El texto se reduce (hasta <paramref name="min"/>) para caber en su caja en lugar de desbordarla.</summary>
        internal static void Fit(TMP_Text text, float min, bool wrap)
        {
            if (text == null) return;
            text.enableAutoSizing = true;
            text.fontSizeMax = Mathf.Max(text.fontSize, min);
            text.fontSizeMin = min;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
