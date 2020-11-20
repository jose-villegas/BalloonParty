using Entitas;
using UnityEngine;
using UnityEngine.UI;

public class LevelUpPopUp : EntityLinkerController, IAnyGameLevelUpListener
{
    [SerializeField] private Animator _animator;
    [SerializeField] private Text _levelLabel;

    protected override void DefineEntity(IEntity e)
    {
        base.DefineEntity(e);

        var gameEntity = e as GameEntity;
        
        gameEntity.AddAnyGameLevelUpListener(this);
    }

    public void OnAnyGameLevelUp(GameEntity entity, int value)
    {
        _levelLabel.text = (value - 1).ToString("N0");
        _animator.SetTrigger("Appear");
    }
}
