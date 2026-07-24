using System;
using BalloonParty.Configuration;
using BalloonParty.Projectile;
using BalloonParty.Shared;
using BalloonParty.Shared.Rendering;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.SceneLight;
using UniRx;
using UnityEngine;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Item
{
    public class ItemDisplayService : MonoBehaviour
    {
        private readonly CompositeDisposable _disposables = new();

        private string _activePoolKey;
        private ItemVisualView _activeView;
        private ITransformCapture _activeCapture;
        private int _balloonRendererCount;
        private int _baseSortingOffset;
        private ISlotGridConfig _config;
        private IGamePalette _palette;
        private IItemConfiguration _itemConfig;
        private PoolManager _poolManager;
        private SceneLightFieldService _lightField;
        private IProjectileFacingSource _projectileFacing;
        private IReadOnlyReactiveProperty<Vector2Int> _slotIndex;
        private Action _onSortingFootprintChanged;

        internal ITransformCapture TransformCapture => _activeCapture;

        /// <summary>Sorting slots the active item occupies (0 when none) — a host layers its own renderers above this.</summary>
        internal int ActiveItemSortingCount => _activeView != null ? _activeView.SortingRendererCount : 0;

        internal void Bind(
            IReadOnlyReactiveProperty<ItemType> item,
            IReadOnlyReactiveProperty<string> colorName,
            IReadOnlyReactiveProperty<Vector2Int> slotIndex,
            ISlotGridConfig config,
            IItemConfiguration itemConfig,
            IGamePalette palette,
            int baseSortingOffset,
            int balloonRendererCount,
            PoolManager poolManager,
            SceneLightFieldService lightField = null,
            IProjectileFacingSource projectileFacing = null,
            Action onSortingFootprintChanged = null)
        {
            Unbind();

            _config = config;
            _itemConfig = itemConfig;
            _palette = palette;
            _baseSortingOffset = baseSortingOffset;
            _balloonRendererCount = balloonRendererCount;
            _slotIndex = slotIndex;
            _poolManager = poolManager;
            _lightField = lightField;
            _projectileFacing = projectileFacing;
            _onSortingFootprintChanged = onSortingFootprintChanged;

            item
                .Subscribe(type => OnItemChanged(type, colorName.Value))
                .AddTo(_disposables);

            colorName
                .Subscribe(RecolorActiveVisual)
                .AddTo(_disposables);

            slotIndex
                .Subscribe(ApplySorting)
                .AddTo(_disposables);
        }

        public void Unbind()
        {
            _disposables.Clear();
            ReturnActiveVisual();
            _onSortingFootprintChanged = null;
        }

        private void ApplySorting(Vector2Int slot)
        {
            if (_activeView == null || _config == null)
            {
                return;
            }

            var baseOrder = SortingHelper.SlotBaseSortingOrder(slot, _config.SlotsSize, _baseSortingOffset);
            _activeView.ApplySortingOrder(baseOrder + _balloonRendererCount);
        }

        private void OnItemChanged(ItemType type, string colorName)
        {
            ReturnActiveVisual();

            if (type == ItemType.None || _config == null || _poolManager == null)
            {
                _onSortingFootprintChanged?.Invoke();
                return;
            }

            var settings = _itemConfig[type];
            if (settings.VisualPrefab == null)
            {
                _onSortingFootprintChanged?.Invoke();
                return;
            }

            var key = settings.VisualPrefab.name;
            _activePoolKey = key;
            _activeView = _poolManager.GetOrRegister(key, () => new SimplePoolChannel<ItemVisualView>(settings.VisualPrefab));
            _activeCapture = _activeView.GetComponentInChildren<ITransformCapture>();

            _activeView.transform.SetParent(transform, false);
            _activeView.transform.localPosition = Vector3.zero;
            _activeView.transform.localScale = Vector3.one;

            var color = _palette.GetColor(colorName);
            _activeView.Activate(color);
            _activeView.SetRainbow(!string.IsNullOrEmpty(colorName) && _palette.IsRainbow(colorName));

            // The pooled icon isn't DI-injected; hand a laser its light-field access so the idle
            // telegraph can register (it's the capture we already resolved above).
            if (_lightField != null && _activeCapture is LaserItemRotation laser)
            {
                laser.ConfigureLightField(_lightField, _palette, settings.Laser);
            }

            // Same non-injection problem for a projectile-facing icon (e.g. the thrower, Snipe) — a
            // separate component from the transform capture, looked up independently.
            var facingRotator = _activeView.GetComponentInChildren<ProjectileFacingRotator>();
            if (_projectileFacing != null && facingRotator != null)
            {
                facingRotator.Configure(_projectileFacing);
            }

            // A sight probe (the shared per-item prediction-trace test feeding sight reactions, and the
            // rotator's own AlignPredictionHit) — configured with the same source. Independent of the
            // rotator so an item can react to sighting without one.
            var sightProbe = _activeView.GetComponentInChildren<PredictionSightProbe>();
            if (_projectileFacing != null && sightProbe != null)
            {
                sightProbe.Configure(_projectileFacing);
            }

            ApplySorting(_slotIndex.Value);
            _onSortingFootprintChanged?.Invoke();
        }

        private void RecolorActiveVisual(string colorName)
        {
            if (_activeView == null || string.IsNullOrEmpty(colorName))
            {
                return;
            }

            _activeView.SetColor(_palette.GetColor(colorName));
            _activeView.SetRainbow(_palette.IsRainbow(colorName));
        }

        private void ReturnActiveVisual()
        {
            if (_activeView != null && _poolManager != null && _activePoolKey != null)
            {
                _poolManager.Return(_activePoolKey, _activeView);
            }

            _activeView = null;
            _activeCapture = null;
            _activePoolKey = null;
        }
    }
}
