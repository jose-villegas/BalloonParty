using BalloonParty.Shared.SceneLight;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BalloonParty.UI
{
    /// <summary>Tints a collection of graphics to match the current time-of-day light color.</summary>
    internal class TimeOfDayTint : MonoBehaviour
    {
        [SerializeField] private Graphic[] _graphics;
        [Tooltip("How strongly the time-of-day color overrides the original. 0 = no tint, 1 = full replace.")]
        [SerializeField] [Range(0f, 1f)] private float _intensity = 1f;

        [Inject] private ISceneLightRuntime _lightRuntime;

        private Color[] _originalColors;

        private void Start()
        {
            _originalColors = new Color[_graphics.Length];
            for (var i = 0; i < _graphics.Length; i++)
            {
                _originalColors[i] = _graphics[i].color;
            }
        }

        private void LateUpdate()
        {
            var color = _lightRuntime.CurrentColor;
            for (var i = 0; i < _graphics.Length; i++)
            {
                _graphics[i].color = Color.Lerp(_originalColors[i], color, _intensity);
            }
        }
    }
}
