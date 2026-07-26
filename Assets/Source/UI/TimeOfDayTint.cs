using BalloonParty.Shared.SceneLight;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BalloonParty.UI
{
    /// <summary>Tints a collection of graphics and/or sprite renderers to match the current time-of-day light color.</summary>
    internal class TimeOfDayTint : MonoBehaviour
    {
        [SerializeField] private Graphic[] _graphics;
        [SerializeField] private SpriteRenderer[] _spriteRenderers;
        [Tooltip("How strongly the time-of-day color overrides the original. 0 = no tint, 1 = full replace.")]
        [SerializeField] [Range(0f, 1f)] private float _intensity = 1f;

        [Inject] private ISceneLightRuntime _lightRuntime;

        private Color[] _originalGraphicColors;
        private Color[] _originalSpriteColors;

        private void Start()
        {
            if (_graphics != null && _graphics.Length > 0)
            {
                _originalGraphicColors = new Color[_graphics.Length];
                for (var i = 0; i < _graphics.Length; i++)
                {
                    _originalGraphicColors[i] = _graphics[i].color;
                }
            }

            if (_spriteRenderers != null && _spriteRenderers.Length > 0)
            {
                _originalSpriteColors = new Color[_spriteRenderers.Length];
                for (var i = 0; i < _spriteRenderers.Length; i++)
                {
                    _originalSpriteColors[i] = _spriteRenderers[i].color;
                }
            }
        }

        private void LateUpdate()
        {
            var color = _lightRuntime.CurrentColor;

            if (_originalGraphicColors != null)
            {
                for (var i = 0; i < _graphics.Length; i++)
                {
                    _graphics[i].color = Color.Lerp(_originalGraphicColors[i], _originalGraphicColors[i] * color, _intensity);
                }
            }

            if (_originalSpriteColors != null)
            {
                for (var i = 0; i < _spriteRenderers.Length; i++)
                {
                    _spriteRenderers[i].color = Color.Lerp(_originalSpriteColors[i], _originalSpriteColors[i] * color, _intensity);
                }
            }
        }
    }
}
