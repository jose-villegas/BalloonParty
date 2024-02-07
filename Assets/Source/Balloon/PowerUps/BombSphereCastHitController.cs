using Entitas;
using UnityEngine;

[RequireComponent(typeof(LinkedViewController))]
public class BombSphereCastHitController : MonoBehaviour
{
    [SerializeField] private float _radius;

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
        var results =
            Physics2D.OverlapCircleAll(gameEntity.position.Value, _radius, LayerMask.GetMask("Balloons"));
        var settings =
            Contexts.sharedInstance.configuration.gameConfiguration.value.PowerUpConfiguration[BalloonPowerUp.Bomb];

        if (results != null && results.Length > 0)
        {
            foreach (var result in results)
            {
                var linkedView = _cache.Fetch(result);

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
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
}