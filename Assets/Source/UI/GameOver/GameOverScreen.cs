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
        private FormattedLabel _finalLevel;
        private FormattedLabel _finalScore;
        private FormattedLabel _bestLevel;
        private FormattedLabel _bestScore;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            // Wrap each label before its text is overwritten — FormattedLabel captures the authored
            // text (e.g. "Level: {0}") as a template so repeated losses keep substituting cleanly.
            _finalLevel = new FormattedLabel(_finalLevelLabel);
            _finalScore = new FormattedLabel(_finalScoreLabel);
            _bestLevel = new FormattedLabel(_bestLevelLabel);
            _bestScore = new FormattedLabel(_bestScoreLabel);

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
            _finalLevel.Set(msg.FinalLevel);
            _finalScore.Set(msg.FinalScore);
            _bestLevel.Set(_runMeta.BestLevel.Value);
            _bestScore.Set(_runMeta.BestScore.Value);
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }
}
