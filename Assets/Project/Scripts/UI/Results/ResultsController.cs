using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Lumina.Core;

namespace Lumina.UI
{
    /// <summary>Resumen al terminar una práctica. Muestra solo lo que haya en <see cref="PracticeSession"/>; sin evaluación no inventa nota.</summary>
    public class ResultsController : MonoBehaviour
    {
        [SerializeField] private SceneHeaderUI header;
        [SerializeField] private TMP_Text signLabel;
        [SerializeField] private StatCardUI precisionCard;
        [SerializeField] private StatCardUI starsCard;
        [SerializeField] private StatCardUI xpCard;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button practiceAgainButton;

        private void Start()
        {
            header.SetTitle("title.results");
            practiceAgainButton.onClick.AddListener(() => ScreenFader.Load(SceneNames.Practice));

            var sign = PracticeSession.Sign;
            signLabel.text = sign != null ? sign.displayName.ToUpperInvariant() : Loc.T("title.practice");

            var result = PracticeSession.Result;
            if (result.HasValue && result.Value.evaluated)
            {
                var r = result.Value;
                precisionCard.Set(Mathf.RoundToInt(r.precision) + "%", Loc.T("results.precision"));
                starsCard.gameObject.SetActive(r.stars >= 0);
                xpCard.gameObject.SetActive(r.xp >= 0);
                if (r.stars >= 0) starsCard.Set(r.stars.ToString(), Loc.T("progress.stars"));
                if (r.xp >= 0) xpCard.Set("+" + r.xp, Loc.T("results.xp"));
                messageLabel.text = string.IsNullOrWhiteSpace(r.feedback) ? Loc.T("results.good") : r.feedback;
            }
            else
            {
                // TODO(evaluación): mostrar la precisión real cuando el reconocimiento entregue un SignAttemptResult.
                precisionCard.Set("—", Loc.T("results.notEvaluated"));
                starsCard.gameObject.SetActive(false);
                xpCard.gameObject.SetActive(false);
                messageLabel.text = Loc.T("results.none");
            }

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(practiceAgainButton.gameObject);
        }
    }
}
