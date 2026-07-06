using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Item.Paint
{
    /// <summary>
    ///     Handles the Paint item. On activation it lays a triangular region out along the projectile's
    ///     travel direction (see <see cref="PaintTriangle" />), circle-packs it with blob VFX flung from
    ///     the hit point, and — as those blobs land — recolours every balloon within a blob's radius to
    ///     the popped balloon's colour (Splatoon-style), so painting tracks the visible splash coverage.
    ///     The triangle's offset, length, base width, and blob radius are authored per item in
    ///     <see cref="PaintSettings" />.
    /// </summary>
    internal class PaintItemHandler : IBalloonItem
    {
        private const int MaxBlobs = 64;

        private readonly IGamePalette _palette;
        private readonly IItemConfiguration _itemConfig;
        private readonly SlotGrid _grid;
        private readonly PoolManager _poolManager;
        private readonly DisturbanceFieldService _disturbanceField;

        public ItemType Type => ItemType.Paint;

        [Inject]
        public PaintItemHandler(
            IGamePalette palette,
            IItemConfiguration itemConfig,
            SlotGrid grid,
            PoolManager poolManager,
            DisturbanceFieldService disturbanceField)
        {
            _palette = palette;
            _itemConfig = itemConfig;
            _grid = grid;
            _poolManager = poolManager;
            _disturbanceField = disturbanceField;
        }

        public UniTask Activate(ItemActivationContext context)
        {
            var balloon = context.Balloon;
            var worldPosition = context.WorldPosition;

            var settings = _itemConfig[ItemType.Paint];
            if (balloon is not IHasColor sourceColor)
            {
                return UniTask.CompletedTask;
            }

            var paintColor = sourceColor.Color.Value;
            if (string.IsNullOrEmpty(paintColor))
            {
                return UniTask.CompletedTask;
            }

            var tint = _palette.GetColor(paintColor);
            var triangle = PaintTriangle.Build(worldPosition, context.ProjectileDirection, settings.Paint);

            var blobPositions = new List<Vector2>();
            triangle.PackBlobs(settings.Paint.SpreadBlobRadius, MaxBlobs, blobPositions);

            var targetsByBlob = CollectPaintTargets(blobPositions, settings.Paint.SpreadBlobRadius, paintColor);

            if (settings.ActivationEffectPrefab == null)
            {
                for (var i = 0; i < blobPositions.Count; i++)
                {
                    PaintBlob(i);
                }

                return UniTask.CompletedTask;
            }

            var flights = new List<(Vector3 from, Vector3 to)>(blobPositions.Count);
            foreach (var target in blobPositions)
            {
                flights.Add((worldPosition, new Vector3(target.x, target.y, worldPosition.z)));
            }

            var key = settings.ActivationEffectPrefab.name;
            var effect = _poolManager.GetOrRegister(key,
                () => new SimplePoolChannel<EffectView>(settings.ActivationEffectPrefab));

            if (effect is not ISplashEffect splash)
            {
                Debug.LogError(
                    $"PaintItemHandler: pooled effect for \"{key}\" is not an ISplashEffect — " +
                    "check the prefab's EffectView component.");
                _poolManager.Return(key, effect);
                return UniTask.CompletedTask;
            }

            splash.PrepareDisplay(flights, settings, _poolManager, PaintBlob);
            effect.Play(worldPosition, tint, () => _poolManager.Return(key, effect));

            return UniTask.CompletedTask;

            // Each blob stamps the disturbance field where it lands (the visible splash) and recolours the
            // balloons it covers. Captures only this activation's locals — splashes land over time and a
            // second Paint activation may run in between, so no handler field may be read here.
            void PaintBlob(int index)
            {
                if (index < 0 || index >= blobPositions.Count)
                {
                    return;
                }

                var landing = blobPositions[index];
                var direction = (landing - (Vector2)worldPosition).normalized;
                _disturbanceField.Stamp(StampSource.Paint, landing, direction);
                Recolor(paintColor, targetsByBlob[index]);
            }
        }

        // Different-colour paintable balloons within blobRadius of a packed blob, bucketed by the blob
        // nearest to each — so a balloon is recoloured the moment its covering blob lands. Painting
        // tracks the visible splash coverage: balloons in gaps between blobs are left alone. Buckets are
        // indexed 1:1 with blobPositions; empty buckets are simply blobs that covered no balloon.
        private List<IPaintable>[] CollectPaintTargets(
            IReadOnlyList<Vector2> blobPositions, float blobRadius, string paintColor)
        {
            var buckets = new List<IPaintable>[blobPositions.Count];
            var radiusSqr = blobRadius * blobRadius;

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    var slot = new Vector2Int(col, row);
                    var position = (Vector2)_grid.IndexToWorldPosition(slot);
                    if (_grid.At(slot) is not IPaintable paintable || paintable.Color.Value == paintColor)
                    {
                        continue;
                    }

                    var nearest = NearestBlob(blobPositions, position, out var nearestSqr);
                    if (nearestSqr > radiusSqr)
                    {
                        continue;
                    }

                    (buckets[nearest] ??= new List<IPaintable>()).Add(paintable);
                }
            }

            return buckets;
        }

        private static int NearestBlob(IReadOnlyList<Vector2> blobPositions, Vector2 position, out float bestSqr)
        {
            var best = 0;
            bestSqr = float.MaxValue;
            for (var i = 0; i < blobPositions.Count; i++)
            {
                var sqr = (blobPositions[i] - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            return best;
        }

        private static void Recolor(string paintColor, IReadOnlyList<IPaintable> targets)
        {
            if (targets == null)
            {
                return;
            }

            foreach (var target in targets)
            {
                target.Color.Value = paintColor;
            }
        }
    }
}
