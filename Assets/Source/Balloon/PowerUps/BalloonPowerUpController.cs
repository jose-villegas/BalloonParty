using System;
using Entitas;
using UnityEngine;

public abstract class BalloonPowerUpController : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] _spritesToSetColor;

    [Header("Layering")] [SerializeField] private BalloonSpriteLayerController _layerController;
    [SerializeField] private Renderer[] _renderers;

    [SerializeField, Range(0f, 1f)] private float _spritesAlpha;

    protected Contexts _contexts;
    protected IGroup<GameEntity> _freeProjectiles;
    protected IGameConfiguration _configuration;
    protected GameEntity _gameEntity;

    public virtual void Setup(IBalloonColorConfiguration colorConfiguration, GameEntity gameEntity)
    {
        _contexts = Contexts.sharedInstance;
        _configuration = _contexts.configuration.gameConfiguration.value;
        _freeProjectiles = _contexts.game.GetGroup(GameMatcher.AllOf(GameMatcher.FreeProjectile));
        _gameEntity = gameEntity;

        foreach (var spriteRenderer in _spritesToSetColor)
        {
            var color = colorConfiguration.Color;
            spriteRenderer.color = new Color(color.r, color.g, color.b, _spritesAlpha);
        }

        var baseSort = _layerController.SortingOrder + 1;
        
        foreach (var render in _renderers)
        {
            render.sortingOrder = baseSort++;
        }
    }

    public abstract void Activate();
}