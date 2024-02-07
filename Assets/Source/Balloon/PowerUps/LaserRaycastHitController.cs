using System;
using System.Collections.Generic;
using Entitas;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(LinkedViewController))]
public class LaserRaycastHitController : MonoBehaviour
{
    [SerializeField] private float _destroyAfter;
    [SerializeField] private float _raycastDistance;
    [SerializeField] private float _circleCastRadius;
    private LinkedViewController _linkedView;
    private LinkedViewColliderCacheComponent _cache;

    private void Awake()
    {
        _linkedView = GetComponent<LinkedViewController>();
        _linkedView.OnViewLinked += OnViewLinked;

        // obtain collider cache
        var _contexts = Contexts.sharedInstance;
        var cacheEntity = _contexts.game.GetEntities(GameMatcher.LinkedViewColliderCache);

        if (cacheEntity != null && cacheEntity.Length > 0)
        {
            _cache = cacheEntity[0].GetComponent(GameComponentsLookup.LinkedViewColliderCache) as LinkedViewColliderCacheComponent;
        }
    }

    private void OnViewLinked(GameEntity gameEntity)
    {
        var results = new List<RaycastHit2D>();

        var layer = LayerMask.GetMask("Balloons");

        // cross collision with rays
        results.AddRange(Physics2D.CircleCastAll(transform.position, _circleCastRadius, transform.right,
            _raycastDistance, layer));
        results.AddRange(Physics2D.CircleCastAll(transform.position, _circleCastRadius, -transform.right,
            _raycastDistance, layer));
        results.AddRange(Physics2D.CircleCastAll(transform.position, _circleCastRadius, -transform.up,
            _raycastDistance, layer));
        results.AddRange(Physics2D.CircleCastAll(transform.position, _circleCastRadius, transform.up,
            _raycastDistance, layer));

        var settings =
            Contexts.sharedInstance.configuration.gameConfiguration.value.PowerUpConfiguration[BalloonPowerUp.Laser];

        if (results != null && results.Count > 0)
        {
            foreach (var result in results)
            {
                var linkedView = _cache.Fetch(result.collider);

                if (linkedView != null)
                {
                    var linkedEntity = linkedView.LinkedEntity as GameEntity;

                    if (linkedEntity.isBalloon)
                    {
                        linkedEntity.isBalloonHit = true;
                        linkedEntity.isBalloonPowerUpHit = true;
                        linkedEntity.ReplaceBalloonNudge(settings.NudgeDuration, settings.NudgeDistance);
                    }
                }
            }
        }

        Destroy(gameObject, _destroyAfter);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position + transform.up, _circleCastRadius);
        Gizmos.DrawRay(transform.position, transform.up);
        Gizmos.DrawRay(transform.position, -transform.up);
        Gizmos.DrawRay(transform.position, transform.right);
        Gizmos.DrawRay(transform.position, -transform.right);
    }
}