using UnityEngine;
using UnityEngine.SceneManagement;
using Lumina.Api;
using Lumina.Core;
using Lumina.UI;

namespace Lumina.Interaction
{
    /// <summary>
    /// Carga una escena por nombre (puertas entre Lúmina y la casa de aprendizaje). Si la escena es una casa de aprendizaje
    /// cuyo nivel el backend marca como bloqueado (le falta XP), no se entra y se avisa.
    /// </summary>
    public class SceneEntrance : MonoBehaviour
    {
        [SerializeField] private string sceneName;

        public void Enter()
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError($"[SceneEntrance] El nombre de escena está vacío en '{name}'.", this);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneEntrance] La escena '{sceneName}' no está registrada en Build Settings.", this);
                return;
            }

            if (LevelAccess.IsBlocked(sceneName)) return;

            SceneManager.LoadScene(sceneName);
        }
    }
}
