using Entitas;
using UnityEngine;

public class LevelUpPopUp : EntityLinkerController, IAnyGameLevelUpListener
{
    [SerializeField] private Animator _animator;

    protected override void DefineEntity(IEntity e)
    {
        base.DefineEntity(e);

        var gameEntity = e as GameEntity;
        
        gameEntity.AddAnyGameLevelUpListener(this);
    }

    public void OnAnyGameLevelUp(GameEntity entity)
    {
        _animator.SetTrigger("Appear");
    }
}
