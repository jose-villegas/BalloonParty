using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Animator))]
public class ShieldCounterAnimation : MonoBehaviour, IAnyProjectileBounceShieldListener, IAnyReadyToThrowListener, IAnyBalloonsBalanceEventListener
{
    private Animator _animator;
    private Contexts _contexts;
    private float _currentShieldTimer;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        _contexts = Contexts.sharedInstance;

        var e = _contexts.game.CreateEntity();
        e.AddAnyProjectileBounceShieldListener(this);
        e.AddAnyReadyToThrowListener(this);
        e.AddAnyBalloonsBalanceEventListener(this);

        var projectiles = _contexts.game.GetGroup(GameMatcher.ProjectileBounceShield);
        
        if (projectiles.count > 0)
        {
            var entity = projectiles.GetSingleEntity();
            OnAnyProjectileBounceShield(entity, entity.projectileBounceShield.Value);
        }
        else
        {
            _animator.SetTrigger("Waiting");
        }
    }

    public void OnAnyProjectileBounceShield(GameEntity entity, float value)
    {
        if (value > _currentShieldTimer)
        {
            _animator.SetTrigger("Gain");
        }
        else if (value < _currentShieldTimer)
        {
            _animator.SetTrigger("Lost");
        }

        _currentShieldTimer = value;
    }

    public void OnAnyReadyToThrow(GameEntity entity)
    {
        _animator.SetTrigger("Ready");
        _currentShieldTimer = 1f; 
    }

    public void OnAnyBalloonsBalanceEvent(GameEntity entity)
    {
        _animator.SetTrigger("Waiting");
    }
}