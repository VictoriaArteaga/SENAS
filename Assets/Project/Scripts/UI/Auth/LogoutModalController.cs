using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Lumina.Auth;

using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>
    /// Ventana de confirmación para cerrar sesión, integrada en el menú principal (que permanece visible detrás).
    /// Sena se muestra en una textura (cámara propia) y se despide con su animación Wave. El cierre de sesión es solo
    /// visual: se limpia el estado en memoria y se vuelve a la pantalla de acceso; no hay datos persistentes que borrar.
    /// </summary>
    public class LogoutModalController : MonoBehaviour
    {
        private static readonly int WaveTrigger = Animator.StringToHash("Wave");

        [Header("Apertura")]
        [SerializeField] private Button openButton;

        [Header("Ventana")]
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        [Header("Sena")]
        [SerializeField] private RawImage senaView;
        [Tooltip("Escenario de Sena. Si está vacío se busca en la escena (prefab LogoutSenaStage).")]
        [SerializeField] private LogoutSenaStage stage;
        [SerializeField] private Vector2Int textureSize = new Vector2Int(560, 760);

        private const string Title = "¿Te vas de Lúmina?";
        private const string Message = "Sena te espera para continuar la aventura cuando regreses.";
        private const string Farewell = "¡Hasta pronto! Aquí estaré.";

        private RenderTexture _texture;
        private CancellationTokenSource _cts;
        private Coroutine _fade;
        private bool _open;
        private bool _leaving;

        public bool IsOpen => _open;

        private void Awake()
        {
            if (visualRoot == null || group == null || card == null || titleLabel == null || messageLabel == null ||
                confirmButton == null || cancelButton == null)
            {
                Debug.LogError($"[LogoutModalController] Referencias incompletas en '{name}'.", this);
                enabled = false;
                return;
            }

            _cts = new CancellationTokenSource();
            if (stage == null) stage = FindFirstObjectByType<LogoutSenaStage>(FindObjectsInactive.Include);
            if (stage == null) Debug.LogWarning($"[LogoutModalController] No hay un LogoutSenaStage en la escena; la ventana se mostrará sin Sena.", this);

            if (openButton != null) openButton.onClick.AddListener(Open);
            cancelButton.onClick.AddListener(Close);
            confirmButton.onClick.AddListener(OnConfirmClicked);

            // La navegación con teclado queda dentro de la ventana mientras está abierta.
            SetExplicitNavigation(confirmButton, cancelButton);
            SetExplicitNavigation(cancelButton, confirmButton);

            titleLabel.text = Loc.Translate(Title);
            messageLabel.text = Loc.Translate(Message);
            visualRoot.SetActive(false);
        }

        private void Update()
        {
            if (_open && !_leaving && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            ReleaseTexture();
        }

        public void Open()
        {
            if (_open || _leaving) return;
            _open = true;

            titleLabel.text = Loc.Translate(Title);
            messageLabel.text = Loc.Translate(Message);
            confirmButton.interactable = cancelButton.interactable = true;

            if (stage != null && stage.StageCamera != null && senaView != null)
            {
                if (_texture == null)
                {
                    _texture = new RenderTexture(textureSize.x, textureSize.y, 24, RenderTextureFormat.ARGB32) { name = "LogoutSenaView" };
                    _texture.Create();
                }
                stage.StageCamera.targetTexture = _texture;
                stage.StageCamera.enabled = true;
                senaView.texture = _texture;
            }

            visualRoot.SetActive(true);
            RestartFade(true);
            PlayWave();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
        }

        public void Close()
        {
            if (!_open || _leaving) return;
            _open = false;
            RestartFade(false);
            if (EventSystem.current != null && openButton != null) EventSystem.current.SetSelectedGameObject(openButton.gameObject);
        }

        private async void OnConfirmClicked()
        {
            if (_leaving) return;
            _leaving = true;
            confirmButton.interactable = cancelButton.interactable = false;
            messageLabel.text = Loc.Translate(Farewell);
            PlayWave();

            try
            {
                await AuthServices.Current.SignOutAsync(_cts.Token);
                await Task.Delay(1700, _cts.Token);
                if (this == null) return;
                AuthSession.Clear();
                if (!ScreenFader.Load(SceneNames.Login))
                {
                    // Sin escena de acceso registrada no se puede salir: se restablece la ventana.
                    _leaving = false;
                    confirmButton.interactable = cancelButton.interactable = true;
                    messageLabel.text = Loc.Translate(Message);
                }
            }
            catch (OperationCanceledException) { }
        }

        private void PlayWave()
        {
            var animator = stage != null ? stage.SenaAnimator : null;
            if (animator != null && animator.isActiveAndEnabled) animator.SetTrigger(WaveTrigger);
        }

        private void RestartFade(bool opening)
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(Fade(opening));
        }

        private IEnumerator Fade(bool opening)
        {
            float from = group.alpha, to = opening ? 1f : 0f;
            float duration = opening ? 0.28f : 0.18f;
            group.blocksRaycasts = opening;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                group.alpha = Mathf.Lerp(from, to, k);
                float scale = Mathf.Lerp(0.9f, 1f, opening ? k : group.alpha);
                card.localScale = Vector3.one * scale;
                yield return null;
            }
            group.alpha = to;
            card.localScale = Vector3.one * (opening ? 1f : 0.9f);

            if (!opening)
            {
                visualRoot.SetActive(false);
                if (stage != null && stage.StageCamera != null) stage.StageCamera.enabled = false;
            }
            _fade = null;
        }

        private static void SetExplicitNavigation(Button button, Button other)
        {
            button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = other, selectOnRight = other, selectOnUp = other, selectOnDown = other
            };
        }

        private void ReleaseTexture()
        {
            if (_texture == null) return;
            if (stage != null && stage.StageCamera != null) stage.StageCamera.targetTexture = null;
            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }
    }
}
