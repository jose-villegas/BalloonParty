using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(LinkedViewController))]
public class ProjectileGlowColorController : MonoBehaviour, IBalloonColorListener
{
    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField, Range(0f, 1f)] private float _alpha;
    [SerializeField] private float _colorDuration;

    private LinkedViewController _linkedView;
    private IGameConfiguration _configuration;

    private void Awake()
    {
        _linkedView = GetComponent<LinkedViewController>();
        _linkedView.OnViewLinked += OnViewLinked;
    }

    private void Start()
    {
        _configuration = Contexts.sharedInstance.configuration.gameConfiguration.value;
    }

    private void OnViewLinked(GameEntity gameEntity)
    {
        gameEntity.AddBalloonColorListener(this);
    }

    public void OnBalloonColor(GameEntity entity, string value)
    {
        if (_renderer != null)
        {
            var color = _configuration.BalloonColor(value);
            var targetColor = new Color(color.r, color.g, color.b, _alpha);
            _renderer.DOColor(targetColor, _colorDuration);
        }
    }
}