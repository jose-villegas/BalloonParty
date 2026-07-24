using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Extensions;

namespace BalloonParty.Item.Paint
{
    /// <summary>
    ///     Handles the Paint item: recolours balloons within landed blob radii of a packed splash
    ///     triangle to the paint-holder's colour. A rainbow holder's colour is the rainbow wildcard id,
    ///     so the same recolour converts targets to rainbow.
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

            // A rainbow paint-holder's colour IS the rainbow wildcard id, so recolouring targets to it
            // converts them to rainbow; any other holder recolours to its concrete colour.
            var tint = _palette.GetColor(paintColor);
            var triangle = PaintTriangle.Build(worldPosition, context.ProjectileDirection, settings.Paint);

            var blobPositions = new List<Vector2>();
            triangle.PackBlobs(settings.Paint.SpreadBlobRadius, MaxBlobs, blobPositions);

            var targetsByBlob = CollectPaintTargets(
                blobPositions, settings.Paint.SpreadBlobRadius, paintColor);

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
                Log.Error("PaintItem",
                    $"pooled effect for \"{key}\" is not an ISplashEffect — " +
                    "check the prefab's EffectView component.");
                _poolManager.Return(key, effect);
                return UniTask.CompletedTask;
            }

            splash.PrepareDisplay(flights, settings, _poolManager, PaintBlob);

            // A rainbow holder's blobs draw radial palette rings (the global rainbow bands) as they fly.
            if (_palette.IsRainbow(paintColor))
            {
                splash.SetRainbow();
            }

            effect.Play(worldPosition, tint, () => _poolManager.Return(key, effect));

            return UniTask.CompletedTask;

            // Captures only this activation's locals — a later splash may outlive it.
            void PaintBlob(int index)
            {
                if (index < 0 || index >= blobPositions.Count)
                {
                    return;
                }

                var landing = blobPositions[index];
                var direction = (landing - (Vector2)worldPosition).normalized;
                _disturbanceField.Stamp(
                    StampSource.Paint, landing, direction,
                    paletteIndex: _palette.PaletteIndexOf(paintColor));

                ApplyPaint(targetsByBlob[index], paintColor, tint);
            }
        }

        // Buckets align 1:1 with blobPositions; each is applied as its blob lands. Each covered slot is
        // classified once: paintable+different-colour = accept (recolour + drip), resists-paint = reject
        // (drip only, no recolour), everything else (empty, already-that-colour, non-balloon) = ignored.
        private List<PaintTarget>[] CollectPaintTargets(
            IReadOnlyList<Vector2> blobPositions, float blobRadius, string paintColor)
        {
            var buckets = new List<PaintTarget>[blobPositions.Count];
            var radiusSqr = blobRadius * blobRadius;

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    if (!TryClassify(slot, paintColor, out var target))
                    {
                        continue;
                    }

                    var position = (Vector2)_grid.IndexToWorldPosition(slot);
                    var nearest = NearestBlob(blobPositions, position, out var nearestSqr);
                    if (nearestSqr > radiusSqr)
                    {
                        continue;
                    }

                    (buckets[nearest] ??= new List<PaintTarget>()).Add(target);
                }
            }

            return buckets;
        }

        // Accept: paintable, not already the paint colour (skips already-rainbow balloons when the holder
        // is rainbow — same wildcard id). Reject: an IResistsPaint balloon (tough/unbreakable) — the drip
        // still plays and slides off, but no colour commits. Empty / non-balloon slots yield no target.
        private bool TryClassify(Vector2Int slot, string paintColor, out PaintTarget target)
        {
            target = default;

            if (_grid.IsEmpty(slot.x, slot.y))
            {
                return false;
            }

            var actor = _grid.At(slot);

            if (actor is IPaintable paintable)
            {
                if (paintable.Color.Value == paintColor)
                {
                    return false;
                }

                target = new PaintTarget(slot, paintable);
                return true;
            }

            if (actor is IResistsPaint)
            {
                target = new PaintTarget(slot, null);
                return true;
            }

            return false;
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

        // Accepts commit the new colour on the model; rejects don't. Both play the drip on the balloon's
        // view (resolved off the grid) with the incoming paint colour — layering makes an accept read as
        // paint settling over the just-committed colour and a reject as paint sliding off the unchanged body.
        private void ApplyPaint(IReadOnlyList<PaintTarget> targets, string paintColor, Color tint)
        {
            if (targets == null)
            {
                return;
            }

            foreach (var target in targets)
            {
                if (target.Accept)
                {
                    target.Paintable.Color.Value = paintColor;
                }

                _grid.ActorViewAt<IPaintReactive>(target.Slot)?.PlayPaintDrip(tint);
            }
        }

        // Paintable != null = accept (recolour); null = reject (drip slides off, no recolour).
        private readonly struct PaintTarget
        {
            public readonly Vector2Int Slot;
            public readonly IPaintable Paintable;

            public bool Accept => Paintable != null;

            public PaintTarget(Vector2Int slot, IPaintable paintable)
            {
                Slot = slot;
                Paintable = paintable;
            }
        }
    }
}
