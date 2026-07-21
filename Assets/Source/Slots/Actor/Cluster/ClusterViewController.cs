using System;
using System.Collections.Generic;
using BalloonParty.Slots.Grid;
using BalloonParty.Shared.Diagnostics;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Slots.Actor.Cluster
{
    /// <summary>
    /// Manages a single <typeparamref name="TView"/> rendering all clusters of <typeparamref name="TModel"/> actors.
    /// </summary>
    internal abstract class ClusterViewController<TModel, TView, TSettings>
        : IStartable, IDisposable, ITransitionOutgoingContent
        where TModel : class, IClusterableSlotActor
        where TView : ClusterView
        where TSettings : class, IClusterViewSettings
    {
        private readonly SlotClusterRegistry<TModel> _registry;
        private readonly SlotGrid _grid;
        private readonly TSettings _settings;
        private readonly IObjectResolver _resolver;
        private readonly ScenarioContentRoot _scenarioRoot;
        private readonly CompositeDisposable _disposables = new();

        private readonly Vector4[] _positionsBuffer = new Vector4[16];

        private TView _view;
        private TView _snapshotView;

        [Inject]
        protected ClusterViewController(
            SlotClusterRegistry<TModel> registry,
            SlotGrid grid,
            TSettings settings,
            IObjectResolver resolver,
            ScenarioContentRoot scenarioRoot)
        {
            _registry = registry;
            _grid = grid;
            _settings = settings;
            _resolver = resolver;
            _scenarioRoot = scenarioRoot;
        }

        protected abstract TView GetPrefab(TSettings settings);

        public void Start()
        {
            var prefab = GetPrefab(_settings);
            if (prefab == null)
            {
                Log.Error("ClusterView",
                    $"{GetType().Name}: Prefab is not assigned on {typeof(TSettings).Name}. " +
                    "Cluster views will not spawn.");
                return;
            }

            _view = _resolver.Instantiate(prefab);

            // Parented under the scenario root so the level-transition Ascent moves this cluster with everything else.
            _view.transform.SetParent(_scenarioRoot.Transform, worldPositionStays: false);

            if (_view.Renderer != null)
            {
                _view.Renderer.sortingLayerID = _settings.SortingLayerId;
                _view.Renderer.sortingOrder = _settings.SortingOrderOffset;
                _view.Renderer.enabled = false;
            }

            OnViewCreated(_view);

            _registry.OnClusterChanged
                .Subscribe(_ => Reconfigure())
                .AddTo(_disposables);
        }

        public void Dispose()
        {
            _disposables.Dispose();
            ReleaseOutgoing();

            if (_view != null)
            {
                UnityEngine.Object.Destroy(_view.gameObject);
                _view = null;
            }
        }

        // Must run before the board clears — freezes the current cluster shape into a throwaway snapshot view.
        public void HoldOutgoing(Transform outgoingRoot, float exitDrop)
        {
            ReleaseOutgoing();

            var prefab = GetPrefab(_settings);
            if (prefab == null)
            {
                return;
            }

            var snapshot = _resolver.Instantiate(prefab);
            if (snapshot.Renderer != null)
            {
                snapshot.Renderer.sortingLayerID = _settings.SortingLayerId;
                snapshot.Renderer.sortingOrder = _settings.SortingOrderOffset;
                snapshot.Renderer.enabled = false;
            }

            OnViewCreated(snapshot);

            if (!ConfigureView(snapshot))
            {
                UnityEngine.Object.Destroy(snapshot.gameObject);
                return;
            }

            // Offset below the incoming content so it exits the bottom as the new scenario arrives.
            snapshot.transform.SetParent(outgoingRoot, worldPositionStays: true);
            var local = snapshot.transform.localPosition;
            local.y -= exitDrop;
            snapshot.transform.localPosition = local;

            _snapshotView = snapshot;
        }

        public void ReleaseOutgoing()
        {
            if (_snapshotView != null)
            {
                UnityEngine.Object.Destroy(_snapshotView.gameObject);
                _snapshotView = null;
            }
        }

        /// <summary>
        /// Override to wire subclass-specific dependencies.
        /// </summary>
        protected virtual void OnViewCreated(TView view)
        {
        }

        /// <summary>
        /// Override to inject additional positions (gap fills, etc.).
        /// </summary>
        protected virtual int PopulatePositions(
            Vector4[] buffer,
            IReadOnlyDictionary<int, SlotCluster> clusters,
            SlotGrid grid)
        {
            var count = 0;
            foreach (var cluster in clusters.Values)
            {
                var seed = (cluster.ClusterId * 0.7123f) % 1f;
                foreach (var slot in cluster.Slots)
                {
                    if (count >= buffer.Length)
                    {
                        break;
                    }

                    var pos = grid.IndexToWorldPosition(slot);
                    buffer[count++] = new Vector4(pos.x, pos.y, seed, 1f);
                }
            }

            return count;
        }

        private void Reconfigure()
        {
            if (_view != null)
            {
                ConfigureView(_view);
            }
        }

        // Shared by the live view's Reconfigure and the transition snapshot.
        private bool ConfigureView(TView view)
        {
            var clusters = _registry.Clusters;
            if (clusters.Count == 0)
            {
                view.Clear();
                return false;
            }

            var count = PopulatePositions(_positionsBuffer, clusters, _grid);
            if (count == 0)
            {
                view.Clear();
                return false;
            }

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < count; i++)
            {
                min.x = Mathf.Min(min.x, _positionsBuffer[i].x);
                min.y = Mathf.Min(min.y, _positionsBuffer[i].y);
                max.x = Mathf.Max(max.x, _positionsBuffer[i].x);
                max.y = Mathf.Max(max.y, _positionsBuffer[i].y);
            }

            const float halfSlotPadding = 0.5f;
            min -= Vector2.one * halfSlotPadding;
            max += Vector2.one * halfSlotPadding;
            var combinedBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

            view.Configure(_positionsBuffer, count, combinedBounds, _settings);
            return true;
        }
    }
}
