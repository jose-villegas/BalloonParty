using System;
using System.Threading;
using BalloonParty.Configuration.CardReveal;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.CardReveal;
using BalloonParty.Game.Level;
using BalloonParty.UI.HeadsUp;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BalloonParty.UI.CardReveal
{
    /// <summary>
    ///     The card reveal's stage (<see cref="ICardReveals" />): plays each reveal the controller hands it. A
    ///     balloon in one of the level's colours rises to its place with the slip inside it, pops (one of the baked
    ///     pops, its two rubber layers either side of the slip), the card's title is pencilled on the slip, and it
    ///     fades. The rubber is surface data lit live by <c>BalloonParty/Sprite/LitFlipbook</c>, so it takes the
    ///     scene's light. Attaches as the stage only when there are pops to play; until then the tooltip announces
    ///     the cards. Built by <c>Tools/BalloonParty/CardReveal/Build Card Reveal</c>.
    /// </summary>
    public class CardRevealView : MonoBehaviour
    {
        [Tooltip("Holds the whole reveal; faded in and out.")]
        [SerializeField] private CanvasGroup _group;

        [Tooltip("Moves as the balloon: the two rubber layers and the slip between them.")]
        [SerializeField] private RectTransform _balloon;

        [SerializeField] private RawImage _back;
        [SerializeField] private RawImage _front;
        [SerializeField] private WriteOnText _writeOn;

        [Tooltip("What the card does, written under the title once the title is written.")]
        [SerializeField] private WriteOnText _description;

        [Tooltip("How far below its place the balloon starts its rise, in canvas units.")]
        [SerializeField] private float _riseDistance = 260f;

        [Inject] private ICardReveals _cardReveals;
        [Inject] private ICardRevealSettings _settings;
        [Inject] private IGamePalette _palette;
        [Inject] private IActiveLevelParameters _level;

        private CancellationTokenSource _playing;
        private IDisposable _subscription;
        private Vector2 _place;
        private bool _attached;

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

            _cardReveals.AttachStage();
            _attached = true;
            _subscription = _cardReveals.Current.Subscribe(OnReveal);
        }

        private void OnDestroy()
        {
            // Unsubscribed first: detaching drops the queue, and the null that publishes must not reach a view
            // being torn down.
            _subscription?.Dispose();
            Stop();
            if (_attached)
            {
                _cardReveals.DetachStage();
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
            _writeOn.Stop();
            _description.Stop();
        }

        private async UniTaskVoid PlayAsync(RevealedCard reveal, CancellationToken token)
        {
            try
            {
                Dress(reveal);
                await AnimateAsync(_settings.RiseSeconds, Rise, token);
                await UniTask.Delay(Milliseconds(_settings.HoldBeforePopSeconds), true, cancellationToken: token);

                await PlayPopAsync(reveal, token);

                // The hold starts once the slip is written, so it is all there for as long as it is read.
                await UniTask.WaitWhile(() => _writeOn.IsWriting || _description.IsWriting, cancellationToken: token);
                await UniTask.Delay(Milliseconds(_settings.SlipHoldSeconds), true, cancellationToken: token);
                await AnimateAsync(_settings.FadeOutSeconds, t => _group.alpha = 1f - t, token);
                _cardReveals.Finish(reveal);
            }
            catch (OperationCanceledException)
            {
                // Stopped for a reset, a newer reveal or the scene closing; the controller already moved on.
            }
        }

        // The frame follows the time played rather than one delay per frame, which would round up to whole
        // screen refreshes and run slow (20 fps at 60 Hz). A frame the screen skips is skipped, not delayed.
        private async UniTask PlayPopAsync(RevealedCard reveal, CancellationToken token)
        {
            var frames = _settings.SheetColumns * _settings.SheetRows;
            var writeFrom = Mathf.Min(_settings.WriteFromFrame, frames - 1);
            var written = false;
            var elapsed = 0f;
            var frame = 0;
            while (frame < frames)
            {
                ShowFrame(frame);
                if (!written && frame >= writeFrom)
                {
                    _writeOn.Write(reveal.Card.Title);
                    _description.Write(reveal.Card.Description, _writeOn.Duration);
                    written = true;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.unscaledDeltaTime;
                frame = (int)(elapsed * _settings.FramesPerSecond);
            }
        }

        private void Dress(RevealedCard reveal)
        {
            var sheet = _settings.PopSheets[reveal.Pop % _settings.PopSheets.Count];
            _back.texture = sheet;
            _front.texture = sheet;

            var tint = TintForTheLevel();
            _back.color = tint;
            tint.a = 1f - _settings.SlipShowThrough;
            _front.color = tint;

            // A blank slip until the rubber opens: the last reveal's writing mustn't show through.
            _writeOn.Write(string.Empty);
            _description.Write(string.Empty);
            ShowFrame(0);
            Rise(0f);
            _group.alpha = 1f;
        }

        // One of the colours the level's balloons come in, so the prize balloon looks like one of them.
        private Color TintForTheLevel()
        {
            var colors = _level.Current.AllowedColors;
            if (colors == null || colors.Count == 0)
            {
                return Color.white;
            }

            var entry = _palette.GetEntry(colors[UnityEngine.Random.Range(0, colors.Count)]);
            return entry != null ? entry.Color : Color.white;
        }

        private void ShowFrame(int frame)
        {
            _front.uvRect = FrameRect(frame, true, _settings.SheetColumns, _settings.SheetRows);
            _back.uvRect = FrameRect(frame, false, _settings.SheetColumns, _settings.SheetRows);
        }

        // The layers part by how far the balloon still is from its place, the front leading, and meet there.
        private void Rise(float t)
        {
            var eased = DOVirtual.EasedValue(0f, 1f, t, Ease.OutCubic);
            var offset = Vector2.down * (_riseDistance * (1f - eased));
            _balloon.anchoredPosition = _place + offset;
            var part = offset * _settings.Parallax;
            _front.rectTransform.anchoredPosition = part;
            _back.rectTransform.anchoredPosition = -part;
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
