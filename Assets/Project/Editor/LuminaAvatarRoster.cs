using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lumina.EditorTools
{
    /// <summary>
    /// Única definición, para los builders del editor, de QUÉ avatares existen y CUÁLES muestra el selector del registro.
    ///
    ///  · Catálogo (AvatarCatalog.asset): los cuatro avatares modulares antiguos (luna, nube, rio, sol; se siguen resolviendo para los perfiles
    ///    que ya los tienen) + los seis de AVATARS2. Son 10 entradas, en este orden.
    ///  · Selector del registro: solo los seis de AVATARS2 (carol, luna-crimson, tom, crimson, neon, tensy). Los cuatro antiguos NO son opciones.
    ///
    /// Los ids/nombres de los cuatro antiguos NO se repiten aquí: vienen de LuminaAuthSceneBuilder, que es quien los construye.
    /// Los builders solo AÑADEN entradas que falten al catálogo; nunca lo reducen ni lo reordenan (ver LuminaWorldAuthIntegration.MergeCatalog).
    /// </summary>
    internal static class LuminaAvatarRoster
    {
        internal const string LegacyPrefabDir = "Assets/Project/Prefabs/Avatars/";
        internal const string ApprovedPrefabDir = "Assets/Project/Prefabs/PlayerAvatars/";

        internal readonly struct Entry
        {
            public readonly string Id, DisplayName, PrefabPath;
            public Entry(string id, string displayName, string prefabPath) { Id = id; DisplayName = displayName; PrefabPath = prefabPath; }
            public GameObject LoadPrefab() => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        // Avatares de AVATARS2 aprobados: lo que ve el jugador en el selector, en este orden. El prefab es Avatar_{id}.prefab (el mismo que usa PlayerAvatar).
        private static readonly (string id, string name)[] Approved =
        {
            ("carol", "Isa"), ("luna-crimson", "Mavi"), ("tom", "Tom"), ("crimson", "Crimson"), ("neon", "Neon"), ("tensy", "Tensy"),
        };

        /// <summary>Cuatro avatares modulares antiguos: válidos en el catálogo, no seleccionables.</summary>
        internal static IReadOnlyList<Entry> Legacy =>
            LuminaAuthSceneBuilder.LegacyAvatars().Select(a => new Entry(a.id, a.name, $"{LegacyPrefabDir}Avatar_{a.id}.prefab")).ToList();

        /// <summary>Seis avatares aprobados: las opciones visibles del selector.</summary>
        internal static IReadOnlyList<Entry> Selectable =>
            Approved.Select(a => new Entry(a.id, a.name, $"{ApprovedPrefabDir}Avatar_{a.id}.prefab")).ToList();

        /// <summary>Las 10 entradas del catálogo, en orden (antiguos primero).</summary>
        internal static IReadOnlyList<Entry> Catalog => Legacy.Concat(Selectable).ToList();

        /// <summary>Lanza una excepción clara si falta algún prefab que los builders necesitan.</summary>
        internal static void RequirePrefabs()
        {
            var missing = Catalog.Where(e => e.LoadPrefab() == null).Select(e => e.PrefabPath).ToList();
            if (missing.Count > 0)
                throw new System.InvalidOperationException("Faltan prefabs de avatar: " + string.Join(", ", missing) + ". Los antiguos los genera 'Construir todo'; los de AVATARS2 ya deben existir.");
        }
    }
}
