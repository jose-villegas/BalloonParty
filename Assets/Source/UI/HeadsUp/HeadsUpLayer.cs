using System;
using System.Collections.Generic;
using System.Globalization;
using BalloonParty.Configuration.HeadsUp;
using BalloonParty.Game.HeadsUp;
using BalloonParty.Shared.Pool;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.HeadsUp
{
    /// <summary>
    ///     Draws the heads-ups <see cref="HeadsUpService" /> lets through, as pooled <see cref="HandwrittenHeadsUp" />
    ///     lines cloned from <see cref="_linePrefab" />. A line written where something happened is packed clear of
    ///     the lines still showing and inside the view (<see cref="HeadsUpPacking" />); one at a HUD endpoint (the
    ///     heart, a colour's bar, the cannon) follows it every frame. A line that can't be placed (its endpoint not
    ///     registered yet, no camera) reports itself finished at once, so its slot isn't held forever.
    /// </summary>
    internal sealed class HeadsUpLayer : MonoBehaviour, IHeadsUpPresenter
    {
        // Numbers past this are formatted on each write instead of cached: no style counts that high in play.
        private const int CachedArgs = 32;

        [Tooltip("The line every heads-up is cloned from; kept inactive.")]
        [SerializeField] private HandwrittenHeadsUp _linePrefab;

        [Tooltip("Clear space kept around each line when packing, as a share of the line's height.")]
        [SerializeField] [Min(0f)] private float _packingGap = 0.15f;

        [Inject] private IHeadsUpSettings _settings;
        [Inject] private TrailEndpointRegistry _endpoints;

        private readonly List<HandwrittenHeadsUp> _lines = new();
        private readonly List<Rect> _occupied = new();
        private readonly Subject<HeadsUpHandle> _finished = new();

        // Per style, its text for each small number, so a burst of the same heads-up formats once.
        private string[][] _texts;
        private SimplePoolChannel<HandwrittenHeadsUp> _pool;
        private Camera _camera;

        public IObservable<HeadsUpHandle> Finished => _finished;

        private void OnDestroy()
        {
            _finished.Dispose();
        }

        [Inject]
        internal void Construct()
        {
            _texts = new string[HeadsUpStyleIds.Count][];
            _pool = new SimplePoolChannel<HandwrittenHeadsUp>(_linePrefab);
            _pool.SetParent(transform);
            // Released and evicted lines fade beside the ones that replaced them, and held ones sit outside the cap.
            _pool.Prewarm(2 * Math.Max(1, _settings.MaxConcurrent) + 2);
        }

        public void Show(HeadsUpHandle handle, in HeadsUpRequest request, HeadsUpStyleSettings style)
        {
            if (!TryAnchor(request.Anchor, out var anchor) || !TryCamera(out var gameCamera))
            {
                _finished.OnNext(handle);
                return;
            }

            var line = _pool.Get();
            line.Show(handle, Text(style, request), anchor + style.WorldOffset, style, request.Arg,
                request.Anchor.EndpointKey);
            if (!request.Anchor.IsEndpoint)
            {
                Pack(line, HeadsUpPacking.ViewRect(gameCamera));
            }

            _lines.Add(line);
        }

        public void Rewrite(HeadsUpHandle handle, in HeadsUpRequest request, HeadsUpStyleSettings style)
        {
            var line = Find(handle);
            if (line != null)
            {
                line.Rewrite(Text(style, request), style, request.Arg);
            }
        }

        public void Release(HeadsUpHandle handle)
        {
            var line = Find(handle);
            if (line != null)
            {
                line.Release();
            }
        }

        public void ClearAll(bool animated)
        {
            for (var i = _lines.Count - 1; i >= 0; i--)
            {
                if (animated)
                {
                    // One already fading out keeps its own clock.
                    if (!_lines[i].Released)
                    {
                        _lines[i].Release();
                    }
                }
                else
                {
                    _pool.Return(_lines[i]);
                    _lines.RemoveAt(i);
                }
            }
        }

        public void Advance(float liveDelta, float unscaledDelta)
        {
            for (var i = _lines.Count - 1; i >= 0; i--)
            {
                var line = _lines[i];
                Follow(line);
                if (!line.Tick(liveDelta, unscaledDelta))
                {
                    continue;
                }

                var handle = line.Handle;
                var reports = !line.Released;
                _lines.RemoveAt(i);
                _pool.Return(line);
                if (reports)
                {
                    _finished.OnNext(handle);
                }
            }
        }

        // The showing line of a handle; a released one still fading under the same handle isn't it.
        private HandwrittenHeadsUp Find(HeadsUpHandle handle)
        {
            foreach (var line in _lines)
            {
                if (!line.Released && line.Handle == handle)
                {
                    return line;
                }
            }

            return null;
        }

        private bool TryAnchor(in HeadsUpAnchor anchor, out Vector2 position)
        {
            if (!anchor.IsEndpoint)
            {
                position = anchor.Position;
                return true;
            }

            if (_endpoints.TryGet(anchor.EndpointKey, out var endpoint))
            {
                position = endpoint.Center;
                return true;
            }

            position = default;
            return false;
        }

        private bool TryCamera(out Camera gameCamera)
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            gameCamera = _camera;
            return gameCamera != null;
        }

        // The heart and the bars sit on the camera's canvas, which moves with the camera rig's zoom and shake.
        private void Follow(HandwrittenHeadsUp line)
        {
            if (line.EndpointKey != null && _endpoints.TryGet(line.EndpointKey, out var endpoint))
            {
                line.MoveTo((Vector2)endpoint.Center + line.Offset);
            }
        }

        private string Text(HeadsUpStyleSettings style, in HeadsUpRequest request)
        {
            if (request.Word != null)
            {
                return string.Format(CultureInfo.InvariantCulture, style.Format, request.Word);
            }

            var arg = request.Arg;
            if (arg < 0 || arg >= CachedArgs)
            {
                return string.Format(CultureInfo.InvariantCulture, style.Format, arg);
            }

            var texts = _texts[(int)style.Id] ??= new string[CachedArgs];
            return texts[arg] ??= string.Format(CultureInfo.InvariantCulture, style.Format, arg);
        }

        // Written first so the ink's size is known, then moved: clear of the lines still showing where it can
        // be, and wholly on screen (walls sit at the screen's edge).
        private void Pack(HandwrittenHeadsUp line, Rect bounds)
        {
            _occupied.Clear();
            foreach (var other in _lines)
            {
                if (other != line)
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
