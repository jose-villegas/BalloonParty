﻿using System.Collections.Generic;
using Entitas;
using UnityEngine;

public class ProjectileBounceSystem : IExecuteSystem
{
    private readonly Contexts _contexts;
    private readonly IGameConfiguration _configuration;
    private readonly IGroup<GameEntity> _freeProjectiles;

    public ProjectileBounceSystem(Contexts contexts)
    {
        _contexts = contexts;
        _configuration = _contexts.configuration.gameConfiguration.value;
        _freeProjectiles =
            _contexts.game.GetGroup(GameMatcher.AllOf(GameMatcher.FreeProjectile, GameMatcher.ProjectileBounceShield));
    }

    public void Execute()
    {
        foreach (var freeProjectile in _freeProjectiles.GetEntities())
        {
            var shield = freeProjectile.projectileBounceShield.Value;
            var position = freeProjectile.position.Value;
            var direction = freeProjectile.direction.Value;
            var reflect = Vector3.zero;

            // top limit
            if (position.y > _configuration.LimitsClockwise.x)
            {
                reflect += Vector3.down;
                position.y = _configuration.LimitsClockwise.x;
            }

            // right limit
            if (position.x > _configuration.LimitsClockwise.y)
            {
                reflect += Vector3.left;
                position.x = _configuration.LimitsClockwise.y;
            }

            // bottom limit
            if (position.y < _configuration.LimitsClockwise.z)
            {
                reflect += Vector3.up;
                position.y = _configuration.LimitsClockwise.z;
            }

            // left limit
            if (position.x < _configuration.LimitsClockwise.w)
            {
                reflect += Vector3.right;
                position.x = _configuration.LimitsClockwise.w;
            }

            // the projectile has bounced, consume a shield
            if (reflect != Vector3.zero)
            {
                if (OnProjectileWallHit(freeProjectile, shield, position)) continue;
            }

            freeProjectile.ReplaceDirection(Vector2.Reflect(direction, reflect.normalized));
            freeProjectile.ReplacePosition(position);
        }

#if UNITY_EDITOR
        Debug.DrawLine(Vector3.up * _configuration.LimitsClockwise.x + Vector3.right * 100,
            Vector3.up * _configuration.LimitsClockwise.x - Vector3.right * 100, Color.red);
        Debug.DrawLine(Vector3.right * _configuration.LimitsClockwise.y + Vector3.up * 100,
            Vector3.right * _configuration.LimitsClockwise.y - Vector3.up * 100, Color.red);
        Debug.DrawLine(Vector3.up * _configuration.LimitsClockwise.z + Vector3.right * 100,
            Vector3.up * _configuration.LimitsClockwise.z - Vector3.right * 100, Color.red);
        Debug.DrawLine(Vector3.right * _configuration.LimitsClockwise.w + Vector3.up * 100,
            Vector3.right * _configuration.LimitsClockwise.w - Vector3.up * 100, Color.red);
#endif
    }

    private bool OnProjectileWallHit(GameEntity freeProjectile, float shield, Vector3 position)
    {
        // play bounce particle fx
        var bounce = _contexts.game.CreateEntity();
        bounce.AddPosition(position);
        bounce.AddPlayParticleFX("PSVFX_ShieldBounce");

        if (freeProjectile.hasBalloonColor)
        {
            bounce.AddParticleFXStartColor(_configuration.BalloonColor(freeProjectile.balloonColor.Value));
        }

        if (shield > 0)
        {
            freeProjectile.ReplaceProjectileBounceShield(shield - 1);
        }
        else
        {
            freeProjectile.isDestroyed = true;

            // check if balloons can be moved to re-balance
            var e = _contexts.game.CreateEntity();
            e.isBalloonsBalanceEvent = true;

            // increase the turn counter, a game turn ends at projectile dead
            if (_contexts.game.hasGameTurnCounter)
            {
                _contexts.game.ReplaceGameTurnCounter(_contexts.game.gameTurnCounter.Value + 1);
            }

            // reload projectile
            var thrower = _contexts.game.throwerEntity;
            thrower.isReadyToLoad = true;

            return true;
        }

        return false;
    }
}