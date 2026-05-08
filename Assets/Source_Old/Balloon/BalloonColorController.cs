using UnityEngine;

[RequireComponent(typeof(LinkedViewController))]
public class BalloonColorController : MonoBehaviour, IBalloonColorListener
{
    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField] private SpriteRenderer _shadowRenderer;
    [SerializeField, Range(0f, 1f)] private float _shadowAlpha;
    [SerializeField, Range(0f, 5f)] private float _shadowIntensity;

    private LinkedViewController _linkedView;
    private IGameConfiguration _configuration;

    private void Awake()
    {
        _linkedView = GetComponent<LinkedViewController>();
        _linkedView.OnViewLinked += OnViewLinked;
    }

    private void OnViewLinked(GameEntity gameEntity)
    {
        _configuration = Contexts.sharedInstance.configuration.gameConfiguration.value;

        gameEntity.AddBalloonColorListener(this);
        OnBalloonColor(gameEntity, gameEntity.balloonColor.Value);
    }

    public void OnBalloonColor(GameEntity entity, string value)
    {
        var color = _configuration.BalloonColor(value);

        if (_renderer != null)
        {
            _renderer.color = color;
        }

        if (_shadowRenderer != null)
        {
            _shadowRenderer.color = new Color(color.r * _shadowIntensity, color.g * _shadowIntensity, color.b * _shadowIntensity, _shadowAlpha);
        }
    }
}