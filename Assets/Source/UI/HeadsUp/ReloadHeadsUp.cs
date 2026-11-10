using BalloonParty.Configuration.HeadsUp;
using BalloonParty.Game.Level;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Thrower;
using MessagePipe;
using TMPro;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.HeadsUp
{
    /// <summary>
    ///     Writes "ready" beside the cannon each time its next ball is ready (<see cref="ThrowerReadyMessage" />)
    ///     and keeps it there until that ball is fired, or play stops (a level-up, the game-over screen), which
    ///     stops the cannon being ready without a shot. Placed off the cannon's pivot, which stays put, rather
    ///     than its muzzle or a child of it, which turn with the aim: the word is always in the same spot, upright.
    /// </summary>
    public class ReloadHeadsUp : MonoBehaviour
    {
        [SerializeField] private HandwrittenHeadsUp _headsUp;
        [SerializeField] private string _text = "ready";

        [Tooltip("The lowest emphasis of the heads-ups: it shows before nearly every shot.")]
        [SerializeField] private HeadsUpStyle _style =
            new(new Color(0.29f, 0.29f, 0.32f), 0.85f, 0f, 0.85f, FontStyles.Normal);

        [Tooltip("World units from the cannon's pivot to the heads-up's centre: beside the cannon, clear of it.")]
        [SerializeField] private Vector2 _worldOffset = new(0.75f, 0f);

        [Inject] private ISubscriber<ThrowerReadyMessage> _readySubscriber;
        [Inject] private ISubscriber<ProjectileFiredMessage> _firedSubscriber;
        [Inject] private ThrowerOriginProvider _origin;
        [Inject] private ILevelProgress _progress;

        private void Start()
        {
            _readySubscriber.Subscribe(_ => OnReady()).AddTo(this);
            _firedSubscriber.Subscribe(_ => _headsUp.Clear()).AddTo(this);
            Navigation.Current.Where(state => state != NavigationState.Game)
                .Subscribe(_ => _headsUp.Clear()).AddTo(this);
            _progress.Phase.Where(phase => phase != LevelUpPhase.Playing)
                .Subscribe(_ => _headsUp.Clear()).AddTo(this);
        }

        private void OnReady()
        {
            if (!_origin.IsAvailable)
            {
                return;
            }

            _headsUp.ShowUntilCleared(_text, _origin.Pivot + _worldOffset, _style);
        }
    }
}
