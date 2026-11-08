using System.Globalization;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.HeadsUp
{
    /// <summary>
    ///     Writes how a ball's bounces change where it happens: "+n bounce" where it picks up a shield, n
    ///     counting the shields it has picked up this flight, and "-1 bounce" where a wall spends one.
    /// </summary>
    public class BounceHeadsUp : MonoBehaviour
    {
        [Tooltip("Lines written in turn, so bounces close together each get their own instead of replacing one.")]
        [SerializeField] private HandwrittenHeadsUp[] _lines;

        [Tooltip("{0} is how many shields the ball has picked up this flight.")]
        [SerializeField] private string _pickupFormat = "+{0} bounce";
        [SerializeField] private string _wallText = "-1 bounce";

        [Tooltip("World units from where it happened to the line's centre.")]
        [SerializeField] private Vector2 _worldOffset = new(0f, 0.25f);

        [Inject] private ISubscriber<ShieldGainedMessage> _shieldGainedSubscriber;
        [Inject] private ISubscriber<WallHitMessage> _wallHitSubscriber;
        [Inject] private ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        [Inject] private SlotGrid _grid;

        private readonly FlightPickupCount _pickups = new();
        private Canvas _canvas;
        private int _nextLine;

        private void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
            _shieldGainedSubscriber.Subscribe(OnShieldGained).AddTo(this);
            _wallHitSubscriber.Subscribe(msg => Write(_wallText, msg.Position)).AddTo(this);
            _destroyedSubscriber.Subscribe(msg => _pickups.Forget(msg.Projectile)).AddTo(this);
        }

        private void OnDestroy()
        {
            _pickups.Clear();
        }

        private void OnShieldGained(ShieldGainedMessage msg)
        {
            // A shield item that pops with no ball to give it to grants nothing, so there's nothing to write.
            if (msg.Projectile == null)
            {
                return;
            }

            var count = _pickups.Record(msg.Projectile);
            var text = string.Format(CultureInfo.InvariantCulture, _pickupFormat, count);
            Write(text, _grid.IndexToWorldPosition(msg.SlotIndex));
        }

        private void Write(string text, Vector3 worldPosition)
        {
            var gameCamera = Camera.main;
            if (_lines == null || _lines.Length == 0 || gameCamera == null || _canvas == null)
            {
                return;
            }

            var line = _lines[_nextLine];
            _nextLine = (_nextLine + 1) % _lines.Length;

            // Walls sit at the screen's edge: keep the whole line on screen rather than half off it.
            var parent = (RectTransform)line.transform.parent;
            var local = RectAnchorMath.GameWorldToLocalPoint(parent, _canvas, gameCamera,
                worldPosition + (Vector3)_worldOffset);
            var halfSize = ((RectTransform)line.transform).rect.size * 0.5f;
            line.Show(text, RectAnchorMath.ClampInside(parent.rect, local, halfSize));
        }
    }
}
