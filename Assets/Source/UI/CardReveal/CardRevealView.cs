using System;
using System.Threading;
using BalloonParty.Configuration.CardReveal;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.CardReveal;
using BalloonParty.Configuration.Effects;
using BalloonParty.Game.Level;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.SceneLight;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BalloonParty.UI.CardReveal
{
    /// <summary>
    ///     The card reveal's stage (<see cref="ICardReveals" />): plays each reveal the controller hands it. A
    ///     balloon in one of the level's colours rises to its place with two pieces of ribbon rolled up inside it,
    ///     pops (one of the baked pops, its two rubber layers either side of the ribbons), and the ribbons fly off
    ///     it, one with the card's title on it and one with what the card does, readable as they drift. On a
    ///     level-up they fly with the level's ascent and are gone just before the next level settles. The rubber
    ///     is surface data lit live by <c>BalloonParty/Sprite/LitFlipbook</c>, so it takes the scene's light, and
    ///     its tear flashes a light of its colour into the scene light field and stirs the disturbance field, as a
    ///     board pop does. It sits on its own World Space canvas among the board's sprites
    ///     (<see cref="CameraFittedCanvas" />), so the screen-space light tints it and sees it.
    ///     Attaches as the stage only when there are pops to play; until then the tooltip announces the cards.
    ///     Built by <c>Tools/BalloonParty/CardReveal/Build Card Reveal</c>.
    /// </summary>
    public class CardRevealView : MonoBehaviour
    {
        private static readonly int SheetGridId = Shader.PropertyToID("_SheetGrid");

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

        [Tooltip("How far below its place the balloon starts its rise, in canvas units.")]
        [SerializeField] private float _riseDistance = 260f;

        [Inject] private ICardReveals _cardReveals;
        [Inject] private ICardRevealSettings _settings;
        [Inject] private ICinematicsSettings _cinematics;
        [Inject] private IGamePalette _palette;
        [Inject] private IActiveLevelParameters _level;
        [Inject] private ISubscriber<LevelAscendStartedMessage> _ascendStarted;
        [Inject] private SceneLightFieldService _lightField;
        [Inject] private DisturbanceFieldService _disturbanceField;

        private CancellationTokenSource _playing;
        private IDisposable _subscriptions;
        private Material _rubber;
        private Material _rubberShadow;
        private Vector2 _place;
        private string _colorId;
        private float _ascentStartedAt = float.NegativeInfinity;
        private float _releasedAt;
        private float _exitFrom;
        private bool _attached;
        private bool _burst;

        private void Awake()
        {
            _place = _balloon.anchoredPosition;
            _group.alpha = 0f;
        }

        private void Start()
        {
            if (_settings.PopSheets.Count == 0)
            {
                return;
            }

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

            var withAscent = ascentStartedAt + ascentSeconds * Mathf.Clamp01(settings.RibbonsGoneAtAscent);
            return Mathf.Max(withAscent, releasedAt + settings.RibbonMinimumSeconds);
        }

        /// <summary>
        ///     How far the torn rubber has fallen <paramref name="sincePop" /> seconds into the pop: nothing until its
        ///     fall's frame, then speeding up under the settings' gravity.
        /// </summary>
        internal static float FallDistance(float sincePop, ICardRevealSettings settings)
        {
            var falling = sincePop - settings.RubberFallsFromFrame / settings.FramesPerSecond;
            return falling > 0f ? 0.5f * settings.RubberFallGravity * falling * falling : 0f;
        }

        private void OnReveal(RevealedCard reveal)
        {
            Stop();
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
                var startedAt = Time.unscaledTime;
                Dress(reveal);
                await AnimateAsync(_settings.RiseSeconds, Rise, token);
                await UniTask.Delay(Milliseconds(_settings.HoldBeforePopSeconds), true, cancellationToken: token);
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
            var elapsed = 0f;
            while (true)
            {
                // Past the last frame the rubber has fallen out of view: the layers stop drawing (and sampling).
                var frame = (int)(elapsed * _settings.FramesPerSecond);
                SetRubberShown(frame < frames);
                ShowFrame(Mathf.Min(frame, frames - 1));
                Fall(FallDistance(elapsed, _settings));
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
            _descriptionRibbon.Fly(now - _releasedAt, exit, _settings);
            return exit;
        }

        private void Dress(RevealedCard reveal)
        {
            var sheet = _settings.PopSheets[reveal.Pop % _settings.PopSheets.Count];
            _back.texture = sheet;
            _front.texture = sheet;
            _shadow.texture = sheet;

            var tint = TintForTheLevel();
            _back.color = tint;
            _shadow.color = tint;
            tint.a = 1f - _settings.InsideShowThrough;
            _front.color = tint;

            _titleRibbon.Show(reveal.Card.Title);
            _descriptionRibbon.Show(reveal.Card.Description);
            _titleRibbon.Rest();
            _descriptionRibbon.Rest();
            SetRubberShown(true);
            ShowFrame(0);
            Rise(0f);
            _group.alpha = 1f;
        }

        // One of the colours the level's balloons come in, so the prize balloon looks like one of them. Kept, so
        // its tear flashes that colour.
        private Color TintForTheLevel()
        {
            var colors = _level.Current.AllowedColors;
            if (colors == null || colors.Count == 0)
            {
                _colorId = null;
                return Color.white;
            }

            _colorId = colors[UnityEngine.Random.Range(0, colors.Count)];
            var entry = _palette.GetEntry(_colorId);
            return entry != null ? entry.Color : Color.white;
        }

        // Once, when the rubber tears: a flash of the balloon's colour in the light field, which lights the board
        // around it and the reveal itself, and a pop's stir in the disturbance field, as a board balloon's pop.
        private void BurstOnTear(int frame)
        {
            if (_burst || frame < _settings.RubberFallsFromFrame)
            {
                return;
            }

            _burst = true;
            var at = _balloon.position;
            var paletteIndex = _palette.PaletteIndexOf(_colorId);
            _lightField.Flash(at, _settings.PopFlashRadius, _settings.PopFlashIntensity, _settings.PopFlashSeconds,
                paletteIndex);
            _disturbanceField.Stamp(StampSource.BalloonPop, at, Vector2.zero, paletteIndex);
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

        // The layers part by how far the balloon still is from its place, the front leading, and meet there.
        private void Rise(float t)
        {
            var eased = DOVirtual.EasedValue(0f, 1f, t, Ease.OutCubic);
            var offset = Vector2.down * (_riseDistance * (1f - eased));
            _balloon.anchoredPosition = _place + offset;
            var part = offset * _settings.Parallax;
            PlaceLayers(part, -part);
        }

        // Only the rubber falls: the ribbons are the balloon's too, and fly their own way.
        private void Fall(float drop)
        {
            var down = Vector2.down * drop;
            PlaceLayers(down, down);
        }

        private void PlaceLayers(Vector2 front, Vector2 back)
        {
            _front.rectTransform.anchoredPosition = front;
            _back.rectTransform.anchoredPosition = back;
            _shadow.rectTransform.anchoredPosition = back;
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

        private static int Milliseconds(float seconds)
        {
            return Mathf.RoundToInt(seconds * 1000f);
        }
    }
}
