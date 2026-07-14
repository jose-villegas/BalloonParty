using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using Light = BalloonParty.Shared.SceneLight.Light;

namespace BalloonParty.Item.Bomb
{
    internal class BombItemHandler : IBalloonItem, IDisposable
    {
        // Peak magnitude of the flash light; radius matches the blast. Fallback lifetime if the effect
        // reports no duration.
        private const float BlastLightIntensity = 3f;
        private const float FallbackBlastSeconds = 0.4f;

        private readonly ItemEffectPlayer _effectPlayer;
        private readonly BalloonOverlapQuery _overlap;
        private readonly IHitDispatcher _hitDispatcher;
        private readonly IPublisher<NudgeMessage> _nudgePublisher;
        private readonly IItemConfiguration _itemConfig;
        private readonly IGamePalette _palette;
        private readonly List<Collider2D> _overlapResults = new(8);
        private readonly Vector2Int[] _neighborBuffer = new Vector2Int[6];
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly SceneLightFieldService _lightField;
        private readonly CancellationTokenSource _lifetime = new();

        public ItemType Type => ItemType.Bomb;

        [Inject]
        public BombItemHandler(
            IItemConfiguration itemConfig,
            IHitDispatcher hitDispatcher,
            IPublisher<NudgeMessage> nudgePublisher,
            IGamePalette palette,
            ItemEffectPlayer effectPlayer,
            BalloonOverlapQuery overlap,
            DisturbanceFieldService disturbanceField,
            SceneLightFieldService lightField)
        {
            _itemConfig = itemConfig;
            _hitDispatcher = hitDispatcher;
            _nudgePublisher = nudgePublisher;
            _palette = palette;
            _effectPlayer = effectPlayer;
            _overlap = overlap;
            _disturbanceField = disturbanceField;
            _lightField = lightField;
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        public UniTask Activate(ItemActivationContext activation)
        {
            var balloon = activation.Balloon;
            var worldPosition = activation.WorldPosition;

            var settings = _itemConfig[ItemType.Bomb];

            // Shockwave first, before blast nudges mark neighbors unstable.
            _nudgePublisher.Publish(new NudgeMessage(
                null,
                worldPosition,
                NudgeType.Shockwave,
                settings.Bomb.NudgeOverrides));

            var sourceColorId = balloon.GetColorId();
            var context = new DamageContext(settings.Damage, settings.Flags, sourceColorId);
            var isRainbow = _palette.IsRainbow(sourceColorId);

            List<IPaintable> converts = null;
            if (isRainbow)
            {
                converts = RainbowBlast(balloon, worldPosition, settings.Bomb, context);
            }
            else
            {
                BlastBalloons(balloon, worldPosition, settings.Bomb.Radius, context);
            }

            // A rainbow bomb only scales the effect visually — the kill radius is unchanged.
            var effectDuration = _effectPlayer.Play(settings,
                worldPosition,
                sourceColorId,
                isRainbow ? settings.Bomb.RainbowEffectScale : 1f);

            _disturbanceField.Stamp(
                StampSource.Bomb, worldPosition, Vector2.zero,
                paletteIndex: _palette.PaletteIndexOf(sourceColorId));

            // A blast-coloured flash light matching the blast radius (visual scale for a rainbow bomb),
            // held for the effect then released.
            var lightRadius = settings.Bomb.Radius * (isRainbow ? settings.Bomb.RainbowEffectScale : 1f);
            var registration = _lightField.RegisterLight(
                new Light(worldPosition, lightRadius * 3f, BlastLightIntensity, _palette.PaletteIndexOf(sourceColorId)));
            ExpireLight(effectDuration, registration).Forget();

            if (converts != null)
            {
                // Conversion lands mid-effect, once the blast visual has read.
                ConvertAfterDelay(converts, effectDuration * 0.5f).Forget();
            }

            return UniTask.CompletedTask;
        }

        private void BlastBalloons(IBalloonModel balloon, Vector3 worldPosition, float radius, DamageContext context)
        {
            var bombSlot = balloon.SlotIndex.Value;
            HexCoordinates.HexNeighborIndices(bombSlot.x, bombSlot.y, _neighborBuffer);

            // Direct hex neighbors always take piercing damage (guaranteed kill).
            var piercingContext = new DamageContext(context.Damage, DamageFlags.Piercing, context.SourceColorId);

            var count = Physics2D.OverlapCircle(worldPosition, radius, _overlap.Filter, _overlapResults);

            for (var i = 0; i < count; i++)
            {
                if (!_overlap.TryResolveBalloon(_overlapResults[i], balloon, out var balloonView, out var model))
                {
                    continue;
                }

                var modelSlot = model.SlotIndex.Value;
                var isNeighbor = false;
                for (var n = 0; n < 6; n++)
                {
                    if (_neighborBuffer[n] == modelSlot)
                    {
                        isNeighbor = true;
                        break;
                    }
                }

                var hitContext = isNeighbor ? piercingContext : context;

                _hitDispatcher.Dispatch(ActorHitMessage.From(model,
                    balloonView.transform.position,
                    Vector3.zero,
                    hitContext));
            }
        }

        // Rainbow bomb. Classifies every balloon once, at detonation, by CENTRE distance (not collider
        // overlap — that over-reaches and eats the conversion band): centre within Radius is a guaranteed
        // kill; centre in the ring beyond it (up to Radius + RainbowConversionRange) is collected to
        // convert after the effect plays. Returns the convert list (null when empty).
        private List<IPaintable> RainbowBlast(
            IBalloonModel balloon, Vector3 worldPosition, BombSettings bomb, DamageContext context)
        {
            var killRadius = bomb.Radius;
            var outerRadius = bomb.Radius + bomb.RainbowConversionRange;
            var killSqr = killRadius * killRadius;
            var outerSqr = outerRadius * outerRadius;
            var piercingContext = new DamageContext(context.Damage, DamageFlags.Piercing, context.SourceColorId);

            List<IPaintable> converts = null;
            var count = Physics2D.OverlapCircle(worldPosition, outerRadius, _overlap.Filter, _overlapResults);

            for (var i = 0; i < count; i++)
            {
                if (!_overlap.TryResolveBalloon(_overlapResults[i], balloon, out var balloonView, out var model))
                {
                    continue;
                }

                var distSqr = ((Vector2)balloonView.transform.position - (Vector2)worldPosition).sqrMagnitude;
                if (distSqr <= killSqr)
                {
                    _hitDispatcher.Dispatch(ActorHitMessage.From(model,
                        balloonView.transform.position,
                        Vector3.zero,
                        piercingContext));
                }
                else if (distSqr <= outerSqr && model is IPaintable paintable)
                {
                    (converts ??= new List<IPaintable>()).Add(paintable);
                }
            }

            return converts;
        }

        // Band survivors were captured at detonation (they're never killed), so recolour the held
        // references after the delay — no re-query, which would miss balloons the collapsing stack moved.
        private async UniTaskVoid ConvertAfterDelay(IReadOnlyList<IPaintable> targets, float delaySeconds)
        {
            if (delaySeconds > 0f)
            {
                await UniTask.Delay(Mathf.RoundToInt(delaySeconds * 1000f));
            }

            foreach (var target in targets)
            {
                target.Color.Value = GamePalette.RainbowColorId;
            }
        }

        private async UniTaskVoid ExpireLight(float seconds, IDisposable registration)
        {
            var duration = seconds > 0f ? seconds : FallbackBlastSeconds;
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: _lifetime.Token);
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
    }
}
