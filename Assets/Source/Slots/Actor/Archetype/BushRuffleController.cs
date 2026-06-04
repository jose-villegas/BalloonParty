using System;
using BalloonParty.Configuration;
using BalloonParty.Projectile.View;
using BalloonParty.Shared.Messages;
using DG.Tweening;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Detects projectile proximity to bush clusters and drives per-leaf
    /// ruffle animation via DOTween. Also manages ambient wind oscillation.
    /// Registered as an <see cref="ITickable"/> entry point.
    /// </summary>
    internal class BushRuffleController : IStartable, ITickable, IDisposable
    {
        private const string RuffleIdPrefix = "BushRuffle_";

        private readonly IBushSettings _settings;
        private readonly BushViewController _bushViewController;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly CompositeDisposable _disposables = new();

        private Transform _projectileTransform;
        private int _windLeafCount;

        [Inject]
        internal BushRuffleController(
            IBushSettings settings,
            BushViewController bushViewController,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber)
        {
            _settings = settings;
            _bushViewController = bushViewController;
            _loadedSubscriber = loadedSubscriber;
            _destroyedSubscriber = destroyedSubscriber;
        }

        void IStartable.Start()
        {
            _loadedSubscriber
                .Subscribe(OnProjectileLoaded)
                .AddTo(_disposables);

            _destroyedSubscriber
                .Subscribe(_ => _projectileTransform = null)
                .AddTo(_disposables);
        }

        void ITickable.Tick()
        {
            var view = _bushViewController.View;
            if (view == null)
            {
                return;
            }

            var leaves = view.LeafSprites;
            if (leaves.Count > _windLeafCount)
            {
                for (var i = _windLeafCount; i < leaves.Count; i++)
                {
                    StartWindOnLeaf(leaves[i]);
                }

                _windLeafCount = leaves.Count;
            }
            else if (leaves.Count < _windLeafCount)
            {
                _windLeafCount = leaves.Count;
            }

            if (_projectileTransform == null)
            {
                return;
            }

            CheckProximity(view, _projectileTransform.position);
        }

        void IDisposable.Dispose()
        {
            _disposables.Dispose();
        }

        private void OnProjectileLoaded(ProjectileLoadedMessage msg)
        {
            // The ProjectileView publishes this message — find its transform
            var projectiles = UnityEngine.Object.FindObjectsByType<ProjectileView>(
                FindObjectsSortMode.None);

            foreach (var pv in projectiles)
            {
                if (pv.isActiveAndEnabled)
                {
                    _projectileTransform = pv.transform;
                    return;
                }
            }
        }

        private void CheckProximity(BushView view, Vector3 projectilePos)
        {
            var leaves = view.LeafSprites;
            var ruffleRadius = _settings.RuffleRadius;
            var ruffleRadiusSq = ruffleRadius * ruffleRadius;

            for (var i = 0; i < leaves.Count; i++)
            {
                var leaf = leaves[i];
                if (leaf == null)
                {
                    continue;
                }

                var leafPos = leaf.transform.position;
                var dx = leafPos.x - projectilePos.x;
                var dy = leafPos.y - projectilePos.y;
                var distSq = dx * dx + dy * dy;

                if (distSq > ruffleRadiusSq)
                {
                    continue;
                }

                var dist = Mathf.Sqrt(distSq);
                var intensity = 1f - dist / ruffleRadius;
                var direction = new Vector2(dx, dy).normalized;
                var delay = dist * _settings.RuffleStaggerPerUnit;

                RuffleLeaf(leaf, intensity * leaf.DepthFactor, direction, delay);
            }
        }

        private void RuffleLeaf(
            LeafSpriteView leaf, float amplitude, Vector2 direction, float delay)
        {
            var t = leaf.transform;
            var ruffleId = RuffleIdPrefix + t.GetInstanceID();

            if (DOTween.IsTweening(ruffleId))
            {
                return;
            }

            var duration = _settings.RuffleDuration;

            t.DOPunchRotation(
                    new Vector3(0f, 0f, _settings.RuffleRotationAmplitude * amplitude),
                    duration, 6)
                .SetDelay(delay)
                .SetEase(Ease.OutElastic)
                .SetId(ruffleId);

            t.DOPunchScale(
                    Vector3.one * (_settings.RuffleScaleAmplitude * amplitude),
                    duration * 0.8f, 4)
                .SetDelay(delay)
                .SetId(ruffleId);

            var offset = (Vector3)(direction * (_settings.RufflePositionAmplitude * amplitude));
            t.DOPunchPosition(offset, duration * 0.9f, 5)
                .SetDelay(delay)
                .SetId(ruffleId);
        }

        private void StartWindOnLeaf(LeafSpriteView leaf)
        {
            var phase = leaf.PhyllotaxisIndex * 0.4f;
            var period = _settings.WindPeriod + leaf.PhyllotaxisIndex * 0.1f;
            var amplitude = _settings.WindAmplitude;
            var baseRotation = leaf.BaseRotation;
            var t = leaf.transform;

            DOTween.To(
                    () => 0f,
                    angle => t.localRotation = Quaternion.Euler(0f, 0f, baseRotation + angle),
                    amplitude,
                    period)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetDelay(phase)
                .SetId(t);
        }
    }
}

