using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Configuration.CardReveal;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Game.CardReveal;
using BalloonParty.Configuration.Effects;
using BalloonParty.Shared;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.SceneLight;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BalloonParty.UI.CardReveal
{
    /// <summary>
    ///     The card reveal's stage (<see cref="ICardReveals" />): plays each reveal the controller hands it. A
    ///     silver balloon floats up to its place with two pieces of ribbon rolled up inside it, a pushpin pricks
    ///     it, and it pops (one of the baked pops, its two rubber layers either side of the ribbons, starting as
    ///     the needle goes in at the pop's puncture), and the ribbons fly off it, one with the card's title on it
    ///     and one with what the card does, readable as they drift. On a level-up they fly with the level's
    ///     ascent and are gone just before the next level settles. The rubber and the pin are surface data lit live
    ///     by <c>BalloonParty/Sprite/LitFlipbook</c>, so they take the scene's light, and the tear flashes a light
    ///     into the scene light field, stirs the disturbance field as a board pop does, and flings shreds far
    ///     out. It sits on its own World Space canvas among the board's sprites
    ///     (<see cref="CameraFittedCanvas" />), so the screen-space light tints it and sees it. Only the playing
    ///     pop's sheet and the next one's are in memory (<see cref="PopSheetLoader" />).
    ///     Attaches as the stage only when there are pops to play; until then the tooltip announces the cards.
    ///     Built by <c>Tools/BalloonParty/CardReveal/Build Card Reveal</c>.
    /// </summary>
    public class CardRevealView : MonoBehaviour
    {
        private static readonly int SheetGridId = Shader.PropertyToID("_SheetGrid");

        // Where a pop with no puncture listed is pricked: the top of the balloon's front.
        private static readonly Vector2 DefaultPuncture = new(0.5f, 0.8f);

        [Tooltip("Holds the whole reveal; shown while one plays.")]
        [SerializeField] private CanvasGroup _group;

        [Tooltip("Moves as the balloon: the two rubber layers and the ribbons between them.")]
        [SerializeField] private RectTransform _balloon;

        [Tooltip("The balloon's drop shadow, behind the back layer: the back layer's frame, on the shadow's material.")]
        [SerializeField] private RawImage _shadow;

        [SerializeField] private RawImage _back;
        [SerializeField] private RawImage _front;
        [SerializeField] private RibbonPiece _titleRibbon;
        [SerializeField] private RibbonPiece _descriptionRibbon;

        [Tooltip("Tucked behind the title on a bonus that landed on a card at its limit, in place of the " +
                 "description; the title is struck through.")]
        [SerializeField] private RibbonPiece _cappedRibbon;

        [Tooltip("What the capped ribbon says.")]
        [SerializeField] private string _cappedText = "Capped";

        [Tooltip("The pushpin that pricks the balloon, over it, hung from its needle's tip; its head and needle are " +
                 "tinted apart. None, and the balloon pops on its own.")]
        [SerializeField] private RectTransform _pin;

        [SerializeField] private RawImage _pinHead;
        [SerializeField] private RawImage _pinNeedle;

        [Tooltip("How far below its place the balloon starts its rise, in canvas units.")]
        [SerializeField] private float _riseDistance = 260f;

        [Inject] private ICardReveals _cardReveals;
        [Inject] private ICardRevealSettings _settings;
        [Inject] private ICinematicsSettings _cinematics;
        [Inject] private ISubscriber<LevelAscendStartedMessage> _ascendStarted;
        [Inject] private SceneLightFieldService _lightField;
        [Inject] private DisturbanceFieldService _disturbanceField;
        [Inject] private PoolManager _pool;

        private CancellationTokenSource _playing;
        private IDisposable _subscriptions;
        private PopSheetLoader _sheets;
        private Material _rubber;
        private Material _rubberShadow;
        private Vector2 _place;
        private Vector2 _puncture;
        private Vector2 _pinAxis;
        private float _ascentStartedAt = float.NegativeInfinity;
        private float _releasedAt;
        private float _exitFrom;

        // This pop's swing: its reach and pace (around 1) and where in the swing the fall starts.
        private float _swingReach = 1f;
        private float _swingPace = 1f;
        private float _swingPhase;
        private float _spinRate;
        private bool _attached;
        private bool _burst;

        // All three, or the balloon pops on its own: a prefab edited by hand can't leave half a pin.
        private bool PinBuilt => _pin != null && _pinHead != null && _pinNeedle != null;

        private void Awake()
        {
            _place = _balloon.anchoredPosition;
            _group.alpha = 0f;
        }

        private void Start()
        {
            if (_settings.PopSheetPaths.Count == 0)
            {
                return;
            }

            _sheets = new PopSheetLoader(_settings.PopSheetPaths);

            // The shader keeps its taps inside the frame being shown, so it needs the sheet's grid. Copies of the
            // materials, so the assets aren't written in the editor.
            _rubber = WithSheetGrid(_back.material);
            _rubberShadow = WithSheetGrid(_shadow.material);
            _back.material = _rubber;
            _front.material = _rubber;
            _shadow.material = _rubberShadow;

            _cardReveals.AttachStage();
            _attached = true;
            _subscriptions = new CompositeDisposable(
                _ascendStarted.Subscribe(_ => _ascentStartedAt = Time.unscaledTime),
                _cardReveals.Current.Subscribe(OnReveal));
        }

        private void OnDestroy()
        {
            // Unsubscribed first: detaching drops the queue, and the null that publishes must not reach a view
            // being torn down.
            _subscriptions?.Dispose();
            Stop();
            _sheets?.Dispose();
            if (_attached)
            {
                _cardReveals.DetachStage();
            }

            if (_rubber != null)
            {
                Destroy(_rubber);
                Destroy(_rubberShadow);
            }
        }

        /// <summary>
        ///     The UV rect of a frame of one layer: the front layer's frames fill the sheet's top half, the back
        ///     layer's its bottom half, each read left to right, top to bottom.
        /// </summary>
        internal static Rect FrameRect(int frame, bool front, int columns, int rows)
        {
            var width = 1f / columns;
            var height = 0.5f / rows;
            var row = frame / columns;
            var top = front ? 1f : 0.5f;
            return new Rect(frame % columns * width, top - (row + 1) * height, width, height);
        }

        /// <summary>
        ///     When the ribbons released at <paramref name="releasedAt" /> must be gone, on the unscaled clock. With
        ///     an ascent begun since the reveal started (<paramref name="ascentStartedAt" />), at the settings' share
        ///     of it, so they leave just before the next level settles, though never sooner than their shortest
        ///     flight; with none, their flight without an ascent.
        /// </summary>
        internal static float RibbonsGoneAt(float releasedAt, float revealStartedAt, float ascentStartedAt,
            float ascentSeconds, ICardRevealSettings settings)
        {
            if (ascentStartedAt < revealStartedAt)
            {
                return releasedAt + settings.RibbonSecondsWithoutAscent;
            }

            var withAscent = ascentStartedAt + ascentSeconds * Mathf.Clamp01(settings.RibbonsGoneAtAscent) +
                             settings.RibbonsAfterAscentSeconds;
            return Mathf.Max(withAscent, releasedAt + settings.RibbonMinimumSeconds);
        }

        /// <summary>
        ///     Where the balloon floats <paramref name="seconds" /> into its rise, from its place: x and y in canvas
        ///     units, z its tilt in degrees. It rises as a spring let go <paramref name="distance" /> below, floating
        ///     <see cref="ICardRevealSettings.RiseOvershoot" /> past its place at
        ///     <see cref="ICardRevealSettings.RiseSeconds" /> and bobbing back to rest, and sways side to side,
        ///     leaning into the sway, both dying away, the way a balloon let go rises.
        /// </summary>
        internal static Vector3 FloatOffset(float seconds, float distance, ICardRevealSettings settings)
        {
            // An under-damped spring whose first peak is the overshoot, reached at RiseSeconds.
            var logOvershoot = Mathf.Log(Mathf.Clamp(settings.RiseOvershoot, 1e-3f, 0.9f));
            var damping = -logOvershoot / Mathf.Sqrt(Mathf.PI * Mathf.PI + logOvershoot * logOvershoot);
            var bobRate = Mathf.PI / Mathf.Max(settings.RiseSeconds, 1e-3f);
            var decayRate = damping / Mathf.Sqrt(1f - damping * damping) * bobRate;
            var below = Mathf.Exp(-decayRate * seconds) *
                        (Mathf.Cos(bobRate * seconds) + decayRate / bobRate * Mathf.Sin(bobRate * seconds));

            var swayPhase = Mathf.PI * seconds / Mathf.Max(settings.RiseSwaySeconds, 1e-3f);
            var swayFade = Mathf.Exp(-0.5f * decayRate * seconds);
            return new Vector3(
                settings.RiseSway * Mathf.Sin(swayPhase) * swayFade,
                -distance * below,
                -settings.RiseTilt * Mathf.Cos(swayPhase) * swayFade);
        }

        /// <summary>
        ///     How the torn rubber swings <paramref name="sincePop" /> seconds into the pop: x sideways in frame widths,
        ///     y its lean in degrees. Nothing until it starts to fall, then side to side every
        ///     <see cref="ICardRevealSettings.RubberSwingSeconds" /> × <paramref name="pace" />, starting
        ///     <paramref name="phase" /> into the swing, growing in over one swing so the fall starts square, and
        ///     leaning into it as the rise's sway does.
        /// </summary>
        internal static Vector2 FallSwing(
            float sincePop, float reach, float pace, float phase, ICardRevealSettings settings)
        {
            var falling = sincePop - settings.RubberFallsFromFrame / Mathf.Max(settings.FramesPerSecond, 1e-3f);
            if (falling <= 0f)
            {
                return Vector2.zero;
            }

            var swingSeconds = Mathf.Max(settings.RubberSwingSeconds * pace, 1e-3f);
            var angle = Mathf.PI * falling / swingSeconds + phase;
            var grow = Mathf.SmoothStep(0f, 1f, falling / swingSeconds);
            return new Vector2(
                settings.RubberSwing * reach * Mathf.Sin(angle) * grow,
                -settings.RubberSwingTilt * reach * Mathf.Cos(angle) * grow);
        }

        /// <summary>
        ///     How far the torn rubber has turned <paramref name="sincePop" /> seconds into the pop, in degrees, at
        ///     <paramref name="rate" /> degrees a second (its sign the direction). Nothing until it starts to fall;
        ///     then the turn picks up over one <see cref="ICardRevealSettings.RubberSwingSeconds" />, so the fall
        ///     starts square, and runs on at the rate, falling behind it by that one swing's worth.
        /// </summary>
        internal static float FallSpin(float sincePop, float rate, ICardRevealSettings settings)
        {
            var falling = sincePop - settings.RubberFallsFromFrame / Mathf.Max(settings.FramesPerSecond, 1e-3f);
            if (falling <= 0f)
            {
                return 0f;
            }

            var buildUp = Mathf.Max(settings.RubberSwingSeconds, 1e-3f);
            return rate * falling * falling / (falling + buildUp);
        }

        /// <summary>
        ///     How far down the frames move <paramref name="sincePop" /> seconds into the pop, in frame heights: the
        ///     bake's camera's path. Nothing until the fall's frame, then speeding up under the settings' gravity,
        ///     counted in the pop's frames as the bake's knot falls, so the rubber falling with it in the frames
        ///     matches the motion at any frame rate; plus the recentring, the bake's camera easing further down to
        ///     keep the gathered rubber in the frame, added back so the knot's path stays the fall's.
        /// </summary>
        internal static float FallDistance(float sincePop, ICardRevealSettings settings)
        {
            var frame = sincePop * settings.FramesPerSecond;
            var falling = frame - settings.RubberFallsFromFrame;
            var drop = falling > 0f ? 0.5f * settings.RubberFallGravity * falling * falling : 0f;
            var recentring = (frame - settings.RubberRecentreFromFrame) / Mathf.Max(1f, settings.RubberRecentreFrames);
            return drop + settings.RubberRecentreHeight * Mathf.SmoothStep(0f, 1f, recentring);
        }

        /// <summary>
        ///     Where the pin is <paramref name="sinceContact" /> seconds from its needle reaching the puncture
        ///     (negative before): x its distance back from the puncture along its axis, in canvas units (below 0 is
        ///     into the rubber), y its opacity. It comes in from afar, easing to a hover by the puncture and fading
        ///     in, draws back, jabs in speeding up, stays in as the rubber tears, then the burst throws it back out
        ///     as it fades. Until <see cref="PinLeadSeconds" /> before contact it is out of sight.
        /// </summary>
        internal static Vector2 PinPose(float sinceContact, ICardRevealSettings settings)
        {
            var jabFrom = -settings.PinJabSeconds;
            var windupFrom = jabFrom - settings.PinWindupSeconds;
            var enterFrom = windupFrom - settings.PinEnterSeconds;
            var hover = settings.PinHoverDistance;
            var drawn = hover + settings.PinWindupDistance;
            if (sinceContact < enterFrom)
            {
                return new Vector2(hover + settings.PinEnterDistance, 0f);
            }

            if (sinceContact < windupFrom)
            {
                var entered = Progress(sinceContact - enterFrom, settings.PinEnterSeconds);
                var left = 1f - entered;
                return new Vector2(hover + settings.PinEnterDistance * left * left * left, Mathf.Clamp01(2f * entered));
            }

            if (sinceContact < jabFrom)
            {
                var wound = Progress(sinceContact - windupFrom, settings.PinWindupSeconds);
                return new Vector2(Mathf.Lerp(hover, drawn, Mathf.SmoothStep(0f, 1f, wound)), 1f);
            }

            if (sinceContact < 0f)
            {
                var jabbed = Progress(sinceContact - jabFrom, settings.PinJabSeconds);
                return new Vector2(Mathf.Lerp(drawn, -settings.PinDepth, jabbed * jabbed), 1f);
            }

            if (sinceContact < settings.PinStaySeconds)
            {
                return new Vector2(-settings.PinDepth, 1f);
            }

            var gone = Progress(sinceContact - settings.PinStaySeconds, settings.PinLeaveSeconds);
            var still = 1f - gone;
            return new Vector2(Mathf.Lerp(-settings.PinDepth, settings.PinRecoilDistance, 1f - still * still * still),
                still);
        }

        /// <summary>How long before its needle goes in the pin starts to come in.</summary>
        internal static float PinLeadSeconds(ICardRevealSettings settings)
        {
            return settings.PinEnterSeconds + settings.PinWindupSeconds + settings.PinJabSeconds;
        }

        /// <summary>
        ///     When the pop starts, from the start of the balloon's float: as it comes to rest, or with a pin
        ///     (<paramref name="hasPin" />) slower than that, once the pin is in, the balloon holding at rest longer.
        /// </summary>
        internal static float ContactAt(bool hasPin, ICardRevealSettings settings)
        {
            var floatSeconds = settings.RiseSeconds + settings.HoldBeforePopSeconds;
            return hasPin ? Mathf.Max(floatSeconds, PinLeadSeconds(settings)) : floatSeconds;
        }

        /// <summary>
        ///     Where pop <paramref name="pop" /> is punctured, as a share of its frame from the bottom left: its
        ///     listed puncture, or the top of the balloon's front if it has none.
        /// </summary>
        internal static Vector2 Puncture(int pop, IReadOnlyList<Vector2> punctures)
        {
            return pop >= 0 && pop < punctures.Count ? punctures[pop] : DefaultPuncture;
        }

        // Also called with the null Current holds on subscribing, which starts the next reveal's sheet loading.
        private void OnReveal(RevealedCard reveal)
        {
            Stop();
            // Off the layers before a sheet is let go: an unloaded texture would draw as blank.
            ShowSheet(null);
            HoldSheets(reveal);
            if (reveal == null)
            {
                // A reset or a detach dropped it: nothing left to finish.
                _group.alpha = 0f;
                return;
            }

            _playing = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            PlayAsync(reveal, _playing.Token).Forget();
        }

        private void Stop()
        {
            _playing?.Cancel();
            _playing?.Dispose();
            _playing = null;
        }

        private async UniTaskVoid PlayAsync(RevealedCard reveal, CancellationToken token)
        {
            try
            {
                // From before the sheet's wait: a reveal whose sheet came late still leaves with its ascent.
                var startedAt = Time.unscaledTime;
                var pop = SheetOf(reveal.Pop);
                var sheet = await _sheets.GetAsync(pop, token);
                if (sheet == null)
                {
                    _cardReveals.Finish(reveal);
                    return;
                }

                Dress(reveal, pop, sheet);
                var contactAt = ContactAt(PinBuilt, _settings);
                await AnimateAsync(contactAt, t =>
                {
                    Float(t * contactAt);
                    PlacePin(t * contactAt - contactAt);
                }, token);
                Float(float.PositiveInfinity);
                await PopAsync(startedAt, token);
                _group.alpha = 0f;
                _cardReveals.Finish(reveal);
            }
            catch (OperationCanceledException)
            {
                // Stopped for a reset, a newer reveal or the scene closing; the controller already moved on.
            }
        }

        // The pop's frames and the ribbons' flight on one clock. The frame follows the time played rather than one
        // delay per frame, which would round up to whole screen refreshes and run slow; a frame the screen skips is
        // skipped, not delayed. The ribbons' end is looked at again each frame until they start to leave, since the
        // level's ascent can begin after they are out. Both clocks are unscaled: the ascent runs on unscaled time
        // too (LevelAscendCinematic), so its length here is its length on screen.
        private async UniTask PopAsync(float revealStartedAt, CancellationToken token)
        {
            var frames = _settings.SheetColumns * _settings.SheetRows;
            var releaseAt = Mathf.Min(_settings.RibbonsFromFrame, frames - 1) / _settings.FramesPerSecond;
            _releasedAt = float.NaN;
            _exitFrom = float.NaN;
            _burst = false;
            RollSwing();
            var elapsed = 0f;
            while (true)
            {
                // Past the last frame the rubber has fallen out of view: the layers stop drawing (and sampling).
                var frame = (int)(elapsed * _settings.FramesPerSecond);
                SetRubberShown(frame < frames);
                ShowFrame(Mathf.Min(frame, frames - 1));
                var swing = FallSwing(elapsed, _swingReach, _swingPace, _swingPhase, _settings);
                swing.y += FallSpin(elapsed, _spinRate, _settings);
                Fall(FallDistance(elapsed, _settings), swing);
                PlacePin(elapsed);
                BurstOnTear(frame);
                var gone = elapsed >= releaseAt && FlyRibbons(revealStartedAt) >= 1f;
                if (gone && elapsed * _settings.FramesPerSecond >= frames)
                {
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.unscaledDeltaTime;
            }
        }

        // Moves both ribbons on, and says how far through their exit they are (0 to 1).
        private float FlyRibbons(float revealStartedAt)
        {
            var now = Time.unscaledTime;
            var exitSeconds = Mathf.Max(0.01f, _settings.RibbonExitSeconds);
            if (float.IsNaN(_releasedAt))
            {
                _releasedAt = now;
            }

            if (float.IsNaN(_exitFrom) && now >= RibbonsGoneAt(_releasedAt, revealStartedAt, _ascentStartedAt,
                    _cinematics.LevelAscend.AscentSeconds, _settings) - exitSeconds)
            {
                _exitFrom = now;
            }

            var exit = float.IsNaN(_exitFrom) ? 0f : Mathf.Clamp01((now - _exitFrom) / exitSeconds);
            _titleRibbon.Fly(now - _releasedAt, exit, _settings);
            if (_descriptionRibbon.gameObject.activeSelf)
            {
                _descriptionRibbon.Fly(now - _releasedAt, exit, _settings);
            }

            if (_cappedRibbon != null && _cappedRibbon.gameObject.activeSelf)
            {
                _cappedRibbon.Fly(now - _releasedAt, exit, _settings);
            }

            return exit;
        }

        private void Dress(RevealedCard reveal, int pop, Texture2D sheet)
        {
            ShowSheet(sheet);

            var tint = _settings.BalloonColor;
            _back.color = tint;
            _shadow.color = tint;
            tint.a = 1f - _settings.InsideShowThrough;
            _front.color = tint;

            DressRibbons(reveal);
            SetRubberShown(true);
            ShowFrame(0);
            Float(0f);
            DressPin(pop);
            _group.alpha = 1f;
        }

        // A capped bonus keeps its title, struck through, with the capped ribbon behind it instead of the description.
        // A canvas built before the capped ribbon still strikes the title and drops the description.
        private void DressRibbons(RevealedCard reveal)
        {
            var capped = reveal.IsCapped;
            var title = UpgradeCardText.Title(reveal.Card);
            _titleRibbon.Show(capped ? StruckThrough(title) : title);
            _titleRibbon.Rest();
            _descriptionRibbon.gameObject.SetActive(!capped);
            if (!capped)
            {
                _descriptionRibbon.Show(reveal.Card.Description);
                _descriptionRibbon.Rest();
            }

            if (_cappedRibbon == null)
            {
                return;
            }

            _cappedRibbon.gameObject.SetActive(capped);
            if (capped)
            {
                _cappedRibbon.Show(_cappedText);
                _cappedRibbon.Rest();
            }
        }

        private static string StruckThrough(string line)
        {
            return $"<s>{line}</s>";
        }

        // The pin is drawn to the balloon's scale, hung from its needle's tip, and jabs along the angle it was
        // baked at. The puncture is placed from the balloon's place, not its layers: the pin goes in as it rests.
        private void DressPin(int pop)
        {
            if (!PinBuilt)
            {
                return;
            }

            var frame = _back.rectTransform.rect.size;
            _pin.pivot = _settings.PinTip;
            _pin.sizeDelta = Vector2.one * (_settings.PinSize * frame.y);
            _puncture = _place + Vector2.Scale(Puncture(pop, _settings.PopPunctures) - new Vector2(0.5f, 0.5f), frame);
            var lean = _settings.PinLean * Mathf.Deg2Rad;
            _pinAxis = new Vector2(Mathf.Sin(lean), Mathf.Cos(lean));
            PlacePin(float.NegativeInfinity);
        }

        private void PlacePin(float sinceContact)
        {
            if (!PinBuilt)
            {
                return;
            }

            var pose = PinPose(sinceContact, _settings);
            _pin.anchoredPosition = _puncture + _pinAxis * pose.x;
            var shown = pose.y > 0f;
            _pinHead.enabled = shown;
            _pinNeedle.enabled = shown;
            var head = _settings.PinHeadColor;
            head.a = pose.y;
            _pinHead.color = head;
            var needle = _settings.PinNeedleColor;
            needle.a = pose.y;
            _pinNeedle.color = needle;
        }

        // Once, when the rubber tears: a flash of white light in the field (the prize balloon is none of the
        // palette's colours), which lights the board around it and the reveal itself, a pop's stir in the
        // disturbance field, as a board balloon's pop, and the shreds flung far out. The baked frames can't carry
        // rubber far from the balloon, so the shreds are particles, laid out in canvas units: parented to the
        // canvas, they scale with the screen and follow the camera. The pool takes them back when they stop.
        private void BurstOnTear(int frame)
        {
            if (_burst || frame < _settings.RubberFallsFromFrame)
            {
                return;
            }

            _burst = true;
            var at = _balloon.position;
            _lightField.Flash(at, _settings.PopFlashRadius, _settings.PopFlashIntensity, _settings.PopFlashSeconds);
            _disturbanceField.Stamp(StampSource.BalloonPop, at, Vector2.zero);
            if (_settings.ShredsPrefab != null)
            {
                var shreds = _pool.PlayParticle(_settings.ShredsPrefab, at, _settings.BalloonColor).transform;
                shreds.SetParent(transform, false);
                shreds.position = at;
            }
        }

        // The playing reveal's sheet, if any, and the next reveal's, loaded ahead.
        private void HoldSheets(RevealedCard playing)
        {
            _sheets.Hold(playing != null ? SheetOf(playing.Pop) : -1, SheetOf(_cardReveals.NextPop));
        }

        private int SheetOf(int pop)
        {
            return pop < 0 ? -1 : pop % _settings.PopSheetPaths.Count;
        }

        private void ShowSheet(Texture2D sheet)
        {
            _back.texture = sheet;
            _front.texture = sheet;
            _shadow.texture = sheet;
        }

        private Material WithSheetGrid(Material material)
        {
            var copy = new Material(material);
            copy.SetVector(SheetGridId, new Vector4(_settings.SheetColumns, _settings.SheetRows));
            return copy;
        }

        private void SetRubberShown(bool shown)
        {
            _back.enabled = shown;
            _front.enabled = shown;
            _shadow.enabled = shown;
        }

        private void ShowFrame(int frame)
        {
            _front.uvRect = FrameRect(frame, true, _settings.SheetColumns, _settings.SheetRows);
            _back.uvRect = FrameRect(frame, false, _settings.SheetColumns, _settings.SheetRows);
            _shadow.uvRect = _back.uvRect;
        }

        // The layers part by how far the balloon still is from its place, the front leading, and meet there. At
        // infinity it is at rest: the pop starts square at its place, so the rubber falls straight down.
        private void Float(float seconds)
        {
            var offset = float.IsPositiveInfinity(seconds)
                ? Vector3.zero
                : FloatOffset(seconds, _riseDistance, _settings);
            _balloon.anchoredPosition = _place + (Vector2)offset;
            _balloon.localRotation = Quaternion.Euler(0f, 0f, offset.z);
            var part = (Vector2)offset * _settings.Parallax;
            PlaceLayers(part, -part);
            TiltLayers(0f);
        }

        // Only the rubber falls: the ribbons are the balloon's too, and fly their own way.
        private void Fall(float frameHeights, Vector2 swing)
        {
            var size = _back.rectTransform.rect.size;
            var at = new Vector2(swing.x * size.x, -frameHeights * size.y);
            PlaceLayers(at, at);
            TiltLayers(swing.y);
        }

        private void RollSwing()
        {
            var variance = _settings.RubberSwingVariance;
            _swingReach = 1f + UnityEngine.Random.Range(-variance, variance);
            _swingPace = 1f + UnityEngine.Random.Range(-variance, variance);
            _swingPhase = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
            var direction = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            _spinRate = direction * _settings.RubberSpin * (1f + UnityEngine.Random.Range(-variance, variance));
        }

        private void TiltLayers(float degrees)
        {
            var tilt = Quaternion.Euler(0f, 0f, degrees);
            _front.rectTransform.localRotation = tilt;
            _back.rectTransform.localRotation = tilt;
            _shadow.rectTransform.localRotation = tilt;
        }

        private void PlaceLayers(Vector2 front, Vector2 back)
        {
            _front.rectTransform.anchoredPosition = front;
            _back.rectTransform.anchoredPosition = back;
            _shadow.rectTransform.anchoredPosition = back;
        }

        private static float Progress(float elapsed, float seconds)
        {
            return seconds > 0f ? Mathf.Clamp01(elapsed / seconds) : 1f;
        }

        // On unscaled time, like the heads-ups: slow-motion beats don't stretch the reveal.
        private static async UniTask AnimateAsync(float seconds, Action<float> step, CancellationToken token)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                step(elapsed / seconds);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.unscaledDeltaTime;
            }

            step(1f);
        }
    }
}
