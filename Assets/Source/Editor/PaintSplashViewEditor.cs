using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration;
using BalloonParty.Editor.EditorUI;
using BalloonParty.Item.Paint;
using BalloonParty.Shared;
using BalloonParty.Shared.Rendering;
using NaughtyAttributes.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        private float FlightDuration => PaintSettings?.PaintBlobFlightDuration ?? 0.35f;

        private AnimationCurve ArcCurve => PaintSettings?.PaintBlobArcCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
        private AnimationCurve ScaleCurve => PaintSettings?.PaintBlobScaleCurve ?? AnimationCurve.Constant(0f, 1f, 1f);
        private AnimationCurve ShadowScaleCurve => PaintSettings?.PaintBlobShadowScaleCurve ?? AnimationCurve.Constant(0f, 1f, 1f);
        private AnimationCurve SpriteScaleCurve => PaintSettings?.PaintBlobSpriteScaleCurve ?? AnimationCurve.Constant(0f, 1f, 1f);
        private float SpinSpeed => PaintSettings?.PaintBlobSpinSpeed ?? 720f;

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
            if (_flights != null)
            {
                foreach (var flight in _flights)
                {
                    StopBlobParticles(flight);
                }
            }

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
            var shadowScaleCurve = ShadowScaleCurve;
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
                blob.transform.localScale = Vector3.one * scaleCurve.Evaluate(0f);
                blob.transform.rotation = Quaternion.identity;

                blob.SetColor(tint);

                _flights.Add(new FlightState
                {
                    Blob = blob,
                    From = origin,
                    To = destination,
                    Index = i,
                    ArcCurve = arcCurve,
                    ScaleCurve = scaleCurve,
                    ShadowScaleCurve = shadowScaleCurve,
                    SpriteScaleCurve = SpriteScaleCurve,
                    BlobParticles = CollectBlobParticles(blob),
                    TimeOffset = Random.Range(0f, 100f)
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
                    StopBlobParticles(flight);
                    flight.Blob.gameObject.SetActive(false);
                    SpawnEditorSplash(flight.To);
                    continue;
                }

                allLanded = false;

                var pos = Vector3.Lerp(flight.From, flight.To, flight.Progress);
                pos.y += flight.ArcCurve.Evaluate(flight.Progress);
                pos.z -= flight.Index * 0.001f;
                flight.Blob.transform.position = pos;

                flight.Blob.transform.localScale =
                    Vector3.one * flight.ScaleCurve.Evaluate(flight.Progress);

                var blobRenderer = flight.Blob.GetComponentInChildren<Renderer>();
                if (blobRenderer != null)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetFloat(Shader.PropertyToID("_TimeOffset"), flight.TimeOffset);

                    if (flight.ShadowScaleCurve != null)
                    {
                        block.SetFloat(Shader.PropertyToID("_ShadowScale"), flight.ShadowScaleCurve.Evaluate(flight.Progress));
                    }

                    if (flight.SpriteScaleCurve != null)
                    {
                        block.SetFloat(Shader.PropertyToID("_SpriteScale"), flight.SpriteScaleCurve.Evaluate(flight.Progress));
                    }

                    blobRenderer.SetPropertyBlock(block);
                }

                SimulateBlobParticles(flight, delta);

                flight.Blob.transform.Rotate(0f, 0f, -SpinSpeed * delta);
            }

            return !allLanded;
        }

        private static ParticleSystem[] CollectBlobParticles(ColorableRenderer blob)
        {
            var particles = blob.GetComponentsInChildren<ParticleSystem>(true);

            foreach (var ps in particles)
            {
                var main = ps.main;
                main.playOnAwake = false;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }

            return particles;
        }

        private static void SimulateBlobParticles(FlightState flight, float delta)
        {
            if (flight.BlobParticles == null)
            {
                return;
            }

            foreach (var ps in flight.BlobParticles)
            {
                if (ps != null)
                {
                    ps.Simulate(delta, true, false, false);
                }
            }
        }

        private static void StopBlobParticles(FlightState flight)
        {
            if (flight.BlobParticles == null)
            {
                return;
            }

            foreach (var ps in flight.BlobParticles)
            {
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void SpawnEditorSplash(Vector3 position)
        {
            var prefab = SplashPrefab;

            if (prefab == null)
            {
                Debug.LogWarning("[PaintSplash] _splashParticlePrefab is not assigned.", Target);
                return;
            }

            var go = Object.Instantiate(prefab.gameObject, position, Quaternion.identity);
            go.hideFlags = HideFlags.DontSave;
            go.name = $"[EditorSplash] {prefab.name}";

            // In prefab edit mode, objects must live in the prefab stage scene to be visible.
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                SceneManager.MoveGameObjectToScene(go, prefabStage.scene);
            }

            var instance = go.GetComponent<ParticleSystem>();

            if (instance == null)
            {
                DestroyImmediate(go);
                return;
            }

            var seed = (uint)Random.Range(1, int.MaxValue);

            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.playOnAwake = false;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                ps.useAutoRandomSeed = false;
                ps.randomSeed = seed++;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            var rootMain = instance.main;
            rootMain.startColor = _colorPicker.SelectedColor;

            instance.Simulate(0.001f, true, true, false);


            _splashes.Add(new SplashInstance { Particle = instance, Elapsed = 0.001f });
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

                var totalParticles = 0;
                foreach (var ps in splash.Particle.GetComponentsInChildren<ParticleSystem>(true))
                {
                    totalParticles += ps.particleCount;
                }

                if (splash.Elapsed >= 2f && totalParticles == 0)
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
            public ParticleSystem[] BlobParticles;
            public Vector3 From;
            public int Index;
            public bool Landed;
            public float Progress;
            public AnimationCurve ScaleCurve;
            public AnimationCurve ShadowScaleCurve;
            public AnimationCurve SpriteScaleCurve;
            public float TimeOffset;
            public Vector3 To;
        }

        private class SplashInstance
        {
            public float Elapsed;
            public ParticleSystem Particle;
        }
    }
}
