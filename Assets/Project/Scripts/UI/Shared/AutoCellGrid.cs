using UnityEngine;
using UnityEngine.UI;

namespace Lumina.UI
{
    /// <summary>
    /// Cuadrícula de columnas fijas con celdas cuadradas que se reajustan al espacio disponible: la celda es la más grande que
    /// cabe a lo ancho y a lo alto (hasta <see cref="maxCell"/>), así los botones nunca se salen del panel ni se pegan entre sí.
    /// </summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public class AutoCellGrid : MonoBehaviour
    {
        [SerializeField] private float minCell = 44f;
        [SerializeField] private float maxCell = 120f;

        private GridLayoutGroup _grid;
        private RectTransform _rect;

        private void OnEnable() => Refresh();

        private void OnRectTransformDimensionsChange() => Refresh();

        private void OnTransformChildrenChanged() => Refresh();

        public void Refresh()
        {
            if (!isActiveAndEnabled) return;
            if (_grid == null) _grid = GetComponent<GridLayoutGroup>();
            if (_rect == null) _rect = (RectTransform)transform;

            int columns = Mathf.Max(1, _grid.constraintCount);
            int count = 0;
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).gameObject.activeSelf) count++;
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));

            float width = _rect.rect.width - _grid.padding.horizontal - _grid.spacing.x * (columns - 1);
            float height = _rect.rect.height - _grid.padding.vertical - _grid.spacing.y * (rows - 1);
            if (width <= 0f || height <= 0f) return;

            float side = Mathf.Clamp(Mathf.Floor(Mathf.Min(width / columns, height / rows)), minCell, maxCell);
            var size = new Vector2(side, side);
            if (_grid.cellSize != size) _grid.cellSize = size;
        }
    }
}
