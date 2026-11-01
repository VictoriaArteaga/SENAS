using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Lumina.Core;
using Lumina.Player;
using Lumina.Signs;

namespace Lumina.UI
{
    /// <summary>
    /// Panel de una casa de aprendizaje (Números, Meses o Saludos y frases). El jugador recorre la casa, apunta a un objeto y
    /// pulsa E (<see cref="LearningItemSelector"/>): este panel muestra el elemento elegido y permite verlo de nuevo,
    /// moverse entre elementos o PRACTICAR (abre Practice con esa seña). No reconoce señas: el reconocimiento vive en
    /// Practice (<see cref="ISignRecognizer"/>) y la demostración del avatar entra por <see cref="DemonstrationRequested"/>.
    /// "Visto" no significa "aprendido": la barra cuenta lo visto en esta visita, no evalúa nada.
    /// </summary>
    public class LearningLevelController : MonoBehaviour
    {
        [SerializeField] private LevelKind kind;
        [Tooltip("Objeto raíz del panel (el canvas). Está desactivado mientras no se usa.")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private FirstPersonController player;

        [Header("Elemento actual")]
        [SerializeField] private TMP_Text bigLabel;        // número, mes (01…12) o frase
        [SerializeField] private TMP_Text nameLabel;       // palabra (UNO, ENERO…); no se usa en Saludos
        [SerializeField] private TMP_Text positionLabel;   // «NÚMERO 3 DE 10»
        [SerializeField] private TMP_Text groupLabel;      // grupo de la frase (solo Saludos)
        [SerializeField] private TMP_Text senaSaysLabel;   // lo que dice Sena (solo Saludos)
        [SerializeField] private TMP_Text instructionLabel;
        [SerializeField] private TMP_Text demoNotice;
        [SerializeField] private Image[] countDots;        // solo Números: un punto por unidad

        [Header("Acción y progreso")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button learnButton;
        [SerializeField] private Button practiceButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private ProgressBarUI progressBar;

        /// <summary>Se lanza al pulsar VER SEÑA. TODO(avatar): conectar la animación de la seña en el personaje.</summary>
        public UnityEvent DemonstrationRequested = new UnityEvent();

        /// <summary>Poner a true cuando haya una demostración animada; mientras tanto VER SEÑA avisa de que aún no existe.</summary>
        public bool DemonstrationAvailable { get; set; }

        public bool IsOpen { get; private set; }

        private static readonly Color Coral = new Color(1f, 0.369f, 0.302f);
        private static readonly Color Sand = new Color(0.98f, 0.89f, 0.74f);

        private LevelDefinition _level;
        private readonly HashSet<int> _seen = new HashSet<int>();
        private LearningItemTarget[] _targets;
        private int _index;

        private void Awake()
        {
            _level = LearningLevels.Get(kind);
            _targets = FindObjectsByType<LearningItemTarget>(FindObjectsSortMode.None);
            demoNotice.gameObject.SetActive(false);
            previousButton.onClick.AddListener(() => Move(-1));
            nextButton.onClick.AddListener(() => Move(1));
            learnButton.onClick.AddListener(OnLearn);
            practiceButton.onClick.AddListener(OnPractice);
            closeButton.onClick.AddListener(Close);
            panelRoot.SetActive(false);
        }

        private void OnEnable() => GameSettings.LanguageChanged += Refresh;
        private void OnDisable() => GameSettings.LanguageChanged -= Refresh;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (!IsOpen || keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) Close();
            else if (keyboard.leftArrowKey.wasPressedThisFrame) Move(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame) Move(1);
        }

        /// <summary>Abre el panel con el elemento indicado y deja el control del jugador en pausa.</summary>
        public void Open(int index)
        {
            IsOpen = true;
            panelRoot.SetActive(true);
            if (player != null) { player.enabled = false; player.SetCursorLocked(false); }
            Select(index);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(learnButton.gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            panelRoot.SetActive(false);
            foreach (var target in _targets) if (target != null) target.SetHighlighted(false);
            if (player != null) { player.enabled = true; player.SetCursorLocked(true); }
        }

        private void Move(int step)
        {
            int next = _index + step;
            if (next < 0 || next >= _level.items.Length) return;
            Select(next);
        }

        private void Select(int index)
        {
            _index = Mathf.Clamp(index, 0, _level.items.Length - 1);
            _seen.Add(_index);
            demoNotice.gameObject.SetActive(false);
            foreach (var target in _targets) if (target != null) target.SetHighlighted(target.Index == _index);
            Refresh();
        }

        /// <summary>Reescribe todo lo visible del elemento actual (también al cambiar el idioma).</summary>
        private void Refresh()
        {
            if (_level == null || !IsOpen) return;
            var item = _level.items[_index];
            int total = _level.items.Length;

            switch (kind)
            {
                case LevelKind.Numbers:
                    bigLabel.text = (_index + 1).ToString();
                    nameLabel.text = item.Name.ToUpperInvariant();
                    positionLabel.text = Loc.T("levels.numberOf", _index + 1, total);
                    for (int i = 0; i < countDots.Length; i++) ThemedGraphic.Set(countDots[i], i <= _index ? Coral : Sand);
                    break;
                case LevelKind.Months:
                    bigLabel.text = (_index + 1).ToString("00");
                    nameLabel.text = item.Name.ToUpperInvariant();
                    positionLabel.text = Loc.T("levels.monthOf", _index + 1, total);
                    break;
                default:
                    bigLabel.text = item.Name;
                    positionLabel.text = Loc.T("levels.phraseOf", _index + 1, total);
                    if (groupLabel != null) groupLabel.text = Loc.T(item.groupKey);
                    if (senaSaysLabel != null) senaSaysLabel.text = Loc.T("levels.senaSays") + ": «" + item.SenaSays + "»";
                    break;
            }

            instructionLabel.text = _level.Hint(item);
            previousButton.interactable = _index > 0;
            nextButton.interactable = _index < total - 1;
            progressBar.Set((float)_seen.Count / total, Loc.T("levels.seen", _seen.Count, total));
        }

        private void OnLearn()
        {
            if (DemonstrationAvailable) DemonstrationRequested.Invoke();
            else demoNotice.gameObject.SetActive(true);
        }

        private void OnPractice()
        {
            PracticeSession.BeginFromLevel(_level.ToSignEntry(_level.items[_index]), _level.sceneName);
            ScreenFader.Load(SceneNames.Practice);
        }
    }
}
