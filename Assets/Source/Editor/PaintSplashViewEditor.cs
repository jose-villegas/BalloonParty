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

        private static readonly FieldInfo SplashPrefabField =
            typeof(PaintSplashView).GetField("_splashParticlePrefab", BindingFlags.NonPublic | BindingFlags.Instance);

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
        private List<SplashInstance> _splashes;
        private float _timeScale = 1f;

        private PaintSplashView Target => (PaintSplashView)target;
        private ColorableRenderer[] Blobs => (ColorableRenderer[])BlobRenderersField.GetValue(Target);
        private ParticleSystem SplashPrefab => (ParticleSystem)SplashPrefabField.GetValue(Target);
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

        private void CleanUp()
        {
            EditorApplication.update -= EditorTick;

            var blobs = Blobs;

            if (blobs != null)
            {
                foreach (var blob in blobs)
                {
                    if (blob != null)
                    {
                        blob.gameObject.SetActive(false);
                    }
                }
            }

            _flights = null;
            _isPlaying = false;
            _isPaused = false;

            DestroySplashes();

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

            var flying = TickFlights(delta);
            var splashing = TickSplashes(delta);

            if (!flying && !splashing)
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

                if (blob == null)
                {
                    continue;
                }

                var angle = 360f / count * i * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                var destination = origin + direction * radius;

                blob.gameObject.SetActive(true);
                blob.transform.position = origin;
                blob.transform.localScale = Vector3.one * BlobScale;

                blob.SetColor(tint);

                var blobRenderer = blob.GetComponent<Renderer>();
                if (blobRenderer != null)
                {
                    var block = new MaterialPropertyBlock();
                    blobRenderer.GetPropertyBlock(block);
                    block.SetFloat(Shader.PropertyToID("_TimeOffset"), Random.Range(0f, 100f));
                    blobRenderer.SetPropertyBlock(block);
                }

                _flights.Add(new FlightState
                {
                    Blob = blob,
                    From = origin,
                    To = destination,
                    ArcCurve = arcCurve,
                    ScaleCurve = scaleCurve
                });
            }

            _isPlaying = true;
            _isPaused = false;
            _splashes = new List<SplashInstance>();
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

        private bool TickFlights(float delta)
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
                    flight.Blob.gameObject.SetActive(false);
                    Debug.Log($"Blob {_flights.IndexOf(flight)} landed at {flight.To}. SplashPrefab: {SplashPrefab}");
                    SpawnEditorSplash(flight.To);
                    continue;
                }

                allLanded = false;

                flight.Blob.transform.position = CurveUtility.LerpWithVerticalCurve(
                    flight.From, flight.To, flight.Progress, ArcHeight, flight.ArcCurve);

                flight.Blob.transform.localScale =
                    Vector3.one * CurveUtility.SampleMultiplied(flight.Progress, BlobScale, flight.ScaleCurve);
            }

            return !allLanded;
        }

        private void SpawnEditorSplash(Vector3 position)
        {
            var prefab = SplashPrefab;

            if (prefab == null)
            {
                Debug.LogWarning("PaintSplashView: _splashParticlePrefab is not assigned.", Target);
                return;
            }

            var instance = Object.Instantiate(prefab, position, Quaternion.identity);
            instance.gameObject.hideFlags = HideFlags.DontSave;

            var main = instance.main;
            main.startColor = GetSelectedColor();
            main.playOnAwake = false;
            main.simulationSpeed = 1f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            instance.useAutoRandomSeed = true;
            instance.Simulate(0.016f, true, true, false);

            _splashes.Add(new SplashInstance { Particle = instance, Elapsed = 0.016f });
        }

        private bool TickSplashes(float delta)
        {
            if (_splashes == null || _splashes.Count == 0)
            {
                return false;
            }

            var anyAlive = false;

            for (var i = _splashes.Count - 1; i >= 0; i--)
            {
                var splash = _splashes[i];
                splash.Elapsed += delta;
                splash.Particle.Simulate(splash.Elapsed, true, true, false);

                if (splash.Elapsed >= 1f && splash.Particle.particleCount == 0)
                {
                    Object.DestroyImmediate(splash.Particle.gameObject);
                    _splashes.RemoveAt(i);
                    continue;
                }

                anyAlive = true;
            }

            return anyAlive;
        }

        private void DestroySplashes()
        {
            if (_splashes == null)
            {
                return;
            }

            foreach (var splash in _splashes)
            {
                if (splash.Particle != null)
                {
                    Object.DestroyImmediate(splash.Particle.gameObject);
                }
            }

            _splashes = null;
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
            public ColorableRenderer Blob;
            public Vector3 From;
            public bool Landed;
            public float Progress;
            public AnimationCurve ScaleCurve;
            public Vector3 To;
        }

        private class SplashInstance
        {
            public float Elapsed;
            public ParticleSystem Particle;
        }
    }
}
