using System;
using System.Collections.Generic;
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
    /// Subclasses must implement <see cref="GetPrefab"/> to provide the typed prefab
    /// and may override <see cref="PopulatePositions"/> to inject extra positions
    /// (e.g. gap-fill circles between adjacent slots).
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
                Debug.LogError(
                    $"{GetType().Name}: Prefab is not assigned on {typeof(TSettings).Name}. " +
                    "Cluster views will not spawn.");
                return;
            }

            _view = _resolver.Instantiate(prefab);

            // Parent under the scenario root (at the origin during play) so the level-transition
            // Ascent moves this cluster with everything else; the view renders relative to its
            // transform, so a moved root slides it without touching its per-cluster shape.
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

        // Freezes the current cluster shape into a second, throwaway view left at rest (NOT parented
        // under the scenario root, so it doesn't ride the descent). Called before the level transition
        // clears the board, so the outgoing clusters stay put while the live view slides the incoming
        // ones down over them. Reads the still-populated registry, so it must run before the clear.
        public void HoldOutgoing(float exitDrop)
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

            // Ride the scenario root but offset one exitDrop BELOW the incoming content, so as the root
            // descends (lifting the incoming content from +exitDrop down to rest) this snapshot slides
            // from rest down to -exitDrop — the outgoing scenario exits the bottom as the new arrives.
            snapshot.transform.SetParent(_scenarioRoot.Transform, worldPositionStays: true);
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
        /// Called once after the view is instantiated and its renderer is
        /// configured. Override to wire subclass-specific dependencies.
        /// </summary>
        protected virtual void OnViewCreated(TView view)
        {
        }

        /// <summary>
        /// Fills <paramref name="buffer"/> with <c>(x, y, seed, radiusScale)</c>
        /// entries for every position the cluster renderer should cover.
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

        // Configures a view from the CURRENT clusters. Returns false (and clears the view) when there
        // are no clusters to draw. Shared by the live view's Reconfigure and the transition snapshot.
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
