using UnityEngine;
using UnityEngine.InputSystem;
using Lumina.Core;

namespace Lumina.Prototype612
{
    /// <summary>
    /// Ayuda para la prueba MANUAL del prototipo 6.2.1. No sustituye al jugador ni a su controlador: ofrece atajos de teletransporte y un
    /// conmutador día/noche para llegar rápido a cada punto.
    ///   0 spawn · 1 Abecedario · 2 Números · 3 Meses · 4 Saludos · 5 puente norte · 6 centro-norte · 7 centro-sur · 8 puente sur · N día/noche · H ayuda
    /// El conmutador usa GameSettings.DarkMode (el mismo del juego), así que ThemeSkybox, ThemeAmbient y ThemeLights reaccionan igual.
    /// </summary>
    public class R621TestHelper : MonoBehaviour
    {
        [System.Serializable] public class Stop { public string label; public Vector3 position; public float yaw; }
        public Stop[] stops;                         // índice 0 = tecla 0, 1 = tecla 1, ...
        [SerializeField] bool showHelp = true;
        CharacterController _cc;

        void Start() { _cc = GetComponent<CharacterController>(); }

        void Update()
        {
            var kb = Keyboard.current; if (kb == null || _cc == null || stops == null) return;
            Key[] keys = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8 };
            for (int i = 0; i < keys.Length && i < stops.Length; i++) if (kb[keys[i]].wasPressedThisFrame) Teleport(stops[i]);
            if (kb.nKey.wasPressedThisFrame) GameSettings.DarkMode = !GameSettings.DarkMode;
            if (kb.hKey.wasPressedThisFrame) showHelp = !showHelp;
        }

        void Teleport(Stop s) { _cc.enabled = false; transform.position = s.position; transform.rotation = Quaternion.Euler(0f, s.yaw, 0f); _cc.enabled = true; }

        void OnGUI()
        {
            if (!showHelp) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            string text = "PRUEBA MANUAL · prototipo 6.2.1 (cuatro aldeas)   (H oculta)\nWASD mover · ratón mirar · Shift correr · Esc suelta el cursor\n" +
                          "0 spawn · 1 Abecedario · 2 Números · 3 Meses · 4 Saludos · 5 puente norte · 6 centro-norte · 7 centro-sur · 8 puente sur · N día/noche";
            GUI.Box(new Rect(8, 8, 860, 62), GUIContent.none);
            GUI.Label(new Rect(14, 10, 850, 60), text, style);
        }
    }
}
