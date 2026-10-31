using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Lumina.Auth;
using Lumina.Core;
using Lumina.Player;

namespace Lumina.UI
{
    /// <summary>
    /// «MI PROGRESO»: pinta el avatar elegido y los datos de <see cref="PlayerProgressService"/>. No calcula nada propio;
    /// cuando el progreso venga de Firebase solo cambia el proveedor.
    /// </summary>
    public class ProfileController : MonoBehaviour
    {
        [SerializeField] private SceneHeaderUI header;

        [Header("Avatar")]
        [SerializeField] private AvatarCatalog avatarCatalog;
        [SerializeField] private Transform avatarAnchor;
        [SerializeField] private TMP_Text playerNameLabel;

        [Header("Nivel")]
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text levelTitleLabel;
        [SerializeField] private ProgressBarUI xpBar;

        [Header("Estadísticas")]
        [SerializeField] private StatCardUI signsCard;
        [SerializeField] private StatCardUI precisionCard;
        [SerializeField] private StatCardUI starsCard;
        [SerializeField] private TMP_Text emptyHint;

        [Header("Niveles de aprendizaje")]
        [SerializeField] private RectTransform levelsContainer;
        [SerializeField] private RectTransform levelRowTemplate;

        private PlayerProgress _progress;

        private static readonly Color Turquoise = new Color(0.18f, 0.769f, 0.714f);
        private static readonly Color Coral = new Color(1f, 0.369f, 0.302f);
        private static readonly Color Sand = new Color(0.851f, 0.745f, 0.549f);

        private void OnEnable() => GameSettings.LanguageChanged += Rebind;
        private void OnDisable() => GameSettings.LanguageChanged -= Rebind;
        private void Rebind() { if (_progress != null) { Bind(_progress); ShowName(); } }

        private void Start()
        {
            header.SetTitle("title.progress");
            ShowAvatar();
            _progress = PlayerProgressService.Load();
            Bind(_progress);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(header.gameObject.GetComponentInChildren<Button>().gameObject);
        }

        private void ShowAvatar()
        {
            ShowName();
            // Sin avatar elegido en esta ejecución se muestra el primero del catálogo, para no dejar el escenario vacío.
            var entry = avatarCatalog != null ? (avatarCatalog.Find(AuthSession.AvatarId) ?? avatarCatalog.First()) : null;
            if (entry == null) return;

            var body = Instantiate(entry.prefab, avatarAnchor);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            foreach (var collider in body.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }

        private void ShowName()
        {
            playerNameLabel.text = string.IsNullOrWhiteSpace(AuthSession.DisplayName) ? Loc.T("progress.player") : AuthSession.DisplayName.ToUpperInvariant();
        }

        private void Bind(PlayerProgress progress)
        {
            levelLabel.text = Loc.T("progress.level", progress.level);
            levelTitleLabel.text = Loc.Translate(progress.levelTitle);
            xpBar.Set(progress.XpFraction,
                progress.xpForNextLevel > 0 ? Loc.T("progress.xpOf", progress.xp.ToString("N0"), progress.xpForNextLevel.ToString("N0")) : Loc.T("progress.xp", progress.xp.ToString("N0")));

            signsCard.Set(progress.signsLearned.ToString(), Loc.T("progress.signs"));
            precisionCard.Set(progress.averagePrecision >= 0f ? Mathf.RoundToInt(progress.averagePrecision) + "%" : "—", Loc.T("progress.precision"));
            starsCard.Set(progress.stars.ToString(), Loc.T("progress.stars"));

            emptyHint.gameObject.SetActive(progress.signsLearned == 0 && progress.averagePrecision < 0f);

            levelRowTemplate.gameObject.SetActive(false);
            for (int i = levelsContainer.childCount - 1; i >= 0; i--)
                if (levelsContainer.GetChild(i) != levelRowTemplate) Destroy(levelsContainer.GetChild(i).gameObject);

            if (progress.learningLevels == null) return;
            for (int i = 0; i < progress.learningLevels.Length; i++)
                BuildRow(i + 1, progress.learningLevels[i]);
        }

        /// <summary>Cada estado lleva palabra, número/forma y color: nunca se distingue solo por el color.</summary>
        private void BuildRow(int number, LearningLevelProgress level)
        {
            var row = Instantiate(levelRowTemplate, levelsContainer);
            row.gameObject.SetActive(true);
            row.Find("Title").GetComponent<TMP_Text>().text = Loc.Translate(level.title).ToUpperInvariant();

            var badge = row.Find("Badge").GetComponent<Image>();
            var badgeLabel = badge.transform.Find("Label").GetComponent<TMP_Text>();
            var statusPill = row.Find("StatusPill").GetComponent<Image>();
            var status = statusPill.transform.Find("Status").GetComponent<TMP_Text>();

            switch (level.state)
            {
                case LearningLevelState.Completed:
                    ThemedGraphic.Set(badge, Turquoise); ThemedGraphic.Set(statusPill, Turquoise);
                    badgeLabel.text = "OK";
                    status.text = Loc.T("progress.completed");
                    break;
                case LearningLevelState.InProgress:
                    ThemedGraphic.Set(badge, Coral); ThemedGraphic.Set(statusPill, Coral);
                    badgeLabel.text = number.ToString();
                    status.text = Loc.T("progress.inProgress");
                    break;
                default:
                    ThemedGraphic.Set(badge, Sand); ThemedGraphic.Set(statusPill, Sand);
                    badgeLabel.text = "—";
                    status.text = Loc.T("progress.locked");
                    row.GetComponent<CanvasGroup>().alpha = 0.7f;
                    break;
            }
        }
    }
}
