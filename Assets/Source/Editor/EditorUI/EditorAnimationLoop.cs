using System;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.EditorUI
{
    /// <summary>
    ///     Manages a play/pause/stop animation loop driven by
    ///     <see cref="EditorApplication.update"/>. Tracks delta time,
    ///     pause state, and playback speed. The caller supplies a tick
    ///     callback that returns <c>false</c> when the animation is complete.
    /// </summary>
    internal sealed class EditorAnimationLoop
    {
        private double _lastEditorTime;
        private Func<float, bool> _onTick;
        private Action _onComplete;
        private Action _onRepaint;

        /// <summary>Whether the loop is currently running (playing or paused).</summary>
        internal bool IsPlaying { get; private set; }

        internal bool IsPaused { get; private set; }

        /// <summary>Playback speed multiplier. Default 1.</summary>
        internal float TimeScale { get; set; } = 1f;

        /// <summary>
        ///     Starts the animation loop.
        /// </summary>
        /// <param name="onTick">
        ///     Called each editor frame with scaled delta time.
        ///     Return <c>true</c> to keep running, <c>false</c> to auto-stop.
        /// </param>
        /// <param name="onComplete">Called when the loop stops (auto or manual).</param>
        /// <param name="onRepaint">Called after each tick to repaint views (e.g. SceneView, Inspector).</param>
        internal void Start(Func<float, bool> onTick, Action onComplete = null, Action onRepaint = null)
        {
            if (IsPlaying)
            {
                return;
            }

            _onTick = onTick;
            _onComplete = onComplete;
            _onRepaint = onRepaint;

            IsPlaying = true;
            IsPaused = false;
            _lastEditorTime = EditorApplication.timeSinceStartup;

            EditorApplication.update += Tick;
        }

        /// <summary>Stops the loop and invokes the completion callback.</summary>
        internal void Stop()
        {
            if (!IsPlaying)
            {
                return;
            }

            EditorApplication.update -= Tick;
            IsPlaying = false;
            IsPaused = false;

            _onComplete?.Invoke();
            _onTick = null;
            _onComplete = null;
            _onRepaint = null;
        }

        /// <summary>Toggles pause. Prevents delta spike on resume.</summary>
        internal void TogglePause()
        {
            if (!IsPlaying)
            {
                return;
            }

            IsPaused = !IsPaused;

            if (!IsPaused)
            {
                _lastEditorTime = EditorApplication.timeSinceStartup;
            }
        }

        /// <summary>
        ///     Draws Play/Pause + Stop buttons. Returns without drawing if
        ///     <paramref name="onPlay"/> is null.
        /// </summary>
        /// <param name="onPlay">Called when the user clicks Play (loop not yet started).</param>
        internal void DrawControls(Action onPlay)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var playPauseLabel = !IsPlaying
                    ? "▶  Play"
                    : IsPaused
                        ? "▶  Resume"
                        : "⏸  Pause";

                if (GUILayout.Button(playPauseLabel, GUILayout.Height(28)))
                {
                    if (!IsPlaying)
                    {
                        onPlay?.Invoke();
                    }
                    else
                    {
                        TogglePause();
                    }
                }

                using (new EditorGUI.DisabledScope(!IsPlaying))
                {
                    if (GUILayout.Button("⏹  Stop", GUILayout.Height(28)))
                    {
                        Stop();
                    }
                }
            }

            if (IsPlaying)
            {
                var status = IsPaused ? "Paused" : "Playing…";
                EditorGUILayout.HelpBox($"Status: {status}  |  Speed: {TimeScale:F2}×",
                    MessageType.Info);
            }
        }

        /// <summary>Draws a playback speed slider. Call from <c>OnInspectorGUI</c>.</summary>
        internal void DrawSpeedSlider(
            string label = "Playback Speed",
            float min = 0.05f,
            float max = 3f)
        {
            TimeScale = EditorGUILayout.Slider(label, TimeScale, min, max);
        }

        private void Tick()
        {
            if (!IsPlaying)
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (IsPaused)
            {
                _lastEditorTime = EditorApplication.timeSinceStartup;
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var delta = (float)(now - _lastEditorTime) * TimeScale;
            _lastEditorTime = now;

            var keepRunning = _onTick?.Invoke(delta) ?? false;

            _onRepaint?.Invoke();

            if (!keepRunning)
            {
                Stop();
            }
        }
    }
}
