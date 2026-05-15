using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BalloonParty.Configuration;
using BalloonParty.Item.Paint;
using BalloonParty.Shared;
using NaughtyAttributes.Editor;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    [CustomEditor(typeof(PaintSplashView))]
    public class PaintSplashViewEditor : NaughtyInspector
    {
        private static readonly FieldInfo BlobRenderersField =
            typeof(PaintSplashView).GetField("_blobRenderers", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo SplashParticlesField =
            typeof(PaintSplashView).GetField("_splashParticles", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo ArcCurveField =
            typeof(PaintSplashView).GetField("_arcCurve", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo ScaleCurveField =
            typeof(PaintSplashView).GetField("_scaleCurve", BindingFlags.NonPublic | BindingFlags.Instance);

        private int _blobCount = 6;
        private List<FlightState> _flights;
        private GameConfiguration _gameConfig;
        private bool _isPaused;
        private bool _isPlaying;
        private ItemConfiguration _itemConfig;
        private double _lastEditorTime;
        private GamePalette _palette;
        private int _selectedColorIndex;
        private bool _splashPhase;
        private float _timeScale = 1f;

        private PaintSplashView Target => (PaintSplashView)target;
        private SpriteRenderer[] Blobs => (SpriteRenderer[])BlobRenderersField.GetValue(Target);
        private ParticleSystem[] Splashes => (ParticleSystem[])SplashParticlesField.GetValue(Target);
        private AnimationCurve ArcCurve => (AnimationCurve)ArcCurveField.GetValue(Target);
        private AnimationCurve ScaleCurve => (AnimationCurve)ScaleCurveField.GetValue(Target);

        private ItemSettings PaintSettings
        {
            get
            {
                EnsureItemConfig();
                return _itemConfig != null ? _itemConfig[ItemType.Paint] : null;
            }
        }

        private float ArcHeight => PaintSettings?.PaintBlobArcHeight ?? 0.6f;
        private float BlobScale => PaintSettings?.PaintBlobStartScale ?? 0.5f;
        private float FlightDuration => PaintSettings?.PaintBlobFlightDuration ?? 0.35f;

        private float FlightRadius
        {
            get
            {
                EnsureGameConfig();

                if (_gameConfig == null)
                {
                    return 1f;
                }

                // Hex grid diagonal neighbor distance: half-cell horizontal shift + one row vertical.
                var sep = _gameConfig.SlotSeparation;
                return Mathf.Sqrt(sep.x * sep.x + sep.y * sep.y);
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Paint Splash Preview", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(_isPlaying))
            {
                DrawPaletteColorPicker();
                _blobCount = EditorGUILayout.IntSlider("Blob Count", _blobCount, 1, 6);
                EditorGUILayout.LabelField("Flight Duration", $"{FlightDuration:F2}  (from ItemConfiguration)");
                EditorGUILayout.LabelField("Arc Height", $"{ArcHeight:F2}  (from ItemConfiguration)");
                EditorGUILayout.LabelField("Blob Scale", $"{BlobScale:F2}  (from ItemConfiguration)");
                EditorGUILayout.LabelField("Flight Radius", $"{FlightRadius:F2}  (from SlotSeparation)");
            }

            _timeScale = EditorGUILayout.Slider("Playback Speed", _timeScale, 0.05f, 3f);

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                var playPauseLabel = !_isPlaying
                    ? "▶  Play"
                    : _isPaused
                        ? "▶  Resume"
                        : "⏸  Pause";

                if (GUILayout.Button(playPauseLabel, GUILayout.Height(28)))
                {
                    if (!_isPlaying)
                    {
                        StartPreview();
                    }
                    else
                    {
                        TogglePause();
                    }
                }

                using (new EditorGUI.DisabledScope(!_isPlaying))
                {
                    if (GUILayout.Button("⏹  Stop", GUILayout.Height(28)))
                    {
                        StopPreview();
                    }
                }
            }

            if (_isPlaying)
            {
                var status = _isPaused ? "Paused" : "Playing…";
                EditorGUILayout.HelpBox($"Status: {status}  |  Speed: {_timeScale:F2}×", MessageType.Info);
            }
        }

        protected override void OnDisable()
        {
            if (_isPlaying)
            {
                CleanUp();
            }

            base.OnDisable();
        }

        private bool AnySplashAlive()
        {
            if (_flights == null)
            {
                return false;
            }

            foreach (var flight in _flights)
            {
                if (!flight.Landed || flight.Splash == null)
                {
                    continue;
                }

                if (flight.SplashElapsed < 1f || flight.Splash.particleCount > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void CleanUp()
        {
            EditorApplication.update -= EditorTick;

            var blobs = Blobs;
            var particles = Splashes;

            if (blobs != null)
            {
                foreach (var blob in blobs)
                {
                    if (blob != null)
                    {
                        blob.enabled = false;
                    }
                }
            }

            if (particles != null)
            {
                foreach (var particle in particles)
                {
                    if (particle != null)
                    {
                        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }

            _flights = null;
            _isPlaying = false;
            _isPaused = false;
            _splashPhase = false;

            SceneView.RepaintAll();
            Repaint();
        }

        private void DrawPaletteColorPicker()
        {
            EnsurePalette();

            if (_palette == null || _palette.Colors.Length == 0)
            {
                EditorGUILayout.HelpBox("No GamePalette asset found.", MessageType.Warning);
                return;
            }

            var names = _palette.Colors.Select(c => new GUIContent(c.Name)).ToArray();
            var rect = EditorGUILayout.GetControlRect();

            const float swatchSize = 16f;
            const float swatchSpacing = 4f;
            var popupRect = new Rect(rect.x, rect.y, rect.width - swatchSize - swatchSpacing, rect.height);
            var swatchRect = new Rect(rect.xMax - swatchSize, rect.y, swatchSize, swatchSize);

            _selectedColorIndex = Mathf.Clamp(_selectedColorIndex, 0, names.Length - 1);
            _selectedColorIndex = EditorGUI.Popup(popupRect, new GUIContent("Tint"), _selectedColorIndex, names);

            var color = _palette.Colors[_selectedColorIndex].Color;
            EditorGUI.DrawRect(swatchRect, color);
            EditorGUI.DrawRect(new Rect(swatchRect.x - 1, swatchRect.y - 1, swatchRect.width + 2, 1), Color.black);
            EditorGUI.DrawRect(new Rect(swatchRect.x - 1, swatchRect.yMax, swatchRect.width + 2, 1), Color.black);
            EditorGUI.DrawRect(new Rect(swatchRect.x - 1, swatchRect.y, 1, swatchRect.height), Color.black);
            EditorGUI.DrawRect(new Rect(swatchRect.xMax, swatchRect.y, 1, swatchRect.height), Color.black);
        }

        private void EditorTick()
        {
            if (!_isPlaying)
            {
                EditorApplication.update -= EditorTick;
                return;
            }

            if (_isPaused)
            {
                _lastEditorTime = EditorApplication.timeSinceStartup;
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var delta = (float)(now - _lastEditorTime) * _timeScale;
            _lastEditorTime = now;

            if (!_splashPhase)
            {
                TickFlights(delta);
            }

            TickParticles(delta);

            if (_splashPhase && !AnySplashAlive())
            {
                CleanUp();
                return;
            }

            SceneView.RepaintAll();
            Repaint();
        }

        private void EnsureGameConfig()
        {
            if (_gameConfig != null)
            {
                return;
            }

            var guids = AssetDatabase.FindAssets("t:GameConfiguration");
            if (guids.Length > 0)
            {
                _gameConfig = AssetDatabase.LoadAssetAtPath<GameConfiguration>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        private void EnsureItemConfig()
        {
            if (_itemConfig != null)
            {
                return;
            }

            var guids = AssetDatabase.FindAssets("t:ItemConfiguration");
            if (guids.Length > 0)
            {
                _itemConfig = AssetDatabase.LoadAssetAtPath<ItemConfiguration>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        private void EnsurePalette()
        {
            if (_palette != null)
            {
                return;
            }

            var guids = AssetDatabase.FindAssets("t:GamePalette");
            if (guids.Length > 0)
            {
                _palette = AssetDatabase.LoadAssetAtPath<GamePalette>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        private Color GetSelectedColor()
        {
            EnsurePalette();

            if (_palette == null || _palette.Colors.Length == 0)
            {
                return Color.red;
            }

            var index = Mathf.Clamp(_selectedColorIndex, 0, _palette.Colors.Length - 1);
            return _palette.Colors[index].Color;
        }

        private void StartPreview()
        {
            if (_isPlaying)
            {
                return;
            }

            var blobs = Blobs;
            var particles = Splashes;
            var arcCurve = ArcCurve;
            var scaleCurve = ScaleCurve;
            var origin = Target.transform.position;
            var tint = GetSelectedColor();
            var count = Mathf.Clamp(_blobCount, 1, 6);
            var radius = FlightRadius;

            _flights = new List<FlightState>();

            for (var i = 0; i < count; i++)
            {
                var blob = blobs != null && i < blobs.Length ? blobs[i] : null;
                var splash = particles != null && i < particles.Length ? particles[i] : null;

                if (blob == null)
                {
                    continue;
                }

                var angle = 360f / count * i * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                var destination = origin + direction * radius;

                blob.enabled = true;
                blob.transform.position = origin;
                blob.transform.localScale = Vector3.one * BlobScale;

                var block = new MaterialPropertyBlock();
                blob.GetPropertyBlock(block);
                block.SetColor(Shader.PropertyToID("_Color"), tint);
                block.SetFloat(Shader.PropertyToID("_TimeOffset"), Random.Range(0f, 100f));
                blob.SetPropertyBlock(block);

                if (splash != null)
                {
                    var main = splash.main;
                    main.startColor = tint;
                    splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                _flights.Add(new FlightState
                {
                    Blob = blob,
                    Splash = splash,
                    From = origin,
                    To = destination,
                    ArcCurve = arcCurve,
                    ScaleCurve = scaleCurve
                });
            }

            _isPlaying = true;
            _isPaused = false;
            _splashPhase = false;
            _lastEditorTime = EditorApplication.timeSinceStartup;

            EditorApplication.update += EditorTick;
        }

        private void StopPreview()
        {
            if (!_isPlaying)
            {
                return;
            }

            CleanUp();
        }

        private void TickFlights(float delta)
        {
            var allLanded = true;

            foreach (var flight in _flights)
            {
                if (flight.Landed)
                {
                    continue;
                }

                flight.Progress += delta / Mathf.Max(FlightDuration, 0.01f);

                if (flight.Progress >= 1f)
                {
                    flight.Progress = 1f;
                    flight.Landed = true;
                    flight.Blob.enabled = false;

                    if (flight.Splash != null)
                    {
                        flight.Splash.transform.position = flight.To;
                        flight.SplashElapsed = 0f;
                    }

                    continue;
                }

                allLanded = false;

                flight.Blob.transform.position = CurveUtility.LerpWithVerticalCurve(
                    flight.From, flight.To, flight.Progress, ArcHeight, flight.ArcCurve);

                flight.Blob.transform.localScale =
                    Vector3.one * CurveUtility.SampleMultiplied(flight.Progress, BlobScale, flight.ScaleCurve);
            }

            if (allLanded)
            {
                _splashPhase = true;
            }
        }

        private void TickParticles(float delta)
        {
            if (_flights == null)
            {
                return;
            }

            foreach (var flight in _flights)
            {
                if (!flight.Landed || flight.Splash == null)
                {
                    continue;
                }

                flight.SplashElapsed += delta;
                flight.Splash.Simulate(flight.SplashElapsed, true, true, false);
            }
        }

        private void TogglePause()
        {
            if (!_isPlaying)
            {
                return;
            }

            _isPaused = !_isPaused;

            if (!_isPaused)
            {
                // Prevents a large delta spike from the time spent paused.
                _lastEditorTime = EditorApplication.timeSinceStartup;
            }

            Repaint();
        }

        private class FlightState
        {
            public AnimationCurve ArcCurve;
            public SpriteRenderer Blob;
            public Vector3 From;
            public bool Landed;
            public float Progress;
            public AnimationCurve ScaleCurve;
            public ParticleSystem Splash;
            public float SplashElapsed;
            public Vector3 To;
        }
    }
}
