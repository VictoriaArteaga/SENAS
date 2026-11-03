using System;
using UnityEditor;
using UnityEngine;

namespace Lumina.EditorTools
{
    public static class Avatars2PlayTest
    {
        /// <summary>-executeMethod Lumina.EditorTools.Avatars2PlayTest.Run -avatar carol -prefab Assets/.../Avatar_carol.prefab -tag final -out {carpeta}</summary>
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Project/Scenes/LuminaWorld.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var go = new GameObject("TEMP_Avatars2PlayDriver");
            var d = go.AddComponent<Avatars2PlayDriver>();
            d.avatar = Arg("-avatar"); d.prefabPath = Arg("-prefab"); d.tag = Arg("-tag"); d.outDir = Arg("-out"); d.realCatalog = Arg("-real") == "1";
            EditorApplication.EnterPlaymode();
        }

        /// <summary>-executeMethod Lumina.EditorTools.Avatars2PlayTest.RunSelector -out {carpeta}: prueba en Play Mode del selector real de RegisterScene.</summary>
        public static void RunSelector()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Project/Scenes/RegisterScene.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var go = new GameObject("TEMP_Avatars2SelectorPlayDriver");
            go.AddComponent<Avatars2SelectorPlayDriver>().outDir = Arg("-out");
            EditorApplication.EnterPlaymode();
        }

        private static string Arg(string key)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
            return null;
        }
    }
}
