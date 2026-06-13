using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Rendering;
using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.Item.Paint
{
    /// <summary>
    ///     Computed per-frame state for a paint blob in flight.
    ///     Produced by <see cref="PaintSplashView.ComputeBlobFlight" />.
    /// </summary>
    internal struct BlobFlightSnapshot
    {
        public Vector3 Position;
        public float Scale;
        public float ShadowScale;
        public float SpriteScale;
    }

    /// <summary>
    ///     Poolable paint-splash effect. Extends <see cref="EffectView" /> so it
    ///     participates in the standard effect-pool pipeline via <see cref="EffectPoolChannel" />.
    ///     The prefab holds pre-placed <see cref="ColorableRenderer" /> children (one per
    ///     potential neighbor — 6 for a hex grid) with the PaintBlob shader material.
    ///     Splash particles are spawned as independent pooled instances via
    ///     <see cref="ParticlePoolChannel" /> so they outlive the view's pool return.
    ///     Call <see cref="PrepareDisplay" /> with target data before <see cref="Play" />.
    /// </summary>
    public class PaintSplashView : EffectView
    {
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");
        private static readonly int ShadowScaleId = Shader.PropertyToID("_ShadowScale");
        private static readonly int SpriteScaleId = Shader.PropertyToID("_SpriteScale");

        /// <summary>
        ///     Pure math — computes blob position, scale, and MPB values for a given
        ///     progress along the flight arc. Shared by runtime and editor preview.
        /// </summary>
        internal static BlobFlightSnapshot ComputeBlobFlight(
            float progress,
            Vector3 from,
            Vector3 to,
            ItemSettings settings)
        {
            var pos = Vector3.Lerp(from, to, progress);
            pos.y += settings.PaintBlobArcCurve.Evaluate(progress);

            return new BlobFlightSnapshot
            {
                Position = pos,
                Scale = settings.PaintBlobScaleCurve.Evaluate(progress),
                ShadowScale = settings.PaintBlobShadowScaleCurve?.Evaluate(progress) ?? 1f,
                SpriteScale = settings.PaintBlobSpriteScaleCurve?.Evaluate(progress) ?? 1f
            };
        }

        /// <summary>
        ///     Applies <see cref="BlobFlightSnapshot" /> MPB values plus a time offset
        ///     to a renderer. Shared by runtime and editor preview.
        /// </summary>
        internal static void ApplyBlobMaterial(
            Renderer renderer,
            float timeOffset,
            BlobFlightSnapshot snapshot)
        {
            // Reused scratch block — SetPropertyBlock copies the values out immediately,
            // so one shared instance avoids a per-blob, per-frame allocation.
            _blobBlock ??= new MaterialPropertyBlock();
            _blobBlock.SetFloat(TimeOffsetId, timeOffset);
            _blobBlock.SetFloat(ShadowScaleId, snapshot.ShadowScale);
            _blobBlock.SetFloat(SpriteScaleId, snapshot.SpriteScale);
            renderer.SetPropertyBlock(_blobBlock);
        }

        [Header("Blobs")]
        [Tooltip("Pre-placed blob ColorableRenderers — one per possible neighbor (6 for hex grid).")]
        [SerializeField] private ColorableRenderer[] _blobRenderers;

        [Header("Splash")]
        [Tooltip("Particle prefab spawned at each blob's target on arrival. Pooled independently.")]
        [SerializeField] private ParticleSystem _splashParticlePrefab;

        private static MaterialPropertyBlock _blobBlock;

        private List<BlobFlight> _activeFlights;
        private Color _color;
        private Action<int> _onTargetHit;
        private bool _playing;
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

        public override void OnSpawned()
        {
            base.OnSpawned();
            HideAllBlobs();
        }

        public override void OnDespawned()
        {
            _playing = false;
            _activeFlights = null;
            HideAllBlobs();
            base.OnDespawned();
        }

        /// <summary>
        ///     Sets up the paint blob flights before calling <see cref="Play" />.
        ///     Each entry is a (source, target) pair. <paramref name="onTargetHit" /> is
        ///     invoked per blob arrival with the target index — use it to change the
        ///     balloon's color at exactly the right moment.
        /// </summary>
        internal void PrepareDisplay(
            IReadOnlyList<(Vector3 from, Vector3 to)> flights,
            ItemSettings settings,
            PoolManager poolManager,
            Action<int> onTargetHit)
        {
            _settings = settings;
            _poolManager = poolManager;
            _onTargetHit = onTargetHit;

            var blobCount = _blobRenderers != null ? _blobRenderers.Length : 0;

            _activeFlights = new List<BlobFlight>();

            for (var i = 0; i < flights.Count; i++)
            {
                var blob = i < blobCount ? _blobRenderers[i] : null;

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

        /// <summary>
        ///     Starts all paint-blob arcs in parallel.
        ///     <paramref name="tint" /> sets the PaintBlob shader color and splash particle
        ///     start color. <paramref name="onComplete" /> fires after every blob has landed.
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
                blob.transform.localScale = Vector3.one * _settings.PaintBlobScaleCurve.Evaluate(0f);
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
            var flightDuration = Mathf.Max(_settings.PaintBlobFlightDuration, 0.01f);

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
                    ApplyBlobMaterial(flight.BlobRenderer, flight.TimeOffset, snapshot);
                }

                flight.Blob.transform.Rotate(0f, 0f, -_settings.PaintBlobSpinSpeed * delta);
            }

            if (allLanded)
            {
                _playing = false;
                InvokeComplete();
            }
        }

        private void HideAllBlobs()
        {
            if (_blobRenderers == null)
            {
                return;
            }

            foreach (var blob in _blobRenderers)
            {
                if (blob != null)
                {
                    blob.gameObject.SetActive(false);
                }
            }
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
