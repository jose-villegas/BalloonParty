using System.Collections.Generic;
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
    ///     off balloons n times in a row without touching a wall. A ricochet that earns the ball a shield
    ///     writes the "+1 bounce" small under itself, in the pickup's colour, so the reward reads as coming
    ///     from the ricochet rather than from a pickup nobody saw. Each kind has its own
    ///     <see cref="HeadsUpStyle" />, and the escalating ones grow with their count. Lines still showing
    ///     push a new one aside (<see cref="HeadsUpPacking" />), so a burst doesn't stack into one smudge.
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

        [Tooltip("Written under a ricochet that earned the ball a shield, in the pickup style's colour.")]
        [SerializeField] private string _ricochetRewardText = "+1 <i>bounce</i>";

        [Tooltip("The reward's size against the ricochet line above it.")]
        [SerializeField] [Range(0.3f, 1f)] private float _ricochetRewardScale = 0.65f;

        [Tooltip("World units from where it happened to the line's centre.")]
        [SerializeField] private Vector2 _worldOffset = new(0f, 0.25f);

        [Tooltip("Clear space kept around each line when packing, as a share of the line's height.")]
        [SerializeField] [Min(0f)] private float _packingGap = 0.15f;

        [Inject] private ISubscriber<ShieldGainedMessage> _shieldGainedSubscriber;
        [Inject] private ISubscriber<WallHitMessage> _wallHitSubscriber;
        [Inject] private ISubscriber<DeflectChainMessage> _chainSubscriber;
        [Inject] private ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        [Inject] private SlotGrid _grid;

        private readonly ProjectileTally _pickups = new();
        private readonly List<Rect> _occupied = new();
        private int _nextLine;

        private void Start()
        {
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

            // Still counted, so the next pickup's "+n" counts every shield this flight; the ricochet's own
            // line already says where this one came from.
            var count = _pickups.Record(msg.Projectile);
            if (msg.Source == ShieldGainSource.Ricochet)
            {
                return;
            }

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
            if (msg.PaidShield)
            {
                text += string.Format(CultureInfo.InvariantCulture, "\n<size={0:0}%><color=#{1}>{2}</color></size>",
                    _ricochetRewardScale * 100f, ColorUtility.ToHtmlStringRGBA(_pickupStyle.Color),
                    _ricochetRewardText);
            }

            Write(text, msg.Position, _ricochetStyle, msg.Count - DeflectChainRule.MinChain + 1);
        }

        private static Color Ink(int rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
        }

        private void Write(string text, Vector3 worldPosition, HeadsUpStyle style, int count)
        {
            var gameCamera = Camera.main;
            if (_lines == null || _lines.Length == 0 || gameCamera == null)
            {
                return;
            }

            var line = _lines[_nextLine];
            _nextLine = (_nextLine + 1) % _lines.Length;

            line.Show(text, (Vector2)worldPosition + _worldOffset, style, count);
            Pack(line, HeadsUpPacking.ViewRect(gameCamera));
        }

        // Written first so the ink's size is known, then moved: clear of the lines still showing where it can
        // be, and wholly on screen (walls sit at the screen's edge).
        private void Pack(HandwrittenHeadsUp line, Rect bounds)
        {
            _occupied.Clear();
            foreach (var other in _lines)
            {
                if (other != line && other.IsShowing)
                {
                    _occupied.Add(other.InkRect);
                }
            }

            var ink = line.InkRect;
            var position = (Vector2)line.transform.position;
            var size = ink.size + Vector2.one * (ink.height * _packingGap);
            var center = HeadsUpPacking.Place(bounds, ink.center, size, _occupied);
            line.MoveTo(position + center - ink.center);
        }
    }
}
