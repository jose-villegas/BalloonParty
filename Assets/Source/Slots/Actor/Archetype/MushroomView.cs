using BalloonParty.Configuration.GridActors;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    ///     A meadow mushroom on the board (@ref plan_meadow_mushrooms): the quad the <c>Grid/Mushroom</c> shader
    ///     draws the mushroom and its shadow on, and the cap's answer to what happens to it. A ball bouncing off
    ///     this mushroom pushes <see cref="MushroomCapSpring" /> along the way it came in, and a bomb's blast
    ///     that leaves the cap on sways it the same way. A lance cutting it starts
    ///     <see cref="MushroomCutTimeline" />: the cap falls in two and leaves the stem, until it grows back. A
    ///     blast that takes the cap off throws it away whole. Each frame the view hands the shader how far the
    ///     cap leans and swells, and where what came off it is.
    /// </summary>
    /// <remarks>
    ///     A mushroom at rest costs nothing a frame, whole or cut: the view is switched off until something
    ///     happens to the cap, and switches itself off again once nothing moves. Neither the bounce nor the cut is
    ///     the view's to decide: the ball bounces off the circle <c>MushroomBoard</c> builds for the model, the
    ///     rule says when a lance cuts a cap and when it has grown back, and the prefab has no collider.
    /// </remarks>
    internal sealed class MushroomView : GridActorView
    {
        // The golden ratio's step: each new mushroom's spots are turned as far as can be from every one before.
        private const float SeedStep = 0.61803398875f;

        // A cap cut in the first frame of popping back has no size yet; its pieces are drawn this small.
        private const float MinCutSize = 0.05f;

        // A blown cap is drawn as the one piece of a cut that misses it: the line lies behind the cap, across the
        // way it is thrown. On the stem the line stands this share of the radius clear of the cap; as the cap
        // lifts it comes in to touch the rim, and the shader's band of flesh along a cut shows there as a sliver
        // of the cap's underside at its trailing edge. It is fully in once the cap is a third of the way up.
        private const float UndersideClear = 0.14f;
        private const float UndersideRise = 3f;

        // A cap a blast threw trails a few specks for the first part of its flight: this many at the most, this
        // far apart in time, only while it is still rising or near the top.
        private const int TrailSpecks = 6;
        private const float TrailSeconds = 0.07f;
        private const float TrailUntilArc = 0.35f;

        // A speck's lifetime, size and speed are each within this share of what was asked, so no two are alike.
        private const float SpeckVariety = 0.33f;
        private const float SpeckSmallest = 0.6f;
        private const float SpeckLargest = 1.9f;

        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int LeanId = Shader.PropertyToID("_Lean");
        private static readonly int PulseId = Shader.PropertyToID("_Pulse");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int CapGrowId = Shader.PropertyToID("_CapGrow");
        private static readonly int CutLineId = Shader.PropertyToID("_CutLine");
        private static readonly int CutLeanId = Shader.PropertyToID("_CutLean");
        private static readonly int CutPiecesId = Shader.PropertyToID("_CutPieces");
        private static readonly int CutFallId = Shader.PropertyToID("_CutFall");

        [SerializeField] private Renderer _body;

        [Tooltip("The mushroom's spores: specks let go on a hit, a cut and a blast. One system, emitted into by hand.")]
        [SerializeField] private ParticleSystem _spores;

        private readonly CompositeDisposable _subscriptions = new();

        private static int _viewsMade;
        private IMushroomSettings _settings;
        private ISubscriber<MushroomBouncedMessage> _bounced;
        private ISubscriber<MushroomCutMessage> _cutBy;
        private ISubscriber<MushroomRegrownMessage> _regrown;
        private ISubscriber<MushroomSwayedMessage> _swayed;
        private SlotGrid _grid;
        private MaterialPropertyBlock _block;
        private MushroomCapSpring _spring;
        private MushroomCutTimeline _cut;

        // The cut, in the quad's own space: xy a point of the lance's line, zw its way. And the lean and the size
        // the cap had when it was cut (under 1 only for one cut as it popped back), which its pieces start from.
        private Vector4 _cutLine = new(0f, 0f, 1f, 0f);
        private Vector2 _cutLean;
        private float _cutSize = 1f;

        // The way a blast threw the cap, in the quad's own space.
        private Vector2 _blownWay = Vector2.up;
        private float _seed;

        // The specks' own rolls, so that letting spores go never moves the run's seeded random on.
        private XorShift32 _sporeRoll;
        private int _trailLeft;
        private float _trailClock;

        /// <summary>Where the cap stands off its rest, in world units: what the shader is drawing.</summary>
        internal Vector2 Lean => _spring.Lean;

        /// <summary>True from a lance's cut, or a blast that took the cap off, until the cap starts to grow back:
        /// the mushroom is a bare stem.</summary>
        internal bool IsCut => _cut.IsCut;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();

            // Counted, not rolled: a roll would move the run's seeded random on.
            _seed = Mathf.Repeat(++_viewsMade * SeedStep, 1f);
            _sporeRoll = new XorShift32(_viewsMade);
            enabled = false;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        [Inject]
        internal void Construct(
            IMushroomSettings settings, ISubscriber<MushroomBouncedMessage> bounced,
            ISubscriber<MushroomCutMessage> cutBy, ISubscriber<MushroomRegrownMessage> regrown,
            ISubscriber<MushroomSwayedMessage> swayed, SlotGrid grid)
        {
            _settings = settings;
            _bounced = bounced;
            _cutBy = cutBy;
            _regrown = regrown;
            _swayed = swayed;
            _grid = grid;
        }

        public override void OnSpawned()
        {
            base.OnSpawned();
            Rest();

            // A spawn with no despawn before it must not leave two subscriptions: every bounce would lean the
            // cap twice.
            _subscriptions.Clear();
            _bounced?.Subscribe(OnBounced).AddTo(_subscriptions);
            _cutBy?.Subscribe(OnCut).AddTo(_subscriptions);
            _regrown?.Subscribe(OnRegrown).AddTo(_subscriptions);
            _swayed?.Subscribe(OnSwayed).AddTo(_subscriptions);
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            _subscriptions.Clear();
            Rest();
        }

        /// <summary>
        ///     A ball arriving along <paramref name="incoming" /> at <paramref name="speed" /> hit this mushroom:
        ///     the cap leans that way, and swells too when the bounce paid the lance. Nothing bounces off a
        ///     mushroom whose cap is cut, and a hit on one does nothing.
        /// </summary>
        internal void Hit(Vector2 incoming, float speed, bool paid)
        {
            if (_settings == null || IsCut)
            {
                return;
            }

            var tuning = _settings.Lean;
            _spring.Hit(incoming, speed, tuning);
            if (paid)
            {
                _spring.Pay(tuning);
            }

            enabled = !_spring.IsAtRest || !_cut.IsAtRest;
        }

        /// <summary>
        ///     A bomb's blast reached this mushroom and left its cap on: the cap leans along
        ///     <paramref name="away" />, the way from the blast, as far as <paramref name="strength" /> (0 to 1)
        ///     of the tuned sway, and swings back toward the blast. A hit with no ball: nothing is paid. A bare
        ///     stem does not sway.
        /// </summary>
        internal void Sway(Vector2 away, float strength)
        {
            if (_settings == null || IsCut)
            {
                return;
            }

            _spring.Sway(transform.InverseTransformDirection(away), strength, _settings.Lean);
            enabled = !_spring.IsAtRest || !_cut.IsAtRest;
        }

        /// <summary>
        ///     Lets the specks of <paramref name="burst" /> go: each from the burst's origin, somewhere inside
        ///     its fan, slowing and fading as the particle system has it, greenish to bluish. The smoke is not
        ///     the view's: the smoke field draws it.
        /// </summary>
        internal void EmitSpores(in MushroomSporeBurst burst)
        {
            if (_spores == null || _settings == null)
            {
                return;
            }

            var tuning = _settings.Spores;
            for (var i = 0; i < burst.Specks; i++)
            {
                var turn = (Roll() - 0.5f) * burst.FanDegrees * Mathf.Deg2Rad;
                EmitSpeck(tuning, burst.Origin, Turned(burst.Direction, turn), burst.SpeckSpeed);
            }
        }

        /// <summary>
        ///     A lance flying along <paramref name="direction" /> through <paramref name="worldPoint" /> cut the
        ///     cap: it splits along that line, the two pieces fall away and the stem is left. A cap already cut
        ///     has nothing left to cut.
        /// </summary>
        internal void Cut(Vector2 worldPoint, Vector2 direction)
        {
            if (_settings == null || IsCut)
            {
                return;
            }

            Vector2 point = transform.InverseTransformPoint(worldPoint);
            Vector2 way = transform.InverseTransformDirection(direction);
            way = way.sqrMagnitude > 1e-8f ? way.normalized : Vector2.right;
            _cutLine = new Vector4(point.x, point.y, way.x, way.y);

            TakeCapAsItStands();
            _cut.Cut();
            enabled = true;
            Push();
        }

        /// <summary>
        ///     A blast blew the cap off along <paramref name="away" />, the way from the blast: nothing split it,
        ///     so it comes off whole and is thrown that way, and the stem is left. A cap already off has nothing
        ///     left to blow.
        /// </summary>
        internal void BlowOff(Vector2 away)
        {
            if (_settings == null || IsCut)
            {
                return;
            }

            Vector2 way = transform.InverseTransformDirection(away);
            _blownWay = way.sqrMagnitude > 1e-8f ? way.normalized : Vector2.up;
            TakeCapAsItStands();
            _cut.Blow();
            _trailLeft = TrailSpecks;
            _trailClock = 0f;
            enabled = true;
            Push();
        }

        /// <summary>The cap pops back on its stem. Nothing happens to a cap that is whole.</summary>
        internal void Regrow()
        {
            _cut.Regrow();
            enabled = !_spring.IsAtRest || !_cut.IsAtRest;
            Push();
        }

        /// <summary>One frame of the cap's motion. Switches the view off once nothing moves.</summary>
        internal void Tick(float deltaTime)
        {
            var moving = false;
            if (_settings != null)
            {
                // Both are stepped every frame: a short-circuit would leave one a frame behind the other.
                var leaning = _spring.Step(deltaTime, _settings.Lean);
                var falling = _cut.Step(deltaTime, _settings.Cut);
                moving = leaning || falling;
                TrailSpores(deltaTime);
            }

            enabled = moving;
            Push();
        }

        private void OnBounced(MushroomBouncedMessage message)
        {
            if (IsOwnSlot(message.Slot))
            {
                Hit(message.Incoming, message.Speed, message.Reward == MushroomReward.Arm);
            }
        }

        private void OnCut(MushroomCutMessage message)
        {
            if (!IsOwnSlot(message.Slot))
            {
                return;
            }

            if (message.Cause == MushroomCutCause.Blast)
            {
                BlowOff(message.Direction);
            }
            else
            {
                Cut(message.Point, message.Direction);
            }
        }

        private void OnSwayed(MushroomSwayedMessage message)
        {
            if (IsOwnSlot(message.Slot))
            {
                Sway(message.Direction, message.Strength);
            }
        }

        private void OnRegrown(MushroomRegrownMessage message)
        {
            if (IsOwnSlot(message.Slot))
            {
                Regrow();
            }
        }

        private bool IsOwnSlot(Vector2Int slot)
        {
            return ReferenceEquals(_grid.ViewAt(slot), this);
        }

        // What comes off is the cap as it stood, so one taken mid wobble does not jump back to its rest first,
        // and one taken as it popped back is not suddenly full size; the stem is left standing straight.
        private void TakeCapAsItStands()
        {
            _cutLean = _spring.Lean;
            _cutSize = Mathf.Max(_cut.CapGrow, MinCutSize);
            _spring.Reset();
        }

        // A cap in the air sheds a speck now and then while it rises, from where it is, left behind it.
        private void TrailSpores(float deltaTime)
        {
            if (_trailLeft <= 0 || _spores == null)
            {
                return;
            }

            if (!_cut.IsBlown || (_trailLeft < TrailSpecks && _cut.Arc < TrailUntilArc))
            {
                _trailLeft = 0;
                return;
            }

            _trailClock += deltaTime;
            if (_trailClock < TrailSeconds)
            {
                return;
            }

            _trailClock = 0f;
            _trailLeft--;
            var tuning = _settings.Spores;
            Vector2 cap = transform.TransformPoint(_cutLean + (_blownWay * _cut.Part(_settings.Cut)));
            Vector2 back = transform.TransformDirection(-_blownWay);
            EmitSpeck(tuning, cap, Turned(back, (Roll() - 0.5f) * Mathf.PI), tuning.SpeckSpeed * 0.3f);
        }

        private void EmitSpeck(in MushroomSporeTuning tuning, Vector2 from, Vector2 way, float speed)
        {
            // Most are small and a few big ones lead: the roll's cube leaves few near the largest.
            var lead = Roll();
            var speck = new ParticleSystem.EmitParams
            {
                position = from + (way * (tuning.SpeckSize * Roll())),
                velocity = way * (speed * Varied()),
                startLifetime = tuning.SpeckSeconds * Varied(),
                startSize = tuning.SpeckSize * Mathf.Lerp(SpeckSmallest, SpeckLargest, lead * lead * lead),
                startColor = Color.Lerp(tuning.SpeckGreen, tuning.SpeckBlue, Roll())
            };

            // The system never plays by itself, and one that is not playing does not move what is emitted into it.
            if (!_spores.isPlaying)
            {
                _spores.Play();
            }

            _spores.Emit(speck, 1);
        }

        private float Roll()
        {
            return (_sporeRoll.NextUInt() >> 8) * (1f / 16777216f);
        }

        private float Varied()
        {
            return 1f + ((Roll() - 0.5f) * 2f * SpeckVariety);
        }

        private static Vector2 Turned(Vector2 way, float radians)
        {
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2((way.x * cos) - (way.y * sin), (way.x * sin) + (way.y * cos));
        }

        // The line a blown cap is the one piece of: across the way it is thrown, behind it, with the cap on its
        // left. The shader moves a piece off its line to its own side, which here is the way of the throw.
        private Vector4 BlownLine(float radius)
        {
            var clear = UndersideClear * (1f - Mathf.Clamp01(_cut.Arc * UndersideRise));
            var behind = _cutLean - (_blownWay * (radius * _cutSize * (1f + clear)));
            return new Vector4(behind.x, behind.y, _blownWay.y, -_blownWay.x);
        }

        private void Rest()
        {
            _spring.Reset();
            _cut.Reset();
            _cutLean = Vector2.zero;
            _cutSize = 1f;
            _blownWay = Vector2.up;
            _trailLeft = 0;
            if (_spores != null)
            {
                _spores.Clear();
            }

            enabled = false;
            Push();
        }

        private void Push()
        {
            if (_body == null)
            {
                return;
            }

            _block ??= new MaterialPropertyBlock();
            _body.GetPropertyBlock(_block);
            var tuning = default(MushroomCutTuning);
            if (_settings != null)
            {
                _block.SetFloat(RadiusId, _settings.Radius);
                tuning = _settings.Cut;
            }

            _block.SetVector(LeanId, _spring.Lean);
            _block.SetFloat(PulseId, _spring.Pulse);

            // Different on every mushroom, so no two are spotted alike. Each is its own draw anyway: the shader
            // draws from the quad's own space, which a batch would not keep.
            _block.SetFloat(SeedId, _seed);

            // With no pieces showing the shader skips all of the cut's work, so a whole cap costs what it did.
            _block.SetFloat(CapGrowId, _cut.CapGrow);

            // A blown cap is one piece: the shader is told so and skips the other, and it turns the way a thing
            // thrown that way would tumble.
            var blown = _cut.IsBlown;
            var turn = _cut.Turn(tuning) * (blown && _blownWay.x >= 0f ? -1f : 1f);
            _block.SetVector(CutLineId, blown ? BlownLine(_settings != null ? _settings.Radius : 0f) : _cutLine);
            _block.SetVector(CutLeanId, new Vector4(_cutLean.x, _cutLean.y, _cutSize, 0f));
            _block.SetVector(
                CutPiecesId, new Vector4(_cut.Part(tuning), _cut.Carry(tuning), turn, _cut.PieceSize(tuning)));
            _block.SetVector(
                CutFallId, new Vector4(_cut.PieceFade, _cut.ShadowHeight(tuning), blown ? 1f : 0f, 0f));
            _body.SetPropertyBlock(_block);
        }
    }
}
