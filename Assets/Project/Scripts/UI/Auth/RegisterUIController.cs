using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Lumina.Auth;

namespace Lumina.UI
{
    /// <summary>
    /// Pantalla de creación de cuenta. Valida el formulario y el avatar, pide el resultado a
    /// <see cref="AuthServices.Current"/> (simulación local: no se envía ni se guarda nada) y muestra una bienvenida
    /// desde la que se continúa al menú principal.
    /// </summary>
    public class RegisterUIController : MonoBehaviour
    {
        [Header("Formulario")]
        [SerializeField] private AuthField nameField;
        [SerializeField] private AuthField usernameField;
        [SerializeField] private AuthField emailField;
        [SerializeField] private AuthField passwordField;
        [SerializeField] private AuthField confirmField;
        [SerializeField] private Button createButton;
        [SerializeField] private TMP_Text createLabel;
        [SerializeField] private Button backButton;
        [SerializeField] private StatusMessage status;
        [SerializeField] private LoadingDots loading;
        [SerializeField] private AvatarSelectorController avatars;

        [Header("Presentación")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup panelGroup;

        [Header("Bienvenida")]
        [SerializeField] private CanvasGroup welcomeGroup;
        [SerializeField] private TMP_Text welcomeTitle;
        [SerializeField] private TMP_Text welcomeBody;
        [SerializeField] private Button continueButton;

        private CancellationTokenSource _cts;
        private bool _busy;
        private string _createLabelText;

        private void Awake()
        {
            if (nameField == null || usernameField == null || emailField == null || passwordField == null || confirmField == null ||
                createButton == null || backButton == null || status == null || avatars == null || panel == null || panelGroup == null ||
                welcomeGroup == null || welcomeTitle == null || welcomeBody == null || continueButton == null)
            {
                Debug.LogError($"[RegisterUIController] Referencias incompletas en '{name}'.", this);
                enabled = false;
                return;
            }

            _cts = new CancellationTokenSource();
            _createLabelText = createLabel != null ? createLabel.text : string.Empty;

            createButton.onClick.AddListener(OnCreateClicked);
            backButton.onClick.AddListener(() => { if (!_busy) ScreenFader.Load(SceneNames.Login); });
            continueButton.onClick.AddListener(() => ScreenFader.Load(SceneNames.MainMenu));

            // Un solo botón alterna ambas contraseñas.
            passwordField.RevealToggled += revealed => confirmField.SetRevealed(revealed, false);

            nameField.Submitted += usernameField.Focus;
            usernameField.Submitted += emailField.Focus;
            emailField.Submitted += passwordField.Focus;
            passwordField.Submitted += confirmField.Focus;
            confirmField.Submitted += OnCreateClicked;

            foreach (var field in new[] { nameField, usernameField, emailField, passwordField, confirmField })
                field.TextChanged += () => { if (!_busy) status.Clear(); };
            avatars.Selected += id => { AuthSession.SelectAvatar(id); status.Clear(); };

            if (loading != null) loading.gameObject.SetActive(false);
            welcomeGroup.gameObject.SetActive(false);
        }

        private void Start()
        {
            panelGroup.alpha = 0f;
            StartCoroutine(Intro());
            status.Info("Cuéntanos quién eres y elige a tu compañero de aventura.");
            nameField.Focus();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private IEnumerator Intro()
        {
            Vector2 home = panel.anchoredPosition;
            const float duration = 0.7f;
            yield return null;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / duration, 3f);
                panelGroup.alpha = k;
                panel.anchoredPosition = home + Vector2.right * (70f * (1f - k));
                yield return null;
            }
            panelGroup.alpha = 1f;
            panel.anchoredPosition = home;
        }

        private bool Validate()
        {
            string[] errors =
            {
                AuthValidation.Required(nameField.Text, "tu nombre"),
                AuthValidation.Username(usernameField.Text),
                AuthValidation.Email(emailField.Text),
                AuthValidation.Password(passwordField.Text),
                AuthValidation.PasswordConfirmation(passwordField.Text, confirmField.Text)
            };
            var fields = new[] { nameField, usernameField, emailField, passwordField, confirmField };

            bool ok = true;
            AuthField first = null;
            for (int i = 0; i < fields.Length; i++)
            {
                if (errors[i] != null) { fields[i].SetError(errors[i]); ok = false; first ??= fields[i]; }
                else fields[i].ClearError();
            }

            if (!avatars.HasSelection)
            {
                status.Error("Elige un avatar con el botón SELECCIONAR.");
                avatars.Nudge();
                return false;
            }
            if (!ok)
            {
                status.Error("Revisa los campos marcados.");
                first?.Focus();
            }
            return ok;
        }

        private async void OnCreateClicked()
        {
            if (_busy) return;
            if (!Validate()) return;

            SetBusy(true);
            status.Clear();

            try
            {
                var data = new RegistrationData
                {
                    PlayerName = nameField.Text.Trim(),
                    Username = usernameField.Text.Trim(),
                    Email = emailField.Text.Trim(),
                    Password = passwordField.Text,
                    AvatarId = avatars.SelectedId
                };
                var result = await AuthServices.Current.RegisterAsync(data, _cts.Token);
                if (this == null) return;

                if (!result.Succeeded)
                {
                    if (result.Status == AuthStatus.EmailAlreadyUsed) emailField.SetError(result.Message);
                    else if (result.Status == AuthStatus.UsernameTaken) usernameField.SetError(result.Message);
                    status.Error(result.Message ?? "No se pudo crear la cuenta.");
                    SetBusy(false);
                    return;
                }

                AuthSession.SignIn(result.DisplayName ?? data.PlayerName, data.AvatarId);
                ShowWelcome(data.PlayerName);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this == null) return;
                status.Error("Algo salió mal. Inténtalo otra vez.");
                SetBusy(false);
            }
        }

        private void ShowWelcome(string playerName)
        {
            welcomeTitle.text = $"¡Bienvenido a Lúmina, {playerName}!";
            welcomeBody.text = $"Tu compañero {avatars.SelectedName} ya te espera.\nEsta es una cuenta de prueba: no se guardó ninguna información.";
            welcomeGroup.gameObject.SetActive(true);
            StartCoroutine(FadeWelcome());
        }

        private IEnumerator FadeWelcome()
        {
            welcomeGroup.alpha = 0f;
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                welcomeGroup.alpha = t / 0.4f;
                yield return null;
            }
            welcomeGroup.alpha = 1f;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            createButton.interactable = !busy;
            backButton.interactable = !busy;
            foreach (var field in new[] { nameField, usernameField, emailField, passwordField, confirmField })
                field.SetInteractable(!busy);
            if (loading != null) loading.gameObject.SetActive(busy);
            if (createLabel != null) createLabel.text = busy ? string.Empty : _createLabelText;
            if (!busy && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(createButton.gameObject);
        }
    }
}
