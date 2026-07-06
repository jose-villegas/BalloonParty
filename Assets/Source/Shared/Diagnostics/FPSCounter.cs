using UnityEngine;

namespace BalloonParty.Shared.Diagnostics
{
    internal sealed class FPSCounter : MonoBehaviour
    {
        private const float UpdateInterval = 0.5f;

        [SerializeField] private int _fontSize = 24;
        [SerializeField] private Color _goodColor = Color.green;
        [SerializeField] private Color _warnColor = Color.yellow;
        [SerializeField] private Color _badColor = Color.red;
        [SerializeField] private int _warnThreshold = 45;
        [SerializeField] private int _badThreshold = 30;

        private float _elapsed;
        private int _frames;
        private int _currentFps;
        private float _worstDelta;
        private int _worstFps;
        private GUIStyle _style;
        private GUIContent _labelContent;
        private Vector2 _labelSize;
        private int _labelFps = int.MinValue;
        private int _labelWorst = int.MinValue;

        private void Update()
        {
            _frames++;
            _elapsed += Time.unscaledDeltaTime;

            if (Time.unscaledDeltaTime > _worstDelta)
            {
                _worstDelta = Time.unscaledDeltaTime;
            }

            if (_elapsed >= UpdateInterval)
            {
                _currentFps = Mathf.RoundToInt(_frames / _elapsed);
                _worstFps = Mathf.RoundToInt(1f / _worstDelta);
                _frames = 0;
                _elapsed = 0f;
                _worstDelta = 0f;
            }
        }

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = _fontSize,
                    fontStyle = FontStyle.Bold
                };
            }

            _style.normal.textColor = _currentFps >= _warnThreshold
                ? _goodColor
                : _currentFps >= _badThreshold
                    ? _warnColor
                    : _badColor;

            // Rebuild only on change — per-OnGUI allocs would pollute the GC profile this overlay reads.
            if (_labelFps != _currentFps || _labelWorst != _worstFps)
            {
                _labelFps = _currentFps;
                _labelWorst = _worstFps;
                _labelContent = new GUIContent($"{_currentFps} FPS (low {_worstFps})");
                _labelSize = _style.CalcSize(_labelContent);
            }

            var rect = new Rect(8, 8, _labelSize.x + 4, _labelSize.y);
            GUI.Label(rect, _labelContent, _style);
        }
    }
}
