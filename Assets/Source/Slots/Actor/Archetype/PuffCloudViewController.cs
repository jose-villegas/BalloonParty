using System;
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
        private readonly IPuffCloudSettings _settings;
        private readonly IObjectResolver _resolver;
        private readonly CompositeDisposable _disposables = new();

        // Shader cap is 16 slots; matches the array size in PuffCloudView.
        private readonly Vector4[] _positionsBuffer = new Vector4[16];

        private PuffCloudView _view;

        [Inject]
        internal PuffCloudViewController(
            PuffClusterRegistry registry,
            SlotGrid grid,
            IPuffCloudSettings settings,
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
                    "PuffCloudViewController: CloudPrefab is not assigned on IPuffCloudSettings. " +
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

            var count = 0;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            foreach (var cluster in clusters.Values)
            {
                var seed = (cluster.ClusterId * 0.7123f) % 1f;

                foreach (var slot in cluster.Slots)
                {
                    if (count >= _positionsBuffer.Length)
                    {
                        break;
                    }

                    var pos = _grid.IndexToWorldPosition(slot);
                    _positionsBuffer[count++] = new Vector4(pos.x, pos.y, seed, 0f);
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

            _view.Configure(_positionsBuffer, count, combinedBounds, _settings);
        }
    }
}
