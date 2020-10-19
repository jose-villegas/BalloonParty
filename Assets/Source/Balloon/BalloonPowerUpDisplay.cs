using UnityEngine;
using UnityEngine.UI;

public class BalloonPowerUpDisplay : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] _spritesToSetColor;

    [SerializeField, Range(0f, 1f)]
    private float _spritesAlpha;
    
    public void Setup(IBalloonColorConfiguration colorConfiguration)
    {
        foreach (var spriteRenderer in _spritesToSetColor)
        {
            var color = colorConfiguration.Color;
            spriteRenderer.color = new Color(color.r, color.g, color.b, _spritesAlpha);
        }
    }
}