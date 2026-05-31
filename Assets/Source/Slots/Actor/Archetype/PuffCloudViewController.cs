using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Manages a single <see cref="PuffCloudView"/> instance that renders all
    /// Puff clusters in one draw call. On any cluster change, collects every
    /// slot position from every cluster and reconfigures the view.
    /// </summary>
    internal class PuffCloudViewController : IStartable, IDisposable
    {
        private readonly PuffClusterRegistry _registry;
        private readonly SlotGrid _grid;
        private readonly PuffCloudSettings _settings;
        private readonly IObjectResolver _resolver;
        private readonly CompositeDisposable _disposables = new();

        private PuffCloudView _view;

        [Inject]
        internal PuffCloudViewController(
            PuffClusterRegistry registry,
            SlotGrid grid,
            PuffCloudSettings settings,
            IObjectResolver resolver)
        {
            _registry = registry;
            _grid = grid;
            _settings = settings;
            _resolver = resolver;
        }

        public void Start()
        {
            if (_settings.CloudPrefab == null)
            {
                Debug.LogError(
                    "PuffCloudViewController: CloudPrefab is not assigned on PuffCloudSettings. " +
                    "Cloud views will not spawn.");
                return;
            }

            _view = _resolver.Instantiate(_settings.CloudPrefab);

            if (_view.Renderer != null)
            {
                _view.Renderer.sortingLayerID = _settings.SortingLayerId;
                _view.Renderer.sortingOrder = _settings.SortingOrderOffset;
                _view.Renderer.enabled = false;
            }

            _registry.OnClusterChanged
                .Subscribe(_ => Reconfigure())
                .AddTo(_disposables);
        }

        public void Dispose()
        {
            _disposables.Dispose();

            if (_view != null)
            {
                UnityEngine.Object.Destroy(_view.gameObject);
                _view = null;
            }
        }

        private void Reconfigure()
        {
            if (_view == null)
            {
                return;
            }

            var clusters = _registry.Clusters;
            if (clusters.Count == 0)
            {
                _view.Clear();
                return;
            }

            var allPositions = new List<Vector4>();
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            foreach (var cluster in clusters.Values)
            {
                // Deterministic seed from cluster ID — gives each cluster a unique noise pattern
                var seed = (cluster.ClusterId * 0.7123f) % 1f;

                foreach (var slot in cluster.Slots)
                {
                    var pos = _grid.IndexToWorldPosition(slot);
                    allPositions.Add(new Vector4(pos.x, pos.y, seed, 0f));
                    min.x = Mathf.Min(min.x, pos.x);
                    min.y = Mathf.Min(min.y, pos.y);
                    max.x = Mathf.Max(max.x, pos.x);
                    max.y = Mathf.Max(max.y, pos.y);
                }
            }

            const float halfSlotPadding = 0.5f;
            min -= Vector2.one * halfSlotPadding;
            max += Vector2.one * halfSlotPadding;
            var combinedBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

            _view.Configure(allPositions.ToArray(), combinedBounds, _settings);
        }
    }
}
