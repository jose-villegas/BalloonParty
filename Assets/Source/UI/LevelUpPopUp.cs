using System.Collections;
using Entitas;
using UnityEngine;
using UnityEngine.UI;

public class LevelUpPopUp : EntityLinkerController, IAnyGameLevelUpListener
{
    [SerializeField] private Animator _animator;
    [SerializeField] private Text _levelLabel;
    [SerializeField] private Image _levelGlowFill;
    [SerializeField] private ParticleSystem _levelGlowFillParticleSystem;
    [Header("Animation")] [SerializeField] private float _fillAnimationDelay;
    [SerializeField] private float _playParticlesDelay;

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

        StartCoroutine(LevelGlowFill(value));
    }

    private IEnumerator LevelGlowFill(int value)
    {
        yield return new WaitForSeconds(_playParticlesDelay);
        var duration = _levelGlowFillParticleSystem.main.duration;
        var t = 0f;
        
        _levelGlowFillParticleSystem.Stop();
        _levelGlowFillParticleSystem.Play();
        
        yield return new WaitForSeconds(_fillAnimationDelay);

        while (t <= duration)
        {
            _levelGlowFill.fillAmount = t / duration;
            t += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }

        _levelGlowFill.fillAmount = 1f;
        _levelLabel.text = value.ToString("N0");
    }
}
