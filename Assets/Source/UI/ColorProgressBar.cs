using UnityEngine;
using UnityEngine.UI;

public class ColorProgressBar : MonoBehaviour
{
    [SerializeField] private Graphic[] _graphicsToSetColor;

    public void Setup(IBalloonColorConfiguration colorConfiguration, IGameConfiguration gameConfiguration)
    {
        foreach (var image in _graphicsToSetColor)
        {
            image.color = colorConfiguration.Color;
        }
    }
}