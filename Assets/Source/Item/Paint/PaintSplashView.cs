using System;
using System.Collections.Generic;
using BalloonParty.Shared;
using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.Item.Paint
{
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

        [Header("Blobs")]
        [Tooltip("Pre-placed blob ColorableRenderers — one per possible neighbor (6 for hex grid).")]
        [SerializeField] private ColorableRenderer[] _blobRenderers;

        [Header("Splash")]
        [Tooltip("Particle prefab spawned at each blob's target on arrival. Pooled independently.")]
        [SerializeField] private ParticleSystem _splashParticlePrefab;

        [Header("Flight Curves")]
        [Tooltip("Scale multiplier over flight progress (0→1). Defaults to a sine bulge if left empty.")]
        [SerializeField] private AnimationCurve _scaleCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("Vertical offset multiplier over flight progress (0→1). Peaks at 0.5 for a parabolic arc.")]
        [SerializeField] private AnimationCurve _arcCurve = new(
            new Keyframe(0f, 0f, 0f, 4f),
            new Keyframe(0.5f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, -4f, 0f));

        private List<BlobFlight> _activeFlights;
        private float _arcHeight;
        private float _blobScale;
        private Color _color;
        private float _flightDuration;
        private Action<int> _onTargetHit;
        private bool _playing;
        private PoolManager _poolManager;

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
        public void PrepareDisplay(
            List<(Vector3 from, Vector3 to)> flights,
            float flightDuration,
            float arcHeight,
            float blobScale,
            PoolManager poolManager,
            Action<int> onTargetHit)
        {
            _flightDuration = Mathf.Max(flightDuration, 0.01f);
            _arcHeight = arcHeight;
            _blobScale = blobScale;
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
                blob.transform.localScale = Vector3.one * _blobScale;

                blob.SetColor(tint);

                var blobRenderer = blob.GetComponent<Renderer>();
                if (blobRenderer != null)
                {
                    var block = new MaterialPropertyBlock();
                    blobRenderer.GetPropertyBlock(block);
                    block.SetFloat(TimeOffsetId, UnityEngine.Random.Range(0f, 100f));
                    blobRenderer.SetPropertyBlock(block);
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

            foreach (var flight in _activeFlights)
            {
                if (flight.Landed)
                {
                    continue;
                }

                flight.Progress += delta / _flightDuration;

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

                flight.Blob.transform.position =
                    CurveUtility.LerpWithVerticalCurve(flight.From, flight.To, flight.Progress, _arcHeight, _arcCurve);

                flight.Blob.transform.localScale =
                    Vector3.one * CurveUtility.SampleMultiplied(flight.Progress, _blobScale, _scaleCurve);
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
            public Vector3 From;
            public int Index;
            public bool Landed;
            public float Progress;
            public Vector3 To;
        }
    }
}
