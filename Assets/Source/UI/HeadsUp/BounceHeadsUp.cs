using System.Globalization;
using BalloonParty.Projectile;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Grid;
using MessagePipe;
using TMPro;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.HeadsUp
{
    /// <summary>
    ///     Writes how a ball's bounces go, where it happens: "+n bounce" where it picks up a shield (n counting
    ///     its pickups this flight), "-1 bounce" where a wall spends one, and "xn ricochet" where it deflects
    ///     off balloons n times in a row without touching a wall. Each kind has its own
    ///     <see cref="HeadsUpStyle" />, and the escalating ones grow with their count.
    /// </summary>
    /// <remarks>
    ///     The colours follow the heads-up research in <c>UI/HeadsUp/README.md</c>: blue for a gain and plum
    ///     for the skill shot (both apart from the balloon colours and readable to colour-blind players), and
    ///     a quiet graphite for the wall, which happens on nearly every flight: emphasis goes to consequence,
    ///     not frequency, so only the wall that leaves the ball on its last shield turns vermilion.
    /// </remarks>
    public class BounceHeadsUp : MonoBehaviour
    {
        [Tooltip("Lines written in turn, so bounces close together each get their own instead of replacing one.")]
        [SerializeField] private HandwrittenHeadsUp[] _lines;

        [Tooltip("{0} is how many shields the ball has picked up this flight. Rich text, e.g. <i>bounce</i>.")]
        [SerializeField] private string _pickupFormat = "+{0} <i>bounce</i>";
        [SerializeField] private HeadsUpStyle _pickupStyle = new(Ink(0x0072B2), 1f, 0.15f, 1.35f, FontStyles.Bold);

        [SerializeField] private string _wallText = "-1 <i>bounce</i>";
        [SerializeField] private HeadsUpStyle _wallStyle = new(Ink(0x4A4A52, 0.8f), 0.9f, 0f, 0.9f, FontStyles.Normal);

        [Tooltip("The wall that leaves the ball on its last shield: the next wall ends the flight.")]
        [SerializeField] private HeadsUpStyle _lastShieldStyle = new(Ink(0xB34700), 0.9f, 0f, 0.9f, FontStyles.Bold);

        [Tooltip("{0} is the deflects in a row, from the second on.")]
        [SerializeField] private string _ricochetFormat = "x{0} <i>ricochet</i>";
        [SerializeField] private HeadsUpStyle _ricochetStyle = new(Ink(0x8E3B8E), 1.1f, 0.25f, 1.6f, FontStyles.Bold);

        [Tooltip("World units from where it happened to the line's centre.")]
        [SerializeField] private Vector2 _worldOffset = new(0f, 0.25f);

        [Inject] private ISubscriber<ShieldGainedMessage> _shieldGainedSubscriber;
        [Inject] private ISubscriber<WallHitMessage> _wallHitSubscriber;
        [Inject] private ISubscriber<DeflectChainMessage> _chainSubscriber;
        [Inject] private ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        [Inject] private SlotGrid _grid;

        private readonly ProjectileTally _pickups = new();
        private Canvas _canvas;
        private int _nextLine;

        private void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
            _shieldGainedSubscriber.Subscribe(OnShieldGained).AddTo(this);
            _wallHitSubscriber.Subscribe(OnWallHit).AddTo(this);
            _chainSubscriber.Subscribe(OnDeflectChain).AddTo(this);
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
            Write(text, _grid.IndexToWorldPosition(msg.SlotIndex), _pickupStyle, count);
        }

        private void OnWallHit(WallHitMessage msg)
        {
            Write(_wallText, msg.Position, msg.ShieldsRemaining == 0 ? _lastShieldStyle : _wallStyle, 1);
        }

        private void OnDeflectChain(DeflectChainMessage msg)
        {
            // Sized from the first announced chain, so "x2" starts at the style's base size.
            var text = string.Format(CultureInfo.InvariantCulture, _ricochetFormat, msg.Count);
            Write(text, msg.Position, _ricochetStyle, msg.Count - DeflectChainRule.MinChain + 1);
        }

        private static Color Ink(int rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
        }

        private void Write(string text, Vector3 worldPosition, HeadsUpStyle style, int count)
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
            line.Show(text, RectAnchorMath.ClampInside(parent.rect, local, halfSize), style, count);
        }
    }
}
