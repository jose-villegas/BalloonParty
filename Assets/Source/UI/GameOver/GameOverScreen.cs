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

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
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
            SetText(_finalLevelLabel, msg.FinalLevel);
            SetText(_finalScoreLabel, msg.FinalScore);
            SetText(_bestLevelLabel, _runMeta.BestLevel.Value);
            SetText(_bestScoreLabel, _runMeta.BestScore.Value);
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        private static void SetText(TMP_Text label, int value)
        {
            if (label != null)
            {
                label.text = value.ToString("N0");
            }
        }
    }
}
