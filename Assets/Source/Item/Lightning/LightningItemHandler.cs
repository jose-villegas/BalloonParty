using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using BalloonParty.Projectile.Model;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using Light = BalloonParty.Shared.SceneLight.Light;

namespace BalloonParty.Item.Lightning
{
    /// <summary>
    ///     Handles the Lightning item: chains through same-color balloons nearest-first and destroys
    ///     them. A rainbow holder instead <em>converts</em> a whole colour group to rainbow (never
    ///     destroys) — the colour chosen by the last projectile fired, so it seeds a combo rather than
    ///     clearing the board.
    /// </summary>
    internal class LightningItemHandler : IBalloonItem, IStartable, IDisposable
    {
        private sealed class ByDistanceComparer : IComparer<(IBalloonModel model, Vector3 worldPos)>
        {
            internal Vector3 Origin;

            public int Compare((IBalloonModel model, Vector3 worldPos) a, (IBalloonModel model, Vector3 worldPos) b)
                => (Origin - a.worldPos).sqrMagnitude.CompareTo((Origin - b.worldPos).sqrMagnitude);
        }

        private readonly IHitDispatcher _hitDispatcher;
        private readonly IItemConfiguration _itemConfig;
        private readonly IGamePalette _palette;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly PoolManager _poolManager;
        private readonly SlotGrid _grid;
        private readonly SceneLightFieldService _lightField;
        private readonly CancellationTokenSource _lifetime = new();

        // Safe to share: set and consumed synchronously within one CollectSortedTargets call.
        private readonly ByDistanceComparer _distanceComparer = new();

        private IProjectileModel _activeProjectile;
        private IDisposable _subscription;

        public ItemType Type => ItemType.Lightning;

        [Inject]
        public LightningItemHandler(
            IItemConfiguration itemConfig,
            IHitDispatcher hitDispatcher,
            IGamePalette palette,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            SlotGrid grid,
            PoolManager poolManager,
            SceneLightFieldService lightField)
        {
            _itemConfig = itemConfig;
            _hitDispatcher = hitDispatcher;
            _palette = palette;
            _loadedSubscriber = loadedSubscriber;
            _grid = grid;
            _poolManager = poolManager;
            _lightField = lightField;
        }

        public void Start()
        {
            _subscription = _loadedSubscriber.Subscribe(msg => _activeProjectile = msg.Model);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        public UniTask Activate(ItemActivationContext activation)
        {
            var balloon = activation.Balloon;
            var worldPosition = activation.WorldPosition;

            var settings = _itemConfig[ItemType.Lightning];
            var sourceColorId = balloon.GetColorId();
            var convertsToRainbow = _palette.IsRainbow(sourceColorId);

            // A rainbow holder converts a whole colour group to rainbow (chosen by the last projectile's
            // colour) instead of destroying; a concrete holder chains and destroys its own colour.
            var matchColor = convertsToRainbow ? _activeProjectile?.ColorName.Value : sourceColorId;
            if (convertsToRainbow && (string.IsNullOrEmpty(matchColor) || _palette.IsRainbow(matchColor)))
            {
                matchColor = _grid.FindNearestColorId(balloon.SlotIndex.Value, balloon, _palette);
            }

            if (string.IsNullOrEmpty(matchColor))
            {
                return UniTask.CompletedTask;
            }

            // Per-activation list: the chain view holds this reference long after this method returns.
            var targets = new List<(IBalloonModel model, Vector3 worldPos)>();
            CollectSortedTargets(balloon, worldPosition, matchColor, targets);

            if (targets.Count == 0)
            {
                return UniTask.CompletedTask;
            }

            var context = new DamageContext(settings.Damage, settings.Flags, sourceColorId);

            void ApplyTo(IBalloonModel model, Vector3 pos)
            {
                if (convertsToRainbow)
                {
                    if (model is IPaintable paintable)
                    {
                        paintable.Color.Value = GamePalette.RainbowColorId;
                    }
                }
                else
                {
                    _hitDispatcher.Dispatch(ActorHitMessage.From(model, pos, Vector3.zero, context));
                }
            }

            if (settings.ActivationEffectPrefab == null)
            {
                foreach (var (model, pos) in targets)
                {
                    ApplyTo(model, pos);
                }

                return UniTask.CompletedTask;
            }

            var positions = new List<Vector3>(targets.Count + 1) { worldPosition };
            foreach (var (_, pos) in targets)
            {
                positions.Add(pos);
            }

            var key = settings.ActivationEffectPrefab.name;
            var effect = _poolManager.GetOrRegister(key,
                () => new SimplePoolChannel<EffectView>(settings.ActivationEffectPrefab));

            if (effect is not IChainEffect chain)
            {
                Log.Error("LightningItem",
                    $"pooled effect for \"{key}\" is not an IChainEffect — " +
                    "check the prefab's EffectView component.");
                _poolManager.Return(key, effect);
                return UniTask.CompletedTask;
            }

            // matchColor is concrete in both paths (rainbow uses the projectile colour).
            var tint = _palette.GetColor(matchColor);

            // A rainbow chain glows iridescent (lerps through the palette's colours); a concrete chain
            // stays its own colour.
            if (convertsToRainbow)
            {
                chain.SetGlowColors(_palette.ColorValues(), settings.Lightning.GlowColorCycles);
            }

            chain.PrepareDisplay(positions, settings, OnJump);
            effect.Play(Vector3.zero, tint, () => _poolManager.Return(key, effect));

            return UniTask.CompletedTask;

            void OnJump(int index)
            {
                if (index >= targets.Count)
                {
                    return;
                }

                var (model, pos) = targets[index];
                ApplyTo(model, pos);

                // A colour-matched capsule flash along the arc the bolt just traveled (positions[index]
                // is the node it jumped from), held briefly then released. The chain unfolds over
                // several frames, so each jump registers its own light live (lights are retained
                // state, not a single-frame stamp). positions is per-activation, so capturing it here
                // is safe even though jumps arrive after Activate returns.
                var lightning = settings.Lightning;
                var registration = _lightField.RegisterLight(
                    Light.Segment(positions[index], pos, lightning.PopLightRadius, lightning.PopLightIntensity,
                        _palette.PaletteIndexOf(matchColor)));
                ExpireLight(lightning.PopLightSeconds, registration).Forget();
            }
        }

        private async UniTaskVoid ExpireLight(float seconds, IDisposable registration)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // Run ended before the flash faded — the field clears its own lights; still release below.
            }
            finally
            {
                registration.Dispose();
            }
        }

        private void CollectSortedTargets(
            IBalloonModel balloon, Vector3 origin, string matchColor, List<(IBalloonModel model, Vector3 worldPos)> result)
        {
            result.Clear();

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    var slot = new Vector2Int(col, row);
                    if (_grid.At(slot) is not IBalloonModel model || ReferenceEquals(model, balloon))
                    {
                        continue;
                    }

                    if (model is not IHasColor modelColor || modelColor.Color.Value != matchColor)
                    {
                        continue;
                    }

                    result.Add((model, _grid.IndexToWorldPosition(slot)));
                }
            }

            _distanceComparer.Origin = origin;
            result.Sort(_distanceComparer);
        }
    }
}
