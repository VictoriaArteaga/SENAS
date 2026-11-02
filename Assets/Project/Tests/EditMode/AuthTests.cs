using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Lumina.Auth.Tests
{
    /// <summary>Pruebas de la capa de autenticación simulada: validaciones y servicio local. No usan red ni disco.</summary>
    public class AuthValidationTests
    {
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Email_VacioPideEscribirlo(string value) => Assert.IsNotNull(AuthValidation.Email(value));

        [TestCase("abc")]
        [TestCase("ana@correo")]
        [TestCase("ana correo@x.com")]
        [TestCase("@correo.com")]
        public void Email_FormatoInvalidoSeRechaza(string value) => Assert.IsNotNull(AuthValidation.Email(value));

        [Test]
        public void Email_SinArrobaDiceQueFaltaElArroba()
        {
            StringAssert.Contains("@", AuthValidation.Email("anacorreo.com"));
            StringAssert.Contains("@", AuthValidation.Email("abc"));
        }

        [Test]
        public void Email_MensajesCortosParaQueEntrenEnUnaLinea()
        {
            foreach (var v in new[] { "", "abc", "ana@correo", "@correo.com" })
                Assert.LessOrEqual(AuthValidation.Email(v).Length, 50, v);
        }

        [TestCase("ana@correo.com")]
        [TestCase("  ana.perez+lsc@dominio.co  ")]
        public void Email_FormatoValidoSeAcepta(string value) => Assert.IsNull(AuthValidation.Email(value));

        [Test]
        public void Password_RespetaLasReglasDelBackend()
        {
            // 8 a 72 caracteres, con al menos una letra y un número (POST /auth/register).
            Assert.IsNotNull(AuthValidation.Password(""));
            Assert.IsNotNull(AuthValidation.Password("abc1234"));                                   // 7: corta
            Assert.IsNull(AuthValidation.Password("abcd1234"));                                     // 8: válida
            Assert.IsNull(AuthValidation.Password("Lumina2026"));
            Assert.IsNull(AuthValidation.Password(new string('a', AuthValidation.MaxPasswordLength - 1) + "1")); // 72: válida
            Assert.IsNotNull(AuthValidation.Password(new string('a', AuthValidation.MaxPasswordLength) + "1")); // 73: larga
            Assert.IsNotNull(AuthValidation.Password("12345678"));                                  // sin letra
            Assert.IsNotNull(AuthValidation.Password("soloLetras"));                                // sin número
        }

        [Test]
        public void LoginPassword_SoloExigeQueExista()
        {
            Assert.IsNotNull(AuthValidation.LoginPassword(""));
            Assert.IsNull(AuthValidation.LoginPassword("x"));
            Assert.IsNotNull(AuthValidation.LoginPassword(new string('a', AuthValidation.MaxPasswordLength + 1)));
        }

        [Test]
        public void DisplayName_Entre2Y40Caracteres()
        {
            Assert.IsNotNull(AuthValidation.DisplayName("  "));
            Assert.IsNotNull(AuthValidation.DisplayName("A"));
            Assert.IsNull(AuthValidation.DisplayName("Ana"));
            Assert.IsNull(AuthValidation.DisplayName(new string('a', AuthValidation.MaxDisplayNameLength)));
            Assert.IsNotNull(AuthValidation.DisplayName(new string('a', AuthValidation.MaxDisplayNameLength + 1)));
        }

        [Test]
        public void Username_ValidaLongitudYCaracteres()
        {
            Assert.IsNotNull(AuthValidation.Username(""));
            Assert.IsNotNull(AuthValidation.Username("ab"));
            Assert.IsNotNull(AuthValidation.Username(new string('a', AuthValidation.MaxUsernameLength + 1)));
            Assert.IsNotNull(AuthValidation.Username("con espacio"));
            Assert.IsNull(AuthValidation.Username("ana_01.lsc"));
        }

        [Test]
        public void Confirmacion_DebeCoincidir()
        {
            Assert.IsNotNull(AuthValidation.PasswordConfirmation("secreto1", ""));
            Assert.IsNotNull(AuthValidation.PasswordConfirmation("secreto1", "secreto2"));
            Assert.IsNull(AuthValidation.PasswordConfirmation("secreto1", "secreto1"));
        }

        [Test]
        public void Required_RechazaVacios()
        {
            Assert.IsNotNull(AuthValidation.Required("  ", "tu nombre"));
            Assert.IsNull(AuthValidation.Required("Ana", "tu nombre"));
        }
    }

    public class AuthenticationMockServiceTests
    {
        private static IAuthenticationService Service() => new AuthenticationMockService(0);

        [Test]
        public async Task SignIn_AceptaDatosValidosYDeduceElNombre()
        {
            var result = await Service().SignInAsync("ana@correo.com", "secreto1");
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("Ana", result.DisplayName);
        }

        [Test]
        public async Task SignIn_CorreoReservadoSimulaCredencialesInvalidas()
        {
            var result = await Service().SignInAsync(AuthenticationMockService.ReservedErrorEmail, "secreto1");
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(AuthStatus.InvalidCredentials, result.Status);
        }

        [Test]
        public async Task Register_SimulaCorreoYUsuarioRepetidos()
        {
            var emailTaken = await Service().RegisterAsync(new RegistrationData { PlayerName = "Ana", Username = "ana_01", Email = AuthenticationMockService.ReservedErrorEmail });
            Assert.AreEqual(AuthStatus.EmailAlreadyUsed, emailTaken.Status);

            var userTaken = await Service().RegisterAsync(new RegistrationData { PlayerName = "Ana", Username = "USADO", Email = "ana@correo.com" });
            Assert.AreEqual(AuthStatus.UsernameTaken, userTaken.Status);

            var ok = await Service().RegisterAsync(new RegistrationData { PlayerName = "Ana", Username = "ana_01", Email = "ana@correo.com" });
            Assert.IsTrue(ok.Succeeded);
            Assert.AreEqual("Ana", ok.DisplayName);
        }

        [Test]
        public async Task PasswordReset_EsSoloUnaSimulacion()
        {
            var result = await Service().RequestPasswordResetAsync("ana@correo.com");
            Assert.IsTrue(result.Succeeded);
            StringAssert.Contains("Simulación", result.Message);
        }

        [Test]
        public void CancelarLaOperacionLanza()
        {
            var service = new AuthenticationMockService(5000);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                Assert.ThrowsAsync<TaskCanceledException>(async () => await service.SignInAsync("ana@correo.com", "secreto1", cts.Token));
            }
        }

        [Test]
        public void ElServicioActualSePuedeSustituir()
        {
            var original = AuthServices.Current;
            try
            {
                var replacement = new AuthenticationMockService(0);
                AuthServices.Current = replacement;
                Assert.AreSame(replacement, AuthServices.Current);
            }
            finally { AuthServices.Current = original; }
        }

        [Test]
        public void ElAvatarElegidoSeConservaAlIniciarSesionPeroSeBorraAlCerrarla()
        {
            AuthSession.Clear();
            AuthSession.SelectAvatar("nube");
            Assert.AreEqual("nube", AuthSession.AvatarId);
            AuthSession.SignIn("Ana");
            Assert.AreEqual("nube", AuthSession.AvatarId, "iniciar sesión sin avatar no debe borrar la elección");
            AuthSession.Clear();
            Assert.IsNull(AuthSession.AvatarId, "cerrar sesión borra el avatar: el de la cuenta siguiente sale de su perfil");
            AuthSession.SelectAvatar("sol");
            Assert.AreEqual("sol", AuthSession.AvatarId);
            AuthSession.SelectAvatar(null);
            Assert.AreEqual("sol", AuthSession.AvatarId, "un valor vacío no pisa la elección");
        }

        [Test]
        public void LaSesionSoloViveEnMemoria()
        {
            AuthSession.SignIn("Ana", "luna");
            Assert.IsTrue(AuthSession.IsSignedIn);
            Assert.AreEqual("luna", AuthSession.AvatarId);
            AuthSession.Clear();
            Assert.IsFalse(AuthSession.IsSignedIn);
            Assert.IsNull(AuthSession.DisplayName);
        }
    }
}
