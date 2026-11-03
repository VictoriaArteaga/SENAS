using UnityEngine;
using UnityEngine.SceneManagement;
using Lumina.Player;
using Lumina.UI;

namespace Lumina.Interaction
{
    /// <summary>
    /// Ida y vuelta entre Lúmina y las casas de aprendizaje. Al entrar por una puerta recuerda dónde estaba el jugador y hacia dónde
    /// miraba, y al volver a Lúmina lo deja ahí (fuera de esa puerta, de espaldas a ella) en vez de en el punto de inicio del mundo.
    /// Además pone en cada casa un <see cref="HouseExitGuard"/>, para que quien cruce el umbral o caiga fuera de la casa regrese a Lúmina.
    /// No necesita nada en las escenas: se engancha solo al cargarse.
    /// </summary>
    public static class WorldReturn
    {
        private static readonly string[] Houses = { SceneNames.Alphabet, SceneNames.Numbers, SceneNames.Months, SceneNames.Greetings };

        private static bool _has;
        private static Vector3 _position;
        private static float _yaw;

        public static bool IsHouse(string sceneName) => System.Array.IndexOf(Houses, sceneName) >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            _has = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>Guarda dónde está el jugador junto a una puerta de Lúmina, mirando hacia afuera de ella.</summary>
        public static void Remember(Transform door)
        {
            var player = Object.FindFirstObjectByType<FirstPersonController>();
            if (player == null) return;

            Vector3 away = player.transform.position - door.position;
            away.y = 0f;
            _yaw = away.sqrMagnitude > 0.09f
                ? Quaternion.LookRotation(away).eulerAngles.y
                : player.transform.eulerAngles.y + 180f;
            _position = player.transform.position;
            _has = true;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SceneNames.World)
            {
                if (_has) Place();
                _has = false;
            }
            else if (IsHouse(scene.name))
            {
                new GameObject("HouseExitGuard").AddComponent<HouseExitGuard>();
            }
            else if (scene.name == SceneNames.MainMenu || scene.name == SceneNames.Login || scene.name == SceneNames.Register)
            {
                _has = false; // sesión nueva: la próxima vez se empieza en el punto de inicio
            }
        }

        private static void Place()
        {
            var player = Object.FindFirstObjectByType<FirstPersonController>();
            if (player == null) return;

            // El CharacterController pisa la posición del transform si está activo: se apaga para moverlo.
            var body = player.GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            player.transform.SetPositionAndRotation(_position, Quaternion.Euler(0f, _yaw, 0f));
            if (body != null) body.enabled = true;
        }
    }
}
