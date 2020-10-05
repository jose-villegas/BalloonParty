using UnityEngine;

[RequireComponent(typeof(LinkedViewController))]
public class BalloonAnimationController : MonoBehaviour, IStableBalloonListener, IStableBalloonRemovedListener
{
    [SerializeField] private Animator _animator;

    private LinkedViewController _linkedView;
    private IGameConfiguration _configuration;

    private void Awake()
    {
        _configuration = Contexts.sharedInstance.configuration.gameConfiguration.value;

        _linkedView = GetComponent<LinkedViewController>();
        _linkedView.OnViewLinked += OnViewLinked;
    }

    private void OnViewLinked(GameEntity gameEntity)
    {
        gameEntity.AddStableBalloonListener(this);
        gameEntity.AddStableBalloonRemovedListener(this);

        if (gameEntity.isStableBalloon)
        {
            OnStableBalloon(gameEntity);
        }
        else
        {
            OnStableBalloonRemoved(gameEntity);
        }
    }

    public void OnStableBalloon(GameEntity entity)
    {
        _animator.SetBool("IsStable", true);
    }

    public void OnStableBalloonRemoved(GameEntity entity)
    {
        _animator.SetBool("IsStable", false);
    }
}