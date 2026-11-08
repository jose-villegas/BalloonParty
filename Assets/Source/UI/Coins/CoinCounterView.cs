using BalloonParty.Game.Coins;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.UI.Score;
using DG.Tweening;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.Coins
{
    /// <summary>
    ///     The coin counter, bottom left: the coin icon and the balance. Fades in when
    ///     <see cref="ICoinCounterVisibility" /> first says so, gives <see cref="CoinTrailService" /> the icon
    ///     to fly coins to, and punches the icon as each one lands.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    internal sealed class CoinCounterView : MonoBehaviour
    {
        [SerializeField] private FlyingTrail _trailPrefab;
        [SerializeField] private RectTransform _icon;
        [SerializeField] [Min(0f)] private float _fadeInDuration = 0.4f;
        [SerializeField] private float _punchScale = 0.3f;
        [SerializeField] [Min(0.01f)] private float _punchDuration = 0.25f;

        [Inject] private ICoinCounterVisibility _visibility;
        [Inject] private CoinTrailService _trails;
        [Inject] private ISubscriber<CoinLandedMessage> _landedSubscriber;

        private readonly CompositeDisposable _subscriptions = new();

        private CanvasGroup _group;
        private TransformTrailEndpoint _target;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            _target = new TransformTrailEndpoint(_icon);
            _trails.RegisterTarget(_target, _trailPrefab);

            _group.alpha = _visibility.Visible.Value ? 1f : 0f;
            _visibility.Visible.Skip(1).Where(visible => visible).Subscribe(_ => FadeIn()).AddTo(_subscriptions);
            _landedSubscriber.Subscribe(_ => Punch()).AddTo(_subscriptions);
        }

        private void OnDestroy()
        {
            _trails?.UnregisterTarget(_target);
            _subscriptions.Dispose();
            _group.DOKill();
            _icon.DOKill();
        }

        private void FadeIn()
        {
            _group.DOKill();
            _group.DOFade(1f, _fadeInDuration).SetUpdate(true);
        }

        // Completes the last punch first, so a stream of landings restarts from the rest scale instead of growing.
        // Real time, as the coins fly: they land while the level-up popup has stopped the clock.
        private void Punch()
        {
            _icon.DOKill(true);
            _icon.DOPunchScale(Vector3.one * _punchScale, _punchDuration, 6, 0.5f).SetUpdate(true);
        }
    }
}
