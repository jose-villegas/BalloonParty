using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class ShieldCounterLabel : MonoBehaviour, IAnyProjectileBounceShieldListener, IAnyReadyToThrowListener, IAnyBalloonsBalanceEventListener
{
    private Text _label;
    private Contexts _contexts;

    private void Awake()
    {
        _label = GetComponent<Text>();
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
            _label.text = "--";
        }
    }

    public void OnAnyProjectileBounceShield(GameEntity entity, float value)
    {
        _label.text = value.ToString("N0");
    }

    public void OnAnyReadyToThrow(GameEntity entity)
    {
        _label.text = "1";
    }

    public void OnAnyBalloonsBalanceEvent(GameEntity entity)
    {
        _label.text = "--";
    }
}