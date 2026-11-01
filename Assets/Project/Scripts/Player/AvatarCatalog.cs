using System;
using UnityEngine;

namespace Lumina.Player
{
    /// <summary>
    /// Lista de avatares disponibles (id → modelo). El id es el mismo que usa el selector del registro y
    /// <c>AuthSession.AvatarId</c>, de modo que la elección llega al mundo sin más datos.
    /// </summary>
    [CreateAssetMenu(menuName = "Lumina/Avatar Catalog", fileName = "AvatarCatalog")]
    public class AvatarCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string id;
            public string displayName;
            public GameObject prefab;
        }

        [SerializeField] private Entry[] avatars = Array.Empty<Entry>();

        public int Count => avatars != null ? avatars.Length : 0;

        /// <summary>Primer avatar con modelo, para pantallas que necesitan mostrar uno aunque aún no se haya elegido.</summary>
        public Entry First()
        {
            if (avatars == null) return null;
            foreach (var entry in avatars)
                if (entry != null && entry.prefab != null) return entry;
            return null;
        }

        /// <summary>Devuelve la entrada con ese id (sin distinguir mayúsculas) o null si no existe o no tiene modelo.</summary>
        public Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id) || avatars == null) return null;
            foreach (var entry in avatars)
                if (entry != null && entry.prefab != null && string.Equals(entry.id, id, StringComparison.OrdinalIgnoreCase))
                    return entry;
            return null;
        }
    }
}
