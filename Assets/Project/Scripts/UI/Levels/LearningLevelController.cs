using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;
using Lumina.Api;
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
    /// El estado de cada seña y su disponibilidad para practicar provienen del backend. Números, Saludos y Meses
    /// cuentan con reconocimiento en Unity y envían sus resultados por el flujo oficial.
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

        [Header("Televisor (opcional)")]
        [Tooltip("Televisor de la casa. Proyecta el video del elemento elegido.")]
        [SerializeField] private TelevisionScreen television;
        [Tooltip("Un video por elemento, en el mismo orden del nivel (Meses: enero … diciembre). Vacío = sin video.")]
        [SerializeField] private VideoClip[] itemVideos;

        [Header("Música")]
        [Tooltip("Pausa la música de fondo mientras el panel de la lección está abierto (sus videos traen sonido propio). Al cerrarlo, vuelve.")]
        [SerializeField] private bool muteMusicWhileOpen;

        [Header("Práctica dentro de la casa (opcional)")]
        [Tooltip("Si se asigna, PRACTICAR/EMPEZAR practica aquí mismo (cámara, calificación y estrellas) en vez de abrir la escena Practice.")]
        [SerializeField] private LearningPracticeView practice;
        [Tooltip("Distancia (m) delante de la cámara a la que se coloca el televisor durante la práctica.")]
        [SerializeField] private float tvDistance = 2.6f;
        [Tooltip("Dónde queda el televisor en la pantalla durante la práctica: −1 izquierda … 1 derecha. −0,5 lo deja en el cuarto izquierdo, junto al panel.")]
        [SerializeField] private float tvScreenX = -0.5f;
        [Tooltip("Cuánto se agranda el televisor (con sus patas) durante la práctica. Con distancia 3,4 m, 1,27 entra completo, base incluida, sin tocar lo que cuelga del techo.")]
        [SerializeField, Range(1f, 2.5f)] private float tvScale = 1.6f;
        [Tooltip("Cuánto más abajo (m) que los ojos queda el centro de la pantalla; así no roza lo que cuelga del techo.")]
        [SerializeField] private float tvBelowEyes = 0.17f;
        [Tooltip("Objetos que se ocultan mientras el panel está abierto (p. ej. el modelo de Sena, para que no se vea de fondo).")]
        [SerializeField] private GameObject[] hideWhileOpen;

        /// <summary>Se lanza al pulsar VER SEÑA. TODO(avatar): conectar la animación de la seña en el personaje.</summary>
        public UnityEvent DemonstrationRequested = new UnityEvent();

        /// <summary>Poner a true cuando haya una demostración animada; mientras tanto VER SEÑA avisa de que aún no existe.</summary>
        public bool DemonstrationAvailable { get; set; }

        public bool IsOpen { get; private set; }

        /// <summary>True si el panel ya se abrió al menos una vez en esta visita (para ofrecer «Continúa practicando»).</summary>
        public bool WasOpened { get; private set; }

        private static readonly Color Coral = new Color(1f, 0.369f, 0.302f);
        private static readonly Color Sand = new Color(0.98f, 0.89f, 0.74f);

        private LevelDefinition _level;
        private string _levelCode;
        private bool _loadingState;
        private readonly HashSet<int> _seen = new HashSet<int>();
        private LearningItemTarget[] _targets;
        private int _index;

        private void Awake()
        {
            _level = LearningLevels.Get(kind);
            _levelCode = kind == LevelKind.Numbers ? SignCodes.NumbersLevel
                : kind == LevelKind.Months ? SignCodes.MonthsLevel
                : SignCodes.GreetingsLevel;
            _targets = FindObjectsByType<LearningItemTarget>(FindObjectsSortMode.None);
            demoNotice.gameObject.SetActive(false);
            previousButton.onClick.AddListener(() => Move(-1));
            nextButton.onClick.AddListener(() => Move(1));
            learnButton.onClick.AddListener(OnLearn);
            practiceButton.onClick.AddListener(OnPractice);
            closeButton.onClick.AddListener(Close);
            if (practice != null) { practice.RunningChanged += OnPracticeRunningChanged; practice.SignPassed += OnSignPassed; }
            panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (muteMusicWhileOpen) GameAudio.SetMusicSuppressed(false);
            if (practice == null) return;
            practice.RunningChanged -= OnPracticeRunningChanged;
            practice.SignPassed -= OnSignPassed;
        }

        /// <summary>Seña superada: tras una pausa para ver el resultado, pasa a la siguiente lección (enero → febrero → …).</summary>
        private void OnSignPassed() => StartCoroutine(AdvanceAfterPass(_index));

        private IEnumerator AdvanceAfterPass(int passedIndex)
        {
            yield return new WaitForSecondsRealtime(2.5f);
            if (IsOpen && _index == passedIndex && passedIndex < _level.items.Length - 1) Move(1);
        }

        private void OnEnable() { GameSettings.LanguageChanged += Refresh; ProgressStore.Changed += Refresh; }
        private void OnDisable() { GameSettings.LanguageChanged -= Refresh; ProgressStore.Changed -= Refresh; }

        private void Start() => LoadState();

        /// <summary>Pide al backend el estado de las señas de esta casa (solo si Unity ya implementa el nivel).</summary>
        private async void LoadState()
        {
            if (_loadingState || !SignCodes.IsImplementedInUnity(_levelCode) || !ApiSession.HasSession) return;
            _loadingState = true;
            try { await ProgressStore.LoadLevelAsync(_levelCode); }
            catch (ApiException e) { if (this != null) Toast.Show(ApiMessages.For(e)); }
            finally { if (this != null) _loadingState = false; }
        }

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
            WasOpened = true;
            if (muteMusicWhileOpen) GameAudio.SetMusicSuppressed(true);
            panelRoot.SetActive(true);
            if (player != null) { player.enabled = false; player.SetCursorLocked(false); }
            BringTelevision();
            if (practice != null) practice.Begin();
            SetHidden(true);
            Select(index);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(learnButton.gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (muteMusicWhileOpen) GameAudio.SetMusicSuppressed(false);
            if (practice != null) practice.End();
            if (television != null) television.Stop(); // al salir del panel el video deja de reproducirse
            SetHidden(false);
            RestoreTelevision();
            panelRoot.SetActive(false);
            foreach (var target in _targets) if (target != null) target.SetHighlighted(false);
            if (player != null) { player.enabled = true; player.SetCursorLocked(true); }
        }

        private void SetHidden(bool hidden)
        {
            if (hideWhileOpen == null) return;
            foreach (var go in hideWhileOpen) if (go != null) go.SetActive(!hidden);
        }

        /// <summary>Abre la práctica en la primera seña que aún no está superada (o en la primera). Es lo que llama Sena al terminar.</summary>
        public void OpenPractice()
        {
            int first = 0;
            for (int i = 0; i < _level.items.Length; i++)
                if (ProgressStore.StatusOf(_level.items[i].id) != SignStatusNames.Passed) { first = i; break; }
            Open(first);
        }

        private Vector3 _tvHomePosition;
        private Quaternion _tvHomeRotation;
        private bool _tvHomeSaved;
        private Vector3 _tvHomeScale = Vector3.one;

        // Partes del carrito que se esconden SOLO durante la práctica: queda únicamente la pantalla del televisor (marco, pantalla y barbilla).
        private static readonly string[] TvStand = { "BasePlaca", "Rueda", "Poste", "Repisa", "Reproductor", "LuzReproductor", "Soporte" };

        /// <summary>
        /// Durante la práctica el televisor de la casa es EL televisor de la lección, pero solo su pantalla (sin carrito ni patas): más grande
        /// (<see cref="tvScale"/>), recta y de frente a la cámara, a la altura de los ojos y en el cuarto izquierdo de la pantalla (el panel ocupa la
        /// derecha). La mirada del jugador queda a nivel. Al cerrar el panel el televisor vuelve a su sitio, con su carrito, tal como estaba.
        /// </summary>
        private void BringTelevision()
        {
            var cam = Camera.main;
            if (television == null || cam == null) return;
            var tv = television.transform;
            if (!_tvHomeSaved) { _tvHomePosition = tv.position; _tvHomeRotation = tv.rotation; _tvHomeScale = tv.localScale; _tvHomeSaved = true; }

            Vector3 forward = cam.transform.forward; forward.y = 0f; forward.Normalize();
            Vector3 eye = cam.transform.position;
            Vector3 position = eye + forward * tvDistance;
            // El centro de la pantalla está 1,42 m por encima de la base del televisor (a escala 1): queda a la altura de los ojos.
            position.y = eye.y - 1.42f * _tvHomeScale.y * tvScale - tvBelowEyes;

            // Mirada a nivel y desplazada hacia la derecha lo justo para dejar el televisor en el cuarto izquierdo.
            if (player != null) player.LookTowards(new Vector3(position.x, eye.y, position.z), tvScreenX);

            // Recto y de frente: la pantalla es perpendicular a la vista y no se inclina hacia ningún lado.
            Vector3 view = cam.transform.forward; view.y = 0f;
            tv.SetPositionAndRotation(position, Quaternion.LookRotation(-view.normalized));
            tv.localScale = _tvHomeScale * tvScale;
            SetStandVisible(false);
        }

        private void RestoreTelevision()
        {
            if (television == null || !_tvHomeSaved) return;
            var tv = television.transform;
            SetStandVisible(true);
            tv.SetPositionAndRotation(_tvHomePosition, _tvHomeRotation);
            tv.localScale = _tvHomeScale;
            var cam = Camera.main;
            if (player != null && cam != null)
            {
                Vector3 forward = cam.transform.forward; forward.y = 0f;
                player.LookTowards(cam.transform.position + forward.normalized); // mirada otra vez a nivel
            }
        }

        private void SetStandVisible(bool visible)
        {
            foreach (Transform child in television.transform)
                if (System.Array.IndexOf(TvStand, child.name) >= 0) child.gameObject.SetActive(visible);
        }

        private void OnPracticeRunningChanged()
        {
            if (IsOpen && _level != null) RefreshPracticeButton(_level.items[_index]);
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
            PlayVideo(_index);
            if (practice != null) practice.SetSign(_level.ToSignEntry(_level.items[_index]));
            Refresh();
        }

        /// <summary>Proyecta en el televisor el video del elemento; si no tiene, la pantalla queda apagada.</summary>
        private void PlayVideo(int index)
        {
            if (television == null) return;
            var clip = itemVideos != null && index >= 0 && index < itemVideos.Length ? itemVideos[index] : null;
            television.Play(clip);
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
                    int number = int.Parse(item.id.Substring("numeros.".Length));
                    bigLabel.text = number.ToString();
                    nameLabel.text = item.Name.ToUpperInvariant();
                    positionLabel.text = Loc.T("levels.numberOf", _index + 1, total);
                    if (countDots != null && countDots.Length > 0)
                    {
                        bool showCount = number <= countDots.Length;
                        if (countDots[0] != null) countDots[0].transform.parent.gameObject.SetActive(showCount);
                        if (showCount)
                            for (int i = 0; i < countDots.Length; i++)
                                if (countDots[i] != null) ThemedGraphic.Set(countDots[i], i < number ? Coral : Sand);
                    }
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

            if (instructionLabel != null) instructionLabel.text = _level.Hint(item);
            RefreshPracticeButton(item);
            previousButton.interactable = _index > 0;
            nextButton.interactable = _index < total - 1;
            progressBar.Set((float)_seen.Count / total, Loc.T("levels.seen", _seen.Count, total));
        }

        /// <summary>
        /// PRACTICAR solo si Unity ya implementa el nivel y el backend no tiene la seña BLOQUEADA. Si todavía no se conoce el
        /// estado (sin sesión o sin conexión) no se deja practicar: el resultado no se podría guardar.
        /// </summary>
        private void RefreshPracticeButton(LevelItem item)
        {
            var label = practiceButton.GetComponentInChildren<TMP_Text>();
            string text = Loc.T(practice != null ? "levels.start" : "practice.go");
            bool enabled = true;

            if (!SignCodes.IsImplementedInUnity(_levelCode))
            {
                text = Loc.T("levels.comingSoon");
                enabled = false;
            }
            else
            {
                string status = ProgressStore.StatusOf(item.id); // StatusOf traduce el id de Unity (numeros.N, meses.enero) al código del backend
                if (status == SignStatusNames.Locked) { text = Loc.T("levels.signLocked"); enabled = false; }
                // Con práctica dentro de la casa, un estado desconocido (sin sesión o sin conexión) no bloquea EMPEZAR: se puede
                // practicar y, si no se puede guardar, el panel lo dice. En Practice (otra escena) sí hace falta el estado.
                else if (status == null && practice == null) enabled = false;
            }

            if (practice != null && practice.IsRunning) { text = Loc.T("levels.cancel"); enabled = true; }
            practiceButton.interactable = enabled;
            if (label != null) label.text = text;
        }

        private void OnLearn()
        {
            // VER SEÑA: si la casa tiene televisor con video de esta seña, lo vuelve a reproducir.
            if (television != null && itemVideos != null && _index < itemVideos.Length && itemVideos[_index] != null) { PlayVideo(_index); return; }
            if (DemonstrationAvailable) DemonstrationRequested.Invoke();
            else demoNotice.gameObject.SetActive(true);
        }

        private void OnPractice()
        {
            if (practice != null) { practice.Toggle(); return; }
            PracticeSession.BeginFromLevel(_level.ToSignEntry(_level.items[_index]), _level.sceneName);
            ScreenFader.Load(SceneNames.Practice);
        }
    }
}
