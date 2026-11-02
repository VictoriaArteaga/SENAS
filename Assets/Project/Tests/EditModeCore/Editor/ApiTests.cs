using Lumina.Api;
using Lumina.Signs;
using NUnit.Framework;
using UnityEngine;

namespace Lumina.Core.Tests
{
    /// <summary>Pruebas de la capa de API que no necesitan red: JSON, modelos, códigos de seña y adaptador de puntuación.</summary>
    public class ApiTests
    {
        // ---------------------------------------------------------------- JsonObject
        [Test]
        public void JsonObject_ArmaElCuerpoDeUnRegistro()
        {
            string json = new JsonObject().Add("email", "ana@correo.com").Add("password", "Lumina2026").Add("displayName", "Ana").Add("avatarId", "luna").ToString();
            Assert.AreEqual("{\"email\":\"ana@correo.com\",\"password\":\"Lumina2026\",\"displayName\":\"Ana\",\"avatarId\":\"luna\"}", json);
        }

        [Test]
        public void JsonObject_EscapaComillasBarrasYSaltos()
        {
            string json = new JsonObject().Add("password", "a\"b\\c\nd").ToString();
            Assert.AreEqual("{\"password\":\"a\\\"b\\\\c\\nd\"}", json);
        }

        [Test]
        public void JsonObject_NumerosConPuntoYSinNotacionCientifica()
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("es-CO"); // coma decimal
                string json = new JsonObject().Add("precision", 87.99).Add("p2", 82.0).Add("stars", 3).ToString();
                Assert.AreEqual("{\"precision\":87.99,\"p2\":82,\"stars\":3}", json);
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = previous; }
        }

        [Test]
        public void JsonObject_PreferenciasAnidadas()
        {
            string json = new JsonObject().Add("preferences", new JsonObject().Add("language", "en").Add("theme", "dark")).ToString();
            Assert.AreEqual("{\"preferences\":{\"language\":\"en\",\"theme\":\"dark\"}}", json);
        }

        [Test]
        public void JsonObject_VacioSeDetecta() => Assert.IsTrue(new JsonObject().IsEmpty);

        // ---------------------------------------------------------------- Modelos (formato real del backend)
        [Test]
        public void Modelos_LeenLaRespuestaDeLogin()
        {
            const string json = "{\"accessToken\":\"a.b.c\",\"refreshToken\":\"id.secret\",\"tokenType\":\"Bearer\",\"expiresIn\":900,\"user\":{\"id\":\"u1\",\"email\":\"ana@correo.com\",\"role\":\"PLAYER\"}}";
            var tokens = JsonUtility.FromJson<TokensResponse>(json);
            Assert.AreEqual("a.b.c", tokens.accessToken);
            Assert.AreEqual("id.secret", tokens.refreshToken);
            Assert.AreEqual(900, tokens.expiresIn);
            Assert.AreEqual("u1", tokens.user.id);
        }

        [Test]
        public void Modelos_LeenElPerfilConPreferenciasVacias()
        {
            const string json = "{\"userId\":\"u1\",\"email\":\"a@b.co\",\"displayName\":\"Ana\",\"avatarId\":\"luna\",\"avatarConfig\":{},\"preferences\":{},\"totalXp\":310,\"createdAt\":\"2026-11-01T15:00:00.000Z\"}";
            var profile = JsonUtility.FromJson<ProfileDto>(json);
            Assert.AreEqual("Ana", profile.displayName);
            Assert.AreEqual(310, profile.totalXp);
            Assert.IsTrue(string.IsNullOrEmpty(profile.preferences.language));
        }

        [Test]
        public void Modelos_LeenElDetalleDeUnNivelConNulos()
        {
            const string json = "{\"code\":\"ALPHABET\",\"name\":\"Alphabet\",\"position\":1,\"unlocked\":true,\"requiredXp\":0,\"missingXp\":0,\"passedSigns\":1,\"totalSigns\":27,\"earnedStars\":2,\"possibleStars\":81," +
                "\"signs\":[{\"code\":\"A\",\"name\":\"Letter A\",\"type\":\"LETTER\",\"position\":1,\"status\":\"PASSED\",\"bestPrecision\":82.5,\"bestStars\":2}," +
                "{\"code\":\"B\",\"name\":\"Letter B\",\"type\":\"LETTER\",\"position\":2,\"status\":\"AVAILABLE\",\"bestPrecision\":null,\"bestStars\":0}]}";
            var level = JsonUtility.FromJson<LevelDetailDto>(json);
            Assert.AreEqual(2, level.signs.Length);
            Assert.AreEqual(SignStatusNames.Passed, level.signs[0].status);
            Assert.AreEqual(82.5f, level.signs[0].bestPrecision, 0.001f);
            Assert.AreEqual(SignStatusNames.Available, level.signs[1].status);
        }

        [Test]
        public void Modelos_LeenLaRespuestaDeUnaEvaluacion()
        {
            const string json = "{\"evaluationId\":\"e1\",\"attemptId\":\"a1\",\"signCode\":\"numeros.5\",\"passed\":true,\"precision\":82,\"stars\":2,\"awardedXp\":20,\"totalXp\":310,\"bestPrecision\":82,\"bestStars\":2,\"unlockedSign\":\"numeros.6\",\"unlockedLevels\":[\"NUMBERS\"]}";
            var r = JsonUtility.FromJson<EvaluationResponse>(json);
            Assert.IsTrue(r.passed);
            Assert.AreEqual(20, r.awardedXp);
            Assert.AreEqual("numeros.6", r.unlockedSign);
            CollectionAssert.AreEqual(new[] { "NUMBERS" }, r.unlockedLevels);
        }

        [Test]
        public void Modelos_UnArregloEnLaRaizSeEnvuelveParaJsonUtility()
        {
            // ApiClient envuelve los arreglos de la raíz como {"items": [...]} (GET /levels).
            const string array = "[{\"code\":\"ALPHABET\",\"unlocked\":true},{\"code\":\"NUMBERS\",\"unlocked\":false}]";
            var list = ApiClient.Parse<LevelListDto>("{\"items\":" + array + "}");
            Assert.AreEqual(2, list.items.Length);
            Assert.IsFalse(list.items[1].unlocked);
        }

        [Test]
        public void Modelos_LeenElErrorDelBackend()
        {
            const string json = "{\"statusCode\":403,\"code\":\"SIGN_LOCKED\",\"message\":\"Pass the previous sign.\",\"path\":\"/api/v1/evaluations\",\"timestamp\":\"x\"}";
            var error = JsonUtility.FromJson<ApiErrorBody>(json);
            Assert.AreEqual("SIGN_LOCKED", error.code);
            Assert.AreEqual(403, error.statusCode);
        }

        // ---------------------------------------------------------------- Códigos de seña
        [TestCase("numeros.0", true)]
        [TestCase("numeros.30", true)]
        [TestCase("numeros.31", false)]
        [TestCase("numeros.uno", false)]
        [TestCase("meses.enero", false)]
        [TestCase("A", false)]
        public void SignCodes_ReconoceLosNumeros(string id, bool expected) => Assert.AreEqual(expected, SignCodes.IsNumber(id));

        [Test]
        public void SignCodes_LasLetrasSonLaLetraSuelta()
        {
            Assert.AreEqual("A", SignCodes.ForLetter("a"));
            Assert.AreEqual("Ñ", SignCodes.ForLetter("ñ"));
            Assert.AreEqual("Ñ", SignCodes.ForLetter("Ñ"));
        }

        [TestCase("A", SignCodes.AlphabetLevel)]
        [TestCase("Ñ", SignCodes.AlphabetLevel)]
        [TestCase("numeros.12", SignCodes.NumbersLevel)]
        [TestCase("MONTH_JANUARY", SignCodes.MonthsLevel)]
        [TestCase("WORD_HELLO", SignCodes.GreetingsLevel)]
        [TestCase("PHRASE_GOOD_MORNING", SignCodes.GreetingsLevel)]
        public void SignCodes_NivelDeCadaCodigo(string code, string level) => Assert.AreEqual(level, SignCodes.LevelFor(code));

        [Test]
        public void SignCodes_LosCodigosDesconocidosNoTienenNivel()
        {
            Assert.IsNull(SignCodes.LevelFor("saludos.hola"));
            Assert.IsNull(SignCodes.LevelFor("colores.rojo"));
            Assert.IsNull(SignCodes.LevelFor(null));
        }

        [Test]
        public void SignCodes_SoloAbecedarioYNumerosEstanImplementados()
        {
            Assert.IsTrue(SignCodes.IsImplementedInUnity(SignCodes.AlphabetLevel));
            Assert.IsTrue(SignCodes.IsImplementedInUnity(SignCodes.NumbersLevel));
            Assert.IsFalse(SignCodes.IsImplementedInUnity(SignCodes.MonthsLevel));
            Assert.IsFalse(SignCodes.IsImplementedInUnity(SignCodes.GreetingsLevel));
        }

        [Test]
        public void ApiClient_CodificaLosSegmentosDeRuta()
        {
            Assert.AreEqual("%C3%91", ApiClient.Segment("Ñ"));
            Assert.AreEqual("numeros.5", ApiClient.Segment("numeros.5"));
        }

        // ---------------------------------------------------------------- Adaptador de puntuación (misma tabla que la IA y el backend)
        [TestCase(0.5999f, 59.99, 0, 0)]
        [TestCase(0.60f, 60.0, 1, 10)]
        // 0,7399f y 0,8799f no son exactamente esos decimales como float (0,73989999… y 0,87989997…): se truncan a 73,98 y 87,98.
        [TestCase(0.7399f, 73.98, 1, 10)]
        [TestCase(0.74f, 74.0, 2, 20)]
        [TestCase(0.8799f, 87.98, 2, 20)]
        [TestCase(0.88f, 88.0, 3, 30)]
        [TestCase(1f, 100.0, 3, 30)]
        [TestCase(0f, 0.0, 0, 0)]
        public void ScoreAdapter_LimitesDeLaTabla(float similarity, double precision, int stars, int xp)
        {
            var score = ScoreAdapter.FromSimilarity(similarity);
            Assert.AreEqual(precision, score.Precision, 0.0001);
            Assert.AreEqual(stars, score.Stars);
            Assert.AreEqual(xp, score.Xp);
            Assert.AreEqual(stars > 0, score.Passed);
        }

        [Test]
        public void ScoreAdapter_TruncaParaNoSubirDeUmbral()
        {
            // 87,996 redondeado daría 88,00 (3 estrellas) aunque la IA no llegó a 0,88: se trunca a 87,99 (2 estrellas).
            Assert.AreEqual(87.99, ScoreAdapter.ToApiPrecision(87.996), 0.0001);
            Assert.AreEqual(2, ScoreAdapter.StarsFor(ScoreAdapter.ToApiPrecision(87.996)));
            Assert.AreEqual(60.0, ScoreAdapter.ToApiPrecision(60.004), 0.0001);
        }

        [Test]
        public void ScoreAdapter_NuncaSaleDeCeroACien()
        {
            Assert.AreEqual(0.0, ScoreAdapter.ToApiPrecision(-5.0), 0.0001);
            Assert.AreEqual(100.0, ScoreAdapter.ToApiPrecision(130.0), 0.0001);
        }
    }
}
