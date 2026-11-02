using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lumina.Auth;
using Lumina.Core;
using Lumina.UI;
using UnityEngine;

namespace Lumina.Api
{
    /// <summary>
    /// Caché EN MEMORIA de lo que el backend dice del progreso del jugador (niveles, resumen y detalle por nivel). No guarda
    /// nada en disco: se vuelve a cargar al iniciar sesión y después de cada evaluación, y se borra al cerrar sesión.
    /// El estado de los niveles y de las señas (LOCKED / AVAILABLE / PASSED) sale de aquí, nunca de lógica local.
    /// </summary>
    public static class ProgressStore
    {
        private static readonly Dictionary<string, LevelDetailDto> Details = new Dictionary<string, LevelDetailDto>();

        public static LevelSummaryDto[] Levels { get; private set; }
        public static ProgressSummaryDto Summary { get; private set; }
        /// <summary>Precisión media 0–100 de los últimos 100 intentos aprobados; negativa si todavía no hay datos.</summary>
        public static float AveragePrecision { get; private set; } = -1f;

        public static bool IsLoaded => Levels != null && Summary != null;

        /// <summary>Se emite cada vez que cambia algo de la caché.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Bind()
        {
            Clear();
            AuthSession.SignedIn -= OnSignedIn; AuthSession.SignedIn += OnSignedIn;
            AuthSession.SignedOut -= Clear; AuthSession.SignedOut += Clear;
        }

        private static async void OnSignedIn(string userId)
        {
            try { await RefreshAsync(); }
            catch (ApiException e) { Toast.Show(ApiMessages.For(e)); }
        }

        /// <summary>Carga la lista de niveles y el resumen general (GET /levels y GET /progress).</summary>
        public static async Task RefreshAsync(CancellationToken ct = default)
        {
            var levelsTask = ApiClient.GetAsync<LevelListDto>("/levels", ct);
            var summaryTask = ApiClient.GetAsync<ProgressSummaryDto>("/progress", ct);
            await Task.WhenAll(levelsTask, summaryTask);
            Levels = levelsTask.Result.items ?? Array.Empty<LevelSummaryDto>();
            Summary = summaryTask.Result;
            Changed?.Invoke();
        }

        /// <summary>Detalle de un nivel con el estado de cada seña (GET /levels/{code}).</summary>
        public static async Task<LevelDetailDto> LoadLevelAsync(string levelCode, CancellationToken ct = default)
        {
            var detail = await ApiClient.GetAsync<LevelDetailDto>("/levels/" + ApiClient.Segment(levelCode), ct);
            Details[levelCode] = detail;
            Changed?.Invoke();
            return detail;
        }

        /// <summary>Después de una evaluación: vuelve a cargar el resumen, los niveles y el detalle del nivel afectado.</summary>
        public static async Task RefreshAfterEvaluationAsync(string levelCode, CancellationToken ct = default)
        {
            var tasks = new List<Task> { RefreshAsync(ct) };
            if (!string.IsNullOrEmpty(levelCode)) tasks.Add(LoadLevelAsync(levelCode, ct));
            await Task.WhenAll(tasks);
        }

        public static async Task LoadAveragePrecisionAsync(CancellationToken ct = default)
        {
            var history = await ApiClient.GetAsync<HistoryDto>("/evaluations?passed=true&limit=100", ct);
            var items = history?.items;
            if (items == null || items.Length == 0) { AveragePrecision = -1f; }
            else
            {
                float sum = 0f;
                foreach (var item in items) sum += item.precision;
                AveragePrecision = sum / items.Length;
            }
            Changed?.Invoke();
        }

        public static LevelDetailDto GetLevel(string levelCode) =>
            levelCode != null && Details.TryGetValue(levelCode, out var d) ? d : null;

        public static LevelSummaryDto FindLevel(string levelCode)
        {
            if (Levels == null) return null;
            foreach (var level in Levels) if (level.code == levelCode) return level;
            return null;
        }

        /// <summary>True/false según el backend; null si todavía no se conoce (sin sesión o sin datos cargados).</summary>
        public static bool? IsLevelUnlocked(string levelCode) => FindLevel(levelCode)?.unlocked;

        /// <summary>Estado de una seña (LOCKED / AVAILABLE / PASSED) o null si su nivel no está cargado.</summary>
        public static string StatusOf(string signCode)
        {
            var detail = GetLevel(SignCodes.LevelFor(signCode));
            if (detail?.signs == null) return null;
            var backendCode = SignCodes.BackendCodeFor(signCode);
            foreach (var sign in detail.signs)
                if (string.Equals(sign.code, backendCode, StringComparison.OrdinalIgnoreCase)) return sign.status;
            return null;
        }

        public static void Clear()
        {
            Levels = null;
            Summary = null;
            AveragePrecision = -1f;
            Details.Clear();
            Changed?.Invoke();
        }
    }

    /// <summary>Proveedor de «MI PROGRESO» con los datos del backend (caché de <see cref="ProgressStore"/>).</summary>
    public sealed class BackendProgressProvider : IPlayerProgressProvider
    {
        public PlayerProgress Load()
        {
            var progress = new PlayerProgress();
            var summary = ProgressStore.Summary;
            var levels = ProgressStore.Levels;
            if (summary == null || levels == null) return EmptyRows(progress);

            progress.xp = summary.totalXp;
            progress.stars = summary.totalStars;
            progress.signsLearned = summary.passedSigns;
            progress.averagePrecision = ProgressStore.AveragePrecision;
            progress.xpForNextLevel = summary.nextLevel != null && !string.IsNullOrEmpty(summary.nextLevel.code) ? summary.nextLevel.requiredXp : 0;

            string highest = summary.highestUnlockedLevel?.code;
            progress.level = 1;
            for (int i = 0; i < levels.Length; i++)
                if (levels[i].code == highest) progress.level = i + 1;
            progress.levelTitle = string.IsNullOrEmpty(highest) ? progress.levelTitle : SignCodes.SpanishLevelName(highest);

            var rows = new LearningLevelProgress[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                var level = levels[i];
                rows[i] = new LearningLevelProgress
                {
                    title = SignCodes.SpanishLevelName(level.code),
                    state = !level.unlocked ? LearningLevelState.Locked
                        : level.totalSigns > 0 && level.passedSigns >= level.totalSigns ? LearningLevelState.Completed
                        : LearningLevelState.InProgress
                };
            }
            progress.learningLevels = rows;
            return progress;
        }

        private static PlayerProgress EmptyRows(PlayerProgress progress)
        {
            progress.learningLevels = new[]
            {
                new LearningLevelProgress { title = SignCodes.SpanishLevelName(SignCodes.AlphabetLevel), state = LearningLevelState.InProgress },
                new LearningLevelProgress { title = SignCodes.SpanishLevelName(SignCodes.NumbersLevel), state = LearningLevelState.Locked },
                new LearningLevelProgress { title = SignCodes.SpanishLevelName(SignCodes.MonthsLevel), state = LearningLevelState.Locked },
                new LearningLevelProgress { title = SignCodes.SpanishLevelName(SignCodes.GreetingsLevel), state = LearningLevelState.Locked },
            };
            return progress;
        }
    }
}
