using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using Lumina.Core;

namespace Lumina.UI
{
    [Serializable]
    public class AvatarOption
    {
        public string id;
        public string displayName;
        [Tooltip("Modelo 3D (hijo del pivote). Solo está activo el que se está viendo.")]
        public GameObject model;
    }

    /// <summary>
    /// Selector visual de avatares sobre la plataforma de exhibición: flechas para recorrer las opciones,
    /// puntos indicadores, botón SELECCIONAR e insignia de selección. El modelo se puede girar arrastrando
    /// con el ratón y, si no se toca, gira despacio solo. No guarda nada: la elección vive en memoria.
    /// </summary>
    public class AvatarSelectorController : MonoBehaviour
    {
        [Header("Modelos")]
        [SerializeField] private Transform pivot;
        [SerializeField] private AvatarOption[] options;
        [SerializeField] private float baseYaw = 165f;
        [SerializeField] private float autoSpinDegreesPerSecond = 14f;
        [SerializeField] private float dragSensitivity = 0.4f;

        [Header("Interfaz")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button selectButton;
        [SerializeField] private TMP_Text selectLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private GameObject selectedBadge;
        [SerializeField] private Image[] dots;
        [SerializeField] private Color dotActive = new Color(1f, 0.369f, 0.302f);
        [SerializeField] private Color dotInactive = new Color(0.56f, 0.89f, 0.85f);

        private int _index;
        private int _chosen = -1;
        private float _yaw;
        private float _idle;
        private float _pop = 1f;
        private bool _dragging;
        private Vector3 _pivotScale = Vector3.one;

        public bool HasSelection => _chosen >= 0;
        public string SelectedId => HasSelection ? options[_chosen].id : null;
        public string SelectedName => HasSelection ? options[_chosen].displayName : null;
        public int Count => options != null ? options.Length : 0;

        public event Action<string> Selected;

        private void Awake()
        {
            if (pivot == null || options == null || options.Length == 0 || previousButton == null || nextButton == null ||
                selectButton == null || nameLabel == null)
            {
                Debug.LogError($"[AvatarSelectorController] Referencias incompletas en '{name}'.", this);
                enabled = false;
                return;
            }

            _pivotScale = pivot.localScale;
            _yaw = baseYaw;
            previousButton.onClick.AddListener(() => Move(-1));
            nextButton.onClick.AddListener(() => Move(1));
            selectButton.onClick.AddListener(ChooseCurrent);
        }

        private IEnumerator Start()
        {
            // Los modelos aplican su personalización en su propio Start; se esperan dos fotogramas antes de ocultar los demás.
            yield return null;
            yield return null;
            Show(0, false);
        }

        private void Update()
        {
            // Se limita el paso de tiempo para que un tirón puntual (carga de la escena) no gire el modelo de golpe.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            HandleRotationInput();

            if (!_dragging && _idle > 1.5f) _yaw += autoSpinDegreesPerSecond * dt;
            _idle += dt;
            pivot.localRotation = Quaternion.Euler(0f, _yaw, 0f);

            _pop = Mathf.MoveTowards(_pop, 1f, dt * 4.5f);
            float eased = 1f - Mathf.Pow(1f - _pop, 3f);
            pivot.localScale = _pivotScale * Mathf.Lerp(0.82f, 1f, eased);
        }

        private void HandleRotationInput()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame && !PointerOverUi()) _dragging = true;
                if (mouse.leftButton.wasReleasedThisFrame) { _dragging = false; _idle = 0f; }
                if (_dragging)
                {
                    _yaw -= mouse.delta.ReadValue().x * dragSensitivity;
                    _idle = 0f;
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && !TypingInField())
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame) Move(-1);
                else if (keyboard.rightArrowKey.wasPressedThisFrame) Move(1);
            }
        }

        private static bool PointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private static bool TypingInField()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.GetComponent<TMP_InputField>() != null;
        }

        private void Move(int direction)
        {
            Show((_index + direction + options.Length) % options.Length, true);
        }

        private void Show(int index, bool animate)
        {
            _index = index;
            for (int i = 0; i < options.Length; i++)
                if (options[i].model != null) options[i].model.SetActive(i == index);

            nameLabel.text = options[index].displayName.ToUpperInvariant();
            if (dots != null)
                for (int i = 0; i < dots.Length; i++)
                {
                    if (dots[i] == null) continue;
                    dots[i].color = i == index ? dotActive : dotInactive;
                    dots[i].transform.localScale = Vector3.one * (i == index ? 1.35f : 1f);
                }

            RefreshSelectionUi();
            if (animate) _pop = 0f;
            _idle = 0f;
        }

        private void ChooseCurrent()
        {
            _chosen = _index;
            RefreshSelectionUi();
            _pop = 0.4f;
            Selected?.Invoke(options[_chosen].id);
        }

        private void RefreshSelectionUi()
        {
            bool isChosen = _chosen == _index;
            if (selectedBadge != null) selectedBadge.SetActive(isChosen);
            if (selectLabel != null) selectLabel.text = Loc.T(isChosen ? "auth.selected" : "auth.select");
        }

        /// <summary>Llama la atención sobre el botón SELECCIONAR (cuando el formulario se envía sin elegir avatar).</summary>
        public void Nudge()
        {
            _pop = 0f;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selectButton.gameObject);
        }
    }
}
