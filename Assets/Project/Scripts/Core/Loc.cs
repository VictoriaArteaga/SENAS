using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Lumina.Core
{
    /// <summary>
    /// Localización centralizada (español/inglés). Todas las cadenas viven en <see cref="LocTable"/> con un id único.
    /// Respaldo: si falta la versión inglesa se muestra el español y se registra una vez para revisarlo.
    /// </summary>
    public static class Loc
    {
        private struct Entry { public string key, es, en; public Regex pattern; }

        private static Dictionary<string, Entry> _byKey;
        private static Dictionary<string, string> _keyBySpanish;
        private static List<Entry> _patterns;
        private static readonly HashSet<string> Reported = new HashSet<string>();

        public static GameLanguage Current => GameSettings.Language;

        /// <summary>Ids sin traducción detectados en esta ejecución (para revisión y pruebas).</summary>
        public static IReadOnlyCollection<string> MissingReported => Reported;

        private static void Build()
        {
            if (_byKey != null) return;
            _byKey = new Dictionary<string, Entry>();
            _keyBySpanish = new Dictionary<string, string>();
            _patterns = new List<Entry>();
            foreach (var (key, es, en) in LocTable.Rows)
            {
                var entry = new Entry { key = key, es = es, en = en };
                if (_byKey.ContainsKey(key)) { Debug.LogError($"[Loc] Id duplicado: {key}"); continue; }
                if (es.Contains("{0}"))
                {
                    entry.pattern = new Regex("^" + Regex.Escape(es).Replace(@"\{0}", "(.+?)").Replace(@"\{1}", "(.+?)") + "$", RegexOptions.Singleline);
                    _patterns.Add(entry);
                }
                _byKey[key] = entry;
                if (!_keyBySpanish.ContainsKey(es)) _keyBySpanish[es] = key;
            }
        }

        /// <summary>True si la versión inglesa es más larga que la española (para dejar sitio al texto en rótulos de tamaño fijo).</summary>
        public static bool LongerInEnglish(string key)
        {
            Build();
            return _byKey.TryGetValue(key, out var e) && !string.IsNullOrEmpty(e.en) && e.en.Length > e.es.Length * 1.05f;
        }

        public static bool Has(string key) { Build(); return _byKey.ContainsKey(key); }

        /// <summary>Texto del id en el idioma actual. Nunca devuelve null.</summary>
        public static string T(string key)
        {
            Build();
            if (!_byKey.TryGetValue(key, out var e)) { Report(key, "id inexistente"); return key; }
            if (Current == GameLanguage.Spanish) return e.es;
            if (string.IsNullOrEmpty(e.en)) { Report(key, "falta la traducción al inglés"); return e.es; }
            return e.en;
        }

        public static string T(string key, params object[] args)
        {
            try { return string.Format(T(key), args); }
            catch (FormatException) { Report(key, "formato inválido"); return T(key); }
        }

        /// <summary>Id cuyo texto en español es exactamente <paramref name="spanish"/> (para enlazar textos ya escritos en las escenas).</summary>
        public static bool TryKeyForSpanish(string spanish, out string key)
        {
            Build();
            key = null;
            return !string.IsNullOrEmpty(spanish) && _keyBySpanish.TryGetValue(spanish, out key);
        }

        /// <summary>
        /// Traduce un texto que llega ya en español (mensajes de Auth, datos del progreso). Si no hay equivalente lo devuelve
        /// tal cual: puede ser un nombre de jugador u otro dato del usuario, así que no se registra como faltante.
        /// </summary>
        public static string Translate(string spanish)
        {
            if (string.IsNullOrEmpty(spanish) || Current == GameLanguage.Spanish) return spanish;
            Build();
            if (_keyBySpanish.TryGetValue(spanish, out var key)) return T(key);
            foreach (var p in _patterns)
            {
                var m = p.pattern.Match(spanish);
                if (!m.Success) continue;
                var args = new object[m.Groups.Count - 1];
                for (int i = 1; i < m.Groups.Count; i++) args[i - 1] = m.Groups[i].Value;
                return T(p.key, args);
            }
            return spanish;
        }

        private static void Report(string key, string reason)
        {
            if (Reported.Add(key)) Debug.LogWarning($"[Loc] '{key}': {reason}. Se muestra el español.");
        }

        /// <summary>Verifica que todas las entradas tengan inglés; devuelve los ids incompletos.</summary>
        public static List<string> FindIncomplete()
        {
            Build();
            var list = new List<string>();
            foreach (var e in _byKey.Values) if (string.IsNullOrWhiteSpace(e.en)) list.Add(e.key);
            return list;
        }
    }
}
