using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Manages the lifecycle of <see cref="PuffCloudView"/> instances based on
    /// cluster events from <see cref="PuffClusterRegistry"/>. One pooled cloud
    /// view per cluster.
    /// </summary>
    internal class PuffCloudViewController : IStartable, IDisposable
    {
        internal const string PoolKey = "PuffCloud";

        private readonly PuffClusterRegistry _registry;
        private readonly SlotGrid _grid;
        private readonly PuffCloudSettings _settings;
        private readonly PoolManager _poolManager;
        private readonly IObjectResolver _resolver;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly Dictionary<int, PuffCloudView> _activeViews = new();
        private readonly CompositeDisposable _disposables = new();

        [Inject]
        internal PuffCloudViewController(
            PuffClusterRegistry registry,
            SlotGrid grid,
            PuffCloudSettings settings,
            PoolManager poolManager,
            IObjectResolver resolver,
            DisturbanceFieldService disturbanceField)
        {
            _registry = registry;
            _grid = grid;
            _settings = settings;
            _poolManager = poolManager;
            _resolver = resolver;
            _disturbanceField = disturbanceField;
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

            _poolManager.Register(PoolKey, new PuffCloudPoolChannel(_resolver, _settings.CloudPrefab));

            _registry.OnClusterChanged
                .Subscribe(OnClusterChanged)
                .AddTo(_disposables);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }

        private void OnClusterChanged(PuffClusterChangedEvent evt)
        {
            switch (evt.ChangeType)
            {
                case PuffClusterChangeType.Created:
                    SpawnView(evt.Cluster);
                    break;

                case PuffClusterChangeType.Resized:
                    ReconfigureView(evt.Cluster);
                    break;

                case PuffClusterChangeType.Removed:
                    ReturnView(evt.ClusterId);
                    break;
            }
        }

        private void SpawnView(PuffCluster cluster)
        {
            var view = _poolManager.Get<PuffCloudView>(PoolKey);
            view.SetDisturbanceField(_disturbanceField);
            ConfigureView(view, cluster);
            _activeViews[cluster.ClusterId] = view;
        }

        private void ReconfigureView(PuffCluster cluster)
        {
            if (!_activeViews.TryGetValue(cluster.ClusterId, out var view))
            {
                SpawnView(cluster);
                return;
            }

            ConfigureView(view, cluster);
        }

        private void ReturnView(int clusterId)
        {
            if (!_activeViews.TryGetValue(clusterId, out var view))
            {
                return;
            }

            _activeViews.Remove(clusterId);
            _poolManager.Return(PoolKey, view);
        }

        private void ConfigureView(PuffCloudView view, PuffCluster cluster)
        {
            var slots = cluster.Slots;
            var positions = new Vector3[slots.Count];
            for (var i = 0; i < slots.Count; i++)
            {
                positions[i] = _grid.IndexToWorldPosition(slots[i]);
            }

            view.Configure(positions, cluster.WorldBounds, _settings);

            if (view.Renderer != null)
            {
                view.Renderer.sortingLayerID = _settings.SortingLayerId;
                view.Renderer.sortingOrder = _settings.SortingOrderOffset;
            }
        }
    }
}
