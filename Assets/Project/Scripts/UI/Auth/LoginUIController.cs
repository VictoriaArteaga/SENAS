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
    /// Pantalla de inicio de sesión. Solo coordina la interfaz: valida los campos, pide el resultado a
    /// <see cref="AuthServices.Current"/> (hoy una simulación local) y navega. No conoce al proveedor de autenticación.
    /// </summary>
    public class LoginUIController : MonoBehaviour
    {
        [Header("Formulario")]
        [SerializeField] private AuthField emailField;
        [SerializeField] private AuthField passwordField;
        [SerializeField] private Toggle rememberToggle;
        [SerializeField] private Button loginButton;
        [SerializeField] private TMP_Text loginLabel;
        [SerializeField] private Button registerButton;
        [SerializeField] private Button forgotButton;
        [SerializeField] private StatusMessage status;
        [SerializeField] private LoadingDots loading;

        [Header("Presentación")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private SenaSpeaker sena;

        private const string MsgWelcome = "¡Qué alegría verte de nuevo!";
        private const string MsgTyping = "¡Vamos a continuar aprendiendo!";
        private const string MsgLoading = "Un momento, estoy preparando tu aventura.";
        private const string MsgError = "¡Ups! Revisemos tus datos.";
        private const string MsgSuccess = "¡Excelente! ¡Volvamos a explorar Lúmina!";

        private CancellationTokenSource _cts;
        private bool _busy;
        private bool _typingSaid;
        private string _loginLabelText;

        private void Awake()
        {
            if (emailField == null || passwordField == null || loginButton == null || registerButton == null ||
                forgotButton == null || status == null || panel == null || panelGroup == null)
            {
                Debug.LogError($"[LoginUIController] Referencias incompletas en '{name}'.", this);
                enabled = false;
                return;
            }

            _cts = new CancellationTokenSource();
            _loginLabelText = loginLabel != null ? loginLabel.text : string.Empty;

            loginButton.onClick.AddListener(OnLoginClicked);
            registerButton.onClick.AddListener(OnRegisterClicked);
            forgotButton.onClick.AddListener(OnForgotClicked);
            emailField.Submitted += () => passwordField.Focus();
            passwordField.Submitted += OnLoginClicked;
            emailField.TextChanged += OnTyping;
            passwordField.TextChanged += OnTyping;
            if (loading != null) loading.gameObject.SetActive(false);
        }

        private void Start()
        {
            panelGroup.alpha = 0f;
            StartCoroutine(Intro());
            sena?.Say(MsgWelcome);
            sena?.Wave();
            emailField.Focus();
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

        private void OnTyping()
        {
            if (_busy) return;
            status.Clear();
            if (_typingSaid) return;
            _typingSaid = true;
            sena?.Say(MsgTyping);
        }

        private bool Validate()
        {
            string emailError = AuthValidation.Email(emailField.Text);
            string passwordError = AuthValidation.Password(passwordField.Text);
            if (emailError != null) emailField.SetError(emailError); else emailField.ClearError();
            if (passwordError != null) passwordField.SetError(passwordError); else passwordField.ClearError();
            return emailError == null && passwordError == null;
        }

        private async void OnLoginClicked()
        {
            if (_busy) return;

            if (!Validate())
            {
                status.Error("Revisa los campos marcados.");
                sena?.Say(MsgError);
                return;
            }

            SetBusy(true);
            status.Clear();
            sena?.Say(MsgLoading);

            try
            {
                var result = await AuthServices.Current.SignInAsync(emailField.Text.Trim(), passwordField.Text, _cts.Token);
                if (this == null) return;

                if (!result.Succeeded)
                {
                    status.Error(result.Message ?? "No se pudo iniciar sesión.");
                    sena?.Say(MsgError);
                    SetBusy(false);
                    return;
                }

                status.Success("¡Listo! Entrando a Lúmina…");
                sena?.Say(MsgSuccess);
                sena?.Clap();
                AuthSession.SignIn(result.DisplayName);
                await Task.Delay(1500, _cts.Token);
                if (this == null) return;
                ScreenFader.Load(SceneNames.MainMenu);
            }
            catch (OperationCanceledException)
            {
                // La escena se cerró durante la espera; no hay nada que mostrar.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this == null) return;
                status.Error("Algo salió mal. Inténtalo otra vez.");
                sena?.Say(MsgError);
                SetBusy(false);
            }
        }

        private void OnRegisterClicked()
        {
            if (_busy) return;
            ScreenFader.Load(SceneNames.Register);
        }

        private async void OnForgotClicked()
        {
            if (_busy) return;

            string emailError = AuthValidation.Email(emailField.Text);
            if (emailError != null)
            {
                emailField.SetError(emailError);
                status.Info("Escribe tu correo y vuelve a pulsar «¿Olvidé mi contraseña?».");
                sena?.Say("Dime tu correo y te ayudo a recuperar tu cuenta.");
                emailField.Focus();
                return;
            }

            SetBusy(true);
            try
            {
                await AuthServices.Current.RequestPasswordResetAsync(emailField.Text.Trim(), _cts.Token);
                if (this == null) return;
                status.Info("Función de prueba: aún no se envía ningún correo real.");
                sena?.Say("Pronto podrás recuperar tu contraseña desde aquí.");
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (this != null) SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            loginButton.interactable = !busy;
            registerButton.interactable = !busy;
            forgotButton.interactable = !busy;
            emailField.SetInteractable(!busy);
            passwordField.SetInteractable(!busy);
            if (rememberToggle != null) rememberToggle.interactable = !busy;
            if (loading != null) loading.gameObject.SetActive(busy);
            if (loginLabel != null) loginLabel.text = busy ? string.Empty : _loginLabelText;
            if (!busy && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(loginButton.gameObject);
        }
    }
}
