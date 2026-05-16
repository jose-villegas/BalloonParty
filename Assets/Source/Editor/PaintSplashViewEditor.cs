using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration;
using BalloonParty.Editor.EditorUI;
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

        private readonly ConfigAssetCache<GameConfiguration> _gameConfigCache = new();
        private readonly ConfigAssetCache<ItemConfiguration> _itemConfigCache = new();
        private readonly PaletteColorPicker _colorPicker = new();
        private readonly EditorAnimationLoop _animLoop = new();

        private int _blobCount = 6;
        private List<FlightState> _flights;
        private List<SplashInstance> _splashes;

        private PaintSplashView Target => (PaintSplashView)target;
        private ColorableRenderer[] Blobs => (ColorableRenderer[])BlobRenderersField.GetValue(Target);
        private ParticleSystem SplashPrefab => (ParticleSystem)SplashPrefabField.GetValue(Target);

        private ItemSettings PaintSettings
        {
            get
            {
                var config = _itemConfigCache.Value;
                return config != null ? config[ItemType.Paint] : null;
            }
        }

        private float ArcHeight => PaintSettings?.PaintBlobArcHeight ?? 0.6f;
        private float BlobScale => PaintSettings?.PaintBlobStartScale ?? 0.5f;
        private float FlightDuration => PaintSettings?.PaintBlobFlightDuration ?? 0.35f;

        private AnimationCurve ArcCurve => PaintSettings?.PaintBlobArcCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
        private AnimationCurve ScaleCurve => PaintSettings?.PaintBlobScaleCurve ?? AnimationCurve.Constant(0f, 1f, 1f);

        private float FlightRadius
        {
            get
            {
                var config = _gameConfigCache.Value;

                if (config == null)
                {
                    return 1f;
                }

                var sep = config.SlotSeparation;
                return Mathf.Sqrt((sep.x * sep.x) + (sep.y * sep.y));
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Paint Splash Preview", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(_animLoop.IsPlaying))
            {
                _colorPicker.DrawLayout();
                _blobCount = EditorGUILayout.IntSlider("Blob Count", _blobCount, 1, 6);
                EditorGUILayout.LabelField("Flight Duration", $"{FlightDuration:F2}  (from ItemConfiguration)");
                EditorGUILayout.LabelField("Arc Height", $"{ArcHeight:F2}  (from ItemConfiguration)");
                EditorGUILayout.LabelField("Blob Scale", $"{BlobScale:F2}  (from ItemConfiguration)");
                EditorGUILayout.LabelField("Flight Radius", $"{FlightRadius:F2}  (from SlotSeparation)");
            }

            _animLoop.DrawSpeedSlider();

            EditorGUILayout.Space(4);

            _animLoop.DrawControls(StartPreview);
        }

        protected override void OnDisable()
        {
            if (_animLoop.IsPlaying)
            {
                _animLoop.Stop();
            }

            base.OnDisable();
        }

        private void CleanUp()
        {
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

            DestroySplashes();

            SceneView.RepaintAll();
            Repaint();
        }

        private void StartPreview()
        {
            var blobs = Blobs;
            var arcCurve = ArcCurve;
            var scaleCurve = ScaleCurve;
            var origin = Target.transform.position;
            var tint = _colorPicker.SelectedColor;
            var count = Mathf.Clamp(_blobCount, 1, 6);
            var radius = FlightRadius;

            _flights = new List<FlightState>();
            _splashes = new List<SplashInstance>();

            for (var i = 0; i < count; i++)
            {
                var blob = blobs != null && i < blobs.Length ? blobs[i] : null;

                if (blob == null)
                {
                    continue;
                }

                var angle = 360f / count * i * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                var destination = origin + (direction * radius);

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

            _animLoop.Start(
                OnAnimTick,
                CleanUp,
                () =>
                {
                    SceneView.RepaintAll();
                    Repaint();
                });
        }

        private bool OnAnimTick(float delta)
        {
            var flying = TickFlights(delta);
            var splashing = TickSplashes(delta);
            return flying || splashing;
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
                    SpawnEditorSplash(flight.To);
                    continue;
                }

                allLanded = false;

                flight.Blob.transform.position = CurveUtility.LerpWithVerticalCurve(
                    flight.From,
                    flight.To,
                    flight.Progress,
                    ArcHeight,
                    flight.ArcCurve);

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

            var instance = Instantiate(prefab, position, Quaternion.identity);
            instance.gameObject.hideFlags = HideFlags.DontSave;

            var main = instance.main;
            main.startColor = _colorPicker.SelectedColor;
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
                    DestroyImmediate(splash.Particle.gameObject);
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
                    DestroyImmediate(splash.Particle.gameObject);
                }
            }

            _splashes = null;
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
