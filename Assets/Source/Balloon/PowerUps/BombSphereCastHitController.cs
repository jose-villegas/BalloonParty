using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(LinkedViewController))]
public class BombSphereCastHitController : MonoBehaviour
{
    [SerializeField] private float _radius;

    private LinkedViewController _linkedView;

    private void Awake()
    {
        _linkedView = GetComponent<LinkedViewController>();
        _linkedView.OnViewLinked += OnViewLinked;
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
                var linkedView = result.GetComponent<LinkedViewController>();

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