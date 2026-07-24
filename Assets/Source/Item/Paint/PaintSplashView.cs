using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Rendering;
using BalloonParty.Shared.Pool;
using UnityEngine;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Item.Paint
{
    /// <summary>
    ///     Computed per-frame state for a paint blob in flight.
    /// </summary>
    internal struct BlobFlightSnapshot
    {
        public Vector3 Position;
        public float Scale;
        public float ShadowScale;
        public float SpriteScale;
    }

    /// <summary>
    ///     Poolable paint-splash effect. Call <see cref="ISplashEffect.PrepareDisplay" /> before <see cref="Play" />.
    /// </summary>
    public class PaintSplashView : EffectView, ISplashEffect
    {
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");
        private static readonly int ShadowScaleId = Shader.PropertyToID("_ShadowScale");
        private static readonly int SpriteScaleId = Shader.PropertyToID("_SpriteScale");
        private static readonly int RainbowEnabledId = Shader.PropertyToID("_RainbowEnabled");
        private static readonly int RainbowScrollSpeedId = Shader.PropertyToID("_RainbowScrollSpeed");

        [Header("Blobs")]
        [Tooltip("Seed blob ColorableRenderers. The pool clones the first entry when the packed splash " +
                 "needs more than are pre-placed, so one entry is enough.")]
        [SerializeField] private ColorableRenderer[] _blobRenderers;

        [Header("Splash")]
        [Tooltip("Particle prefab spawned at each blob's target on arrival. Pooled independently.")]
        [SerializeField] private ParticleSystem _splashParticlePrefab;

        private static MaterialPropertyBlock _blobBlock;

        private List<BlobFlight> _activeFlights;
        private List<ColorableRenderer> _blobPool;
        private Color _color;
        private Action<int> _onTargetHit;
        private bool _playing;
        private bool _rainbow;
        private PoolManager _poolManager;
        private ItemSettings _settings;

        private void Update()
        {
            if (!_playing)
            {
                return;
            }

            TickFlights(Time.deltaTime);
        }

        /// <summary>
        ///     Sets up the paint blob flights before calling <see cref="Play" />.
        /// </summary>
        void ISplashEffect.PrepareDisplay(
            IReadOnlyList<(Vector3 from, Vector3 to)> flights,
            ItemSettings settings,
            PoolManager poolManager,
            Action<int> onTargetHit)
        {
            _settings = settings;
            _poolManager = poolManager;
            _onTargetHit = onTargetHit;

            EnsureBlobPool(flights.Count);

            _activeFlights = new List<BlobFlight>();

            for (var i = 0; i < flights.Count; i++)
            {
                var blob = i < _blobPool.Count ? _blobPool[i] : null;

                if (blob == null)
                {
                    continue;
                }

                _activeFlights.Add(new BlobFlight
                {
                    From = flights[i].from,
                    To = flights[i].to,
                    Blob = blob,
                    BlobRenderer = blob.GetComponentInChildren<Renderer>(),
                    TimeOffset = UnityEngine.Random.Range(0f, 100f),
                    Index = i
                });
            }
        }

        void ISplashEffect.SetRainbow()
        {
            _rainbow = true;
        }

        public override void OnSpawned()
        {
            base.OnSpawned();
            HideAllBlobs();
        }

        public override void OnDespawned()
        {
            _playing = false;
            _rainbow = false;
            _activeFlights = null;
            HideAllBlobs();
            base.OnDespawned();
        }

        /// <summary>
        ///     Starts all paint-blob arcs in parallel; <paramref name="onComplete" /> fires once all land.
        /// </summary>
        public override void Play(Vector3 position, Color tint, Action onComplete = null)
        {
            OnComplete = onComplete;

            if (_activeFlights == null || _activeFlights.Count == 0)
            {
                InvokeComplete();
                return;
            }

            _color = tint;
            InitialiseBlobs(tint);
            _playing = true;
        }

        private void InitialiseBlobs(Color tint)
        {
            foreach (var flight in _activeFlights)
            {
                var blob = flight.Blob;
                blob.gameObject.SetActive(true);
                blob.transform.position = flight.From;
                blob.transform.localScale = Vector3.one * _settings.Paint.ScaleCurve.Evaluate(0f);
                blob.transform.rotation = Quaternion.identity;

                blob.SetColor(tint);

                if (flight.BlobRenderer is SpriteRenderer spriteRenderer)
                {
                    spriteRenderer.sortingOrder = flight.Index;
                }
            }
        }

        private void PlaySplash(Vector3 position)
        {
            if (_splashParticlePrefab == null || _poolManager == null)
            {
                return;
            }

            var key = _splashParticlePrefab.name;
            var splash = _poolManager.GetOrRegister(key,
                () => new ParticlePoolChannel(_splashParticlePrefab.gameObject));
            splash.Play(position, _color, () => _poolManager.Return(key, splash));
        }

        private void TickFlights(float delta)
        {
            var allLanded = true;
            var flightDuration = Mathf.Max(_settings.Paint.FlightDuration, 0.01f);

            foreach (var flight in _activeFlights)
            {
                if (flight.Landed)
                {
                    continue;
                }

                flight.Progress += delta / flightDuration;

                if (flight.Progress >= 1f)
                {
                    flight.Progress = 1f;
                    flight.Landed = true;
                    flight.Blob.gameObject.SetActive(false);

                    PlaySplash(flight.To);
                    _onTargetHit?.Invoke(flight.Index);
                    continue;
                }

                allLanded = false;

                var snapshot = ComputeBlobFlight(flight.Progress, flight.From, flight.To, _settings);
                flight.Blob.transform.position = snapshot.Position;
                flight.Blob.transform.localScale = Vector3.one * snapshot.Scale;

                if (flight.BlobRenderer != null)
                {
                    ApplyBlobMaterial(flight.BlobRenderer, flight.TimeOffset, snapshot,
                        _rainbow, _settings.Paint.RainbowScrollSpeed);
                }

                flight.Blob.transform.Rotate(0f, 0f, -_settings.Paint.SpinSpeed * delta);
            }

            if (allLanded)
            {
                _playing = false;
                InvokeComplete();
            }
        }

        // Grows by cloning the first seed blob; clones persist across plays since the view is pooled.
        private void EnsureBlobPool(int needed)
        {
            _blobPool ??= new List<ColorableRenderer>();

            if (_blobPool.Count == 0 && _blobRenderers != null)
            {
                foreach (var blob in _blobRenderers)
                {
                    if (blob != null)
                    {
                        _blobPool.Add(blob);
                    }
                }
            }

            if (_blobPool.Count == 0)
            {
                return;
            }

            var template = _blobPool[0];
            while (_blobPool.Count < needed)
            {
                var clone = Instantiate(template, template.transform.parent);
                clone.gameObject.SetActive(false);
                _blobPool.Add(clone);
            }
        }

        private void HideAllBlobs()
        {
            EnsureBlobPool(0);

            foreach (var blob in _blobPool)
            {
                if (blob != null)
                {
                    blob.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        ///     Pure math shared by runtime and editor preview.
        /// </summary>
        internal static BlobFlightSnapshot ComputeBlobFlight(
            float progress,
            Vector3 from,
            Vector3 to,
            ItemSettings settings)
        {
            var pos = Vector3.Lerp(from, to, progress);
            pos.y += settings.Paint.ArcCurve.Evaluate(progress);

            return new BlobFlightSnapshot
            {
                Position = pos,
                Scale = settings.Paint.ScaleCurve.Evaluate(progress),
                ShadowScale = settings.Paint.ShadowScaleCurve?.Evaluate(progress) ?? 1f,
                SpriteScale = settings.Paint.SpriteScaleCurve?.Evaluate(progress) ?? 1f
            };
        }

        /// <summary>
        ///     Applies <see cref="BlobFlightSnapshot" /> MPB values to a renderer. Shared with editor preview.
        /// </summary>
        internal static void ApplyBlobMaterial(
            Renderer renderer,
            float timeOffset,
            BlobFlightSnapshot snapshot,
            bool rainbow = false,
            float rainbowScrollSpeed = 0f)
        {
            // Reused scratch block — avoids a per-blob, per-frame allocation. SetPropertyBlock
            // replaces the whole block, so every property is written every frame. Rainbow blobs
            // scroll their rings faster than the held icon (which keeps the material's own speed).
            _blobBlock ??= new MaterialPropertyBlock();
            _blobBlock.SetFloat(TimeOffsetId, timeOffset);
            _blobBlock.SetFloat(ShadowScaleId, snapshot.ShadowScale);
            _blobBlock.SetFloat(SpriteScaleId, snapshot.SpriteScale);
            _blobBlock.SetFloat(RainbowEnabledId, rainbow ? 1f : 0f);
            _blobBlock.SetFloat(RainbowScrollSpeedId, rainbowScrollSpeed);
            renderer.SetPropertyBlock(_blobBlock);
        }

        private class BlobFlight
        {
            public ColorableRenderer Blob;
            public Renderer BlobRenderer;
            public Vector3 From;
            public int Index;
            public bool Landed;
            public float Progress;
            public float TimeOffset;
            public Vector3 To;
        }
    }
}
