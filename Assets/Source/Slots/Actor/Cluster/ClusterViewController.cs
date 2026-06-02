using System;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Slots.Actor.Cluster
{
    /// <summary>
    /// Generic controller that manages a single <typeparamref name="TView"/>
    /// instance rendering all clusters of <typeparamref name="TModel"/> actors.
    /// On any cluster change, collects every slot position across every cluster
    /// and reconfigures the view in one call.
    /// Subclasses must implement <see cref="GetPrefab"/> to provide the typed prefab.
    /// </summary>
    internal abstract class ClusterViewController<TModel, TView, TSettings> : IStartable, IDisposable
        where TModel : class, IClusterableSlotActor
        where TView : ClusterView
        where TSettings : class, IClusterViewSettings
    {
        private readonly SlotClusterRegistry<TModel> _registry;
        private readonly SlotGrid _grid;
        private readonly TSettings _settings;
        private readonly IObjectResolver _resolver;
        private readonly CompositeDisposable _disposables = new();

        private readonly Vector4[] _positionsBuffer = new Vector4[16];

        private TView _view;

        [Inject]
        protected ClusterViewController(
            SlotClusterRegistry<TModel> registry,
            SlotGrid grid,
            TSettings settings,
            IObjectResolver resolver)
        {
            _registry = registry;
            _grid = grid;
            _settings = settings;
            _resolver = resolver;
        }

        protected abstract TView GetPrefab(TSettings settings);

        public void Start()
        {
            var prefab = GetPrefab(_settings);
            if (prefab == null)
            {
                Debug.LogError(
                    $"{GetType().Name}: Prefab is not assigned on {typeof(TSettings).Name}. " +
                    "Cluster views will not spawn.");
                return;
            }

            _view = _resolver.Instantiate(prefab);

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

