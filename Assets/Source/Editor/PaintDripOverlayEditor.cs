using BalloonParty.Balloon.View;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Adds an in-editor preview to <see cref="PaintDripOverlay" />: scrub the drip with a slider or
    ///     press Play to run it over the component's own duration/curve, without entering play mode. Drives
    ///     the same MPB the runtime uses, so it's the tuning surface for the PaintDrip material.
    /// </summary>
    [CustomEditor(typeof(PaintDripOverlay))]
    internal sealed class PaintDripOverlayEditor : UnityEditor.Editor
    {
        private Color _previewColor = new(0.95f, 0.25f, 0.2f, 1f);
        private float _previewProgress;
        private float _seed = 12.3f;
        private bool _playing;
        private float _elapsed;
        private double _lastTime;

        private void OnDisable()
        {
            StopPlaying();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Drip Preview", EditorStyles.boldLabel);

            var overlay = (PaintDripOverlay)target;

            _previewColor = EditorGUILayout.ColorField("Paint Color", _previewColor);

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                _previewProgress = EditorGUILayout.Slider("Progress", _previewProgress, 0f, 1f);
                if (check.changed && !_playing)
                {
                    overlay.EditorPreview(_previewProgress, _previewColor, _seed);
                    SceneView.RepaintAll();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_playing ? "Stop" : "Play"))
                {
                    if (_playing)
                    {
                        StopPlaying();
                    }
                    else
                    {
                        StartPlaying();
                    }
                }

                if (GUILayout.Button("Reseed"))
                {
                    _seed = Random.Range(0f, 100f);
                    overlay.EditorPreview(_previewProgress, _previewColor, _seed);
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("Hide"))
                {
                    StopPlaying();
                    overlay.Stop();
                    SceneView.RepaintAll();
                }
            }
        }

        private void StartPlaying()
        {
            _playing = true;
            _elapsed = 0f;
            _lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        private void StopPlaying()
        {
            if (!_playing)
            {
                return;
            }

            _playing = false;
            EditorApplication.update -= Tick;
        }

        private void Tick()
        {
            if (target == null)
            {
                StopPlaying();
                return;
            }

            var overlay = (PaintDripOverlay)target;
            var now = EditorApplication.timeSinceStartup;
            _elapsed += (float)(now - _lastTime);
            _lastTime = now;

            var duration = Mathf.Max(overlay.EditorDuration, 0.001f);
            var t = Mathf.Clamp01(_elapsed / duration);
            _previewProgress = overlay.EditorProgressCurve?.Evaluate(t) ?? t;

            overlay.EditorPreview(_previewProgress, _previewColor, _seed);
            SceneView.RepaintAll();
            Repaint();

            if (t >= 1f)
            {
                StopPlaying();
            }
        }
    }
}
