using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lumina.Interaction
{
    /// <summary>Carga una escena por nombre (puertas entre Lúmina y la casa de aprendizaje).</summary>
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

            SceneManager.LoadScene(sceneName);
        }
    }
}
