using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.SceneLight;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BalloonParty.UI
{
    /// <summary>Positions a RectTransform around a center to match the current time-of-day angle.</summary>
    internal class TimeOfDayOrbit : MonoBehaviour
    {
        [Tooltip("The center to orbit around. Falls back to the direct parent if unset.")]
        [SerializeField] private RectTransform _center;

        [Tooltip("Images whose color tracks the current time-of-day light color.")]
        [SerializeField] private Graphic[] _tintedGraphics;

        [Inject] private ISceneLightRuntime _lightRuntime;

        private RectTransform _rect;
        private float _radius;

        private void Awake()
        {
            _rect = (RectTransform)transform;

            if (_center == null)
            {
                _center = transform.parent as RectTransform;
            }
        }

        private void Start()
        {
            _radius = _rect.anchoredPosition.magnitude;
        }

        private void LateUpdate()
        {
            var angleDeg = _lightRuntime.CurrentDirection.Angle01() * 360f;
            var angleRad = angleDeg * Mathf.Deg2Rad;

            _rect.anchoredPosition = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * _radius;

            var lightColor = _lightRuntime.CurrentColor;
            for (var i = 0; i < _tintedGraphics.Length; i++)
            {
                _tintedGraphics[i].color = lightColor;
            }
        }
    }
}
