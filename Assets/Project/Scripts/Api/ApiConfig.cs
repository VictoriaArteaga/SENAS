using UnityEngine;

namespace Lumina.Api
{
    /// <summary>
    /// Configuración de la conexión con el backend. El asset vive en <c>Assets/Project/Data/Resources/ApiConfig.asset</c>
    /// (dentro de Resources para que también exista en las compilaciones sin tener que referenciarlo desde una escena).
    /// Si el asset no existe se usan los valores por defecto de abajo.
    /// </summary>
    [CreateAssetMenu(menuName = "Lumina/API Config", fileName = "ApiConfig")]
    public class ApiConfig : ScriptableObject
    {
        public const string ResourceName = "ApiConfig";

        [Header("URL base (incluye el prefijo /api/v1, sin barra final)")]
        [Tooltip("Se usa en el Editor y en las compilaciones de desarrollo. HTTP está permitido solo ahí.")]
        [SerializeField] private string developmentBaseUrl = "http://localhost:3000/api/v1";
        [Tooltip("Se usa en las compilaciones de release. Debe ser https (la URL de Render). Vacía por ahora.")]
        [SerializeField] private string productionBaseUrl = "";

        [Header("Red")]
        [Tooltip("Tiempo máximo por petición. 15 s porque el servicio gratuito de Render puede tardar en despertar.")]
        [SerializeField, Min(1)] private int timeoutSeconds = 15;

        [Header("Desarrollo")]
        [Tooltip("Vuelve a la autenticación SIMULADA (sin backend). Solo para desarrollo de interfaz; el progreso y las prácticas necesitan el backend.")]
        [SerializeField] private bool useMockAuthentication;

        /// <summary>URL base según el tipo de compilación (desarrollo o release), sin barra final.</summary>
        public string BaseUrl
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return Normalize(developmentBaseUrl);
#else
                return Normalize(productionBaseUrl);
#endif
            }
        }

        /// <summary>URL de producción (https, la de Render). Vacía mientras no se despliegue.</summary>
        public string ProductionBaseUrl => Normalize(productionBaseUrl);

        public int TimeoutSeconds => Mathf.Max(1, timeoutSeconds);
        public bool UseMockAuthentication => useMockAuthentication;

        private static string Normalize(string url) => string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim().TrimEnd('/');

        private static ApiConfig _current;

        /// <summary>Configuración activa: el asset de Resources o, si falta, una instancia con los valores por defecto.</summary>
        public static ApiConfig Current
        {
            get
            {
                if (_current != null) return _current;
                _current = Resources.Load<ApiConfig>(ResourceName);
                if (_current == null)
                {
                    Debug.LogWarning($"[Api] No se encontró Resources/{ResourceName}.asset; se usan los valores por defecto.");
                    _current = CreateInstance<ApiConfig>();
                    _current.hideFlags = HideFlags.DontSave;
                }
                return _current;
            }
        }

        /// <summary>Solo para pruebas: sustituye la configuración activa.</summary>
        public static void OverrideForTests(ApiConfig config) => _current = config;
    }
}
