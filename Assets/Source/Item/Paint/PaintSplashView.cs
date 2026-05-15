using System;
using System.Collections.Generic;
using BalloonParty.Shared.Pool;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Item.Paint
{
    /// <summary>
    ///     Poolable paint-splash effect. Extends <see cref="EffectView" /> so it
    ///     participates in the standard effect-pool pipeline via <see cref="EffectPoolChannel" />.
    ///     The prefab holds pre-placed <see cref="SpriteRenderer" /> children (one per
    ///     potential neighbor — 6 for a hex grid) with the PaintBlob shader material,
    ///     plus an optional child <see cref="ParticleSystem" /> for the splash burst.
    ///     Call <see cref="PrepareDisplay" /> with target data before <see cref="Play" />.
    /// </summary>
    public class PaintSplashView : EffectView
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");

        [Header("Blobs")]
        [Tooltip("Pre-placed blob SpriteRenderers — one per possible neighbor (6 for hex grid).")]
        [SerializeField] private SpriteRenderer[] _blobRenderers;

        [Header("Splash")]
        [SerializeField] private ParticleSystem _splashParticle;

        private List<(Vector3 from, Vector3 to)> _flights;
        private float _flightDuration;
        private float _arcHeight;
        private float _blobScale;
        private Action<int> _onTargetHit;
        private Sequence _sequence;

        public override void OnSpawned()
        {
            base.OnSpawned();
            HideAllBlobs();

            if (_splashParticle != null)
            {
                _splashParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public override void OnDespawned()
        {
            _sequence?.Kill();
            _sequence = null;
            HideAllBlobs();

            if (_splashParticle != null)
            {
                _splashParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

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
            Action<int> onTargetHit)
        {
            _flights = flights;
            _flightDuration = Mathf.Max(flightDuration, 0.01f);
            _arcHeight = arcHeight;
            _blobScale = blobScale;
            _onTargetHit = onTargetHit;
        }

        /// <summary>
        ///     Starts all paint-blob arcs in parallel via DOTween.
        ///     <paramref name="tint" /> sets the PaintBlob shader color and splash particle
        ///     start color. <paramref name="onComplete" /> fires after every blob has landed.
        /// </summary>
        public override void Play(Vector3 position, Color tint, Action onComplete = null)
        {
            OnComplete = onComplete;

            if (_flights == null || _flights.Count == 0)
            {
                InvokeComplete();
                return;
            }

            ApplySplashColor(tint);
            BuildSequence(tint);
        }

        private void ApplySplashColor(Color tint)
        {
            if (_splashParticle != null)
            {
                var main = _splashParticle.main;
                main.startColor = tint;
            }
        }

        private void BuildSequence(Color tint)
        {
            _sequence?.Kill();

            var blobCount = _blobRenderers != null ? _blobRenderers.Length : 0;
            _sequence = DOTween.Sequence();

            for (var i = 0; i < _flights.Count; i++)
            {
                var index = i;
                var (from, to) = _flights[i];
                var blob = index < blobCount ? _blobRenderers[index] : null;

                if (blob == null)
                {
                    continue;
                }

                // Initialise blob
                blob.enabled = true;
                blob.transform.position = from;
                blob.transform.localScale = Vector3.one * _blobScale;

                var block = new MaterialPropertyBlock();
                blob.GetPropertyBlock(block);
                block.SetColor(ColorId, tint);
                block.SetFloat(TimeOffsetId, UnityEngine.Random.Range(0f, 100f));
                blob.SetPropertyBlock(block);

                // Arc tween: animate a 0→1 progress float, compute parabolic position each step
                var capturedFrom = from;
                var capturedTo = to;
                var capturedBlob = blob;
                var capturedIndex = index;
                var progress = 0f;

                var arcTween = DOTween.To(
                        () => progress,
                        t =>
                        {
                            progress = t;
                            var pos = Vector3.Lerp(capturedFrom, capturedTo, t);
                            pos.y += _arcHeight * 4f * t * (1f - t);
                            capturedBlob.transform.position = pos;

                            var scaleMultiplier = 1f + 0.3f * Mathf.Sin(t * Mathf.PI);
                            capturedBlob.transform.localScale = Vector3.one * (_blobScale * scaleMultiplier);
                        },
                        1f,
                        _flightDuration)
                    .SetEase(Ease.Linear)
                    .OnComplete(() =>
                    {
                        capturedBlob.enabled = false;

                        if (_splashParticle != null)
                        {
                            _splashParticle.transform.position = capturedTo;
                            _splashParticle.Play();
                        }

                        _onTargetHit?.Invoke(capturedIndex);
                    });

                // Join all arcs so they fly in parallel
                _sequence.Join(arcTween);
            }

            _sequence.OnComplete(InvokeComplete);
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
                    blob.enabled = false;
                }
            }
        }
    }
}
