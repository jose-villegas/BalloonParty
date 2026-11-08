using BalloonParty.Shared.Messages;
using BalloonParty.Thrower;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.HeadsUp
{
    /// <summary>
    ///     Writes a small "+1" by the cannon each time its next ball is ready (<see cref="ThrowerReadyMessage" />),
    ///     and clears it once that ball is fired. Kept upright beside the muzzle rather than parented to the
    ///     cannon, which turns with the aim.
    /// </summary>
    public class ReloadHeadsUp : MonoBehaviour
    {
        [SerializeField] private HandwrittenHeadsUp _headsUp;
        [SerializeField] private string _text = "+1";

        [Tooltip("World units from the muzzle to the heads-up's centre.")]
        [SerializeField] private Vector2 _worldOffset = new(0.3f, -0.2f);

        [Inject] private ISubscriber<ThrowerReadyMessage> _readySubscriber;
        [Inject] private ISubscriber<ProjectileFiredMessage> _firedSubscriber;
        [Inject] private ThrowerOriginProvider _origin;

        private Canvas _canvas;

        private void Start()
        {
            _canvas = _headsUp.GetComponentInParent<Canvas>();
            _readySubscriber.Subscribe(_ => OnReady()).AddTo(this);
            _firedSubscriber.Subscribe(_ => _headsUp.Clear()).AddTo(this);
        }

        private void OnReady()
        {
            if (!_origin.IsAvailable)
            {
                return;
            }

            var gameCamera = Camera.main;
            if (gameCamera == null || _canvas == null)
            {
                return;
            }

            var parent = (RectTransform)_headsUp.transform.parent;
            var world = _origin.Origin + _worldOffset;
            _headsUp.Show(_text, RectAnchorMath.GameWorldToLocalPoint(parent, _canvas, gameCamera, world));
        }
    }
}
