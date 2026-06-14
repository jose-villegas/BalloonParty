using BalloonParty.Game.Run;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using MessagePipe;
using TMPro;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.GameOver
{
    /// <summary>
    ///     Placeholder loss screen. Appears on <see cref="GameOverMessage"/>, shows the final run
    ///     stats alongside the persisted best, and restarts the run on the button press. Visibility
    ///     is driven by a <see cref="CanvasGroup"/> — the GameObject stays active so its
    ///     subscriptions live for the whole session (never toggle it with SetActive). Wire the
    ///     Restart button's onClick to <see cref="OnRestartPressed"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class GameOverScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text _finalLevelLabel;
        [SerializeField] private TMP_Text _finalScoreLabel;
        [SerializeField] private TMP_Text _bestLevelLabel;
        [SerializeField] private TMP_Text _bestScoreLabel;

        [Inject] private ISubscriber<GameOverMessage> _gameOverSubscriber;
        [Inject] private IRunMeta _runMeta;
        [Inject] private RunController _runController;

        private CanvasGroup _canvasGroup;
        private string _finalLevelFormat;
        private string _finalScoreFormat;
        private string _bestLevelFormat;
        private string _bestScoreFormat;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            // Capture each label's authored text (e.g. "Level: {0}") as a format template before we
            // overwrite it, so repeated losses keep substituting into the original placeholder.
            _finalLevelFormat = LabelFormat(_finalLevelLabel);
            _finalScoreFormat = LabelFormat(_finalScoreLabel);
            _bestLevelFormat = LabelFormat(_bestLevelLabel);
            _bestScoreFormat = LabelFormat(_bestScoreLabel);

            SetVisible(false);
        }

        private void Start()
        {
            _gameOverSubscriber.Subscribe(OnGameOver).AddTo(this);

            // Hide whenever we leave GameOver — e.g. when Restart transitions back to Game.
            Navigation.Current
                .Where(state => state != NavigationState.GameOver)
                .Subscribe(_ => SetVisible(false))
                .AddTo(this);
        }

        // Wired to the Restart button's onClick in the inspector.
        public void OnRestartPressed()
        {
            _runController.RestartRun();
        }

        private void OnGameOver(GameOverMessage msg)
        {
            SetValue(_finalLevelLabel, _finalLevelFormat, msg.FinalLevel);
            SetValue(_finalScoreLabel, _finalScoreFormat, msg.FinalScore);
            SetValue(_bestLevelLabel, _bestLevelFormat, _runMeta.BestLevel.Value);
            SetValue(_bestScoreLabel, _bestScoreFormat, _runMeta.BestScore.Value);
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        private static string LabelFormat(TMP_Text label)
        {
            // Fall back to a bare placeholder when the label has no authored text.
            return label != null && !string.IsNullOrEmpty(label.text) ? label.text : "{0}";
        }

        private static void SetValue(TMP_Text label, string format, int value)
        {
            if (label != null)
            {
                label.text = string.Format(format, value);
            }
        }
    }
}
