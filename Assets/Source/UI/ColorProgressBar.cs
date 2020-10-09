using UnityEngine;
using UnityEngine.UI;

public class ColorProgressBar : MonoBehaviour
{
    [SerializeField] private Graphic[] _graphicsToSetColor;

    public void Setup(Color color, IGameConfiguration configuration)
    {
        foreach (var image in _graphicsToSetColor)
        {
            image.color = color;
        }
    }
}