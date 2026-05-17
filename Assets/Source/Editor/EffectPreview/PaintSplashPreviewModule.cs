using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration;
using BalloonParty.Editor.EditorUI;
using BalloonParty.Item.Paint;
using BalloonParty.Shared.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BalloonParty.Editor.EffectPreview
{
    /// <summary>
    ///     Preview module for <see cref="PaintSplashView" />. Animates blob
    ///     renderers along arc paths with particles, shadow/sprite scale curves,
    ///     spin, and splash particle spawning in prefab-stage-aware mode.
    /// </summary>
    internal sealed class PaintSplashPreviewModule : IEffectPreviewModule
    {
        private static readonly FieldInfo BlobRenderersField =
            typeof(PaintSplashView).GetField("_blobRenderers", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo SplashPrefabField =
            typeof(PaintSplashView).GetField("_splashParticlePrefab", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly PaintSplashView _view;
        private readonly ConfigAssetCache<GameConfiguration> _gameConfigCache = new();
        private readonly ConfigAssetCache<ItemConfiguration> _itemConfigCache = new();

        private int _blobCount = 6;
        private List<FlightState> _flights;
        private List<SplashInstance> _splashes;
        private EffectPreviewContext _ctx;

        private ColorableRenderer[] Blobs => (ColorableRenderer[])BlobRenderersField.GetValue(_view);
        private ParticleSystem SplashPrefab => (ParticleSystem)SplashPrefabField.GetValue(_view);

        private ItemSettings PreStartSettings
        {
            get
            {
                var config = _itemConfigCache.Value;
                return config != null ? config[ItemType.Paint] : null;
            }
        }

        private float FlightDuration => (_ctx?.Settings ?? PreStartSettings)?.PaintBlobFlightDuration ?? 0.35f;
        private float SpinSpeed => _ctx?.Settings?.PaintBlobSpinSpeed ?? 720f;

        internal PaintSplashPreviewModule(PaintSplashView view)
        {
            _view = view;
        }

        public bool UsesColorPicker => true;

        private float FlightRadius
        {
            get
            {
                var config = _ctx?.GameConfig ?? _gameConfigCache.Value;

                if (config == null)
                {
                    return 1f;
                }

                var sep = config.SlotSeparation;
                return Mathf.Sqrt((sep.x * sep.x) + (sep.y * sep.y));
            }
        }

        public void DrawGUI()
        {
            _blobCount = EditorGUILayout.IntSlider("Blob Count", _blobCount, 1, 6);
            EditorGUILayout.LabelField("Flight Duration", $"{FlightDuration:F2}  (from ItemConfiguration)");
            EditorGUILayout.LabelField("Flight Radius", $"{FlightRadius:F2}  (from SlotSeparation)");
        }

        public void Start(EffectPreviewContext context)
        {
            _ctx = context;
            var blobs = Blobs;
            var origin = _view.transform.position;
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
                blob.transform.localScale = Vector3.one *
                    (context.Settings?.PaintBlobScaleCurve?.Evaluate(0f) ?? 1f);
                blob.transform.rotation = Quaternion.identity;

                blob.SetColor(context.Tint);

                var childRenderer = blob.GetComponentInChildren<SpriteRenderer>();
                if (childRenderer != null)
                {
                    childRenderer.sortingOrder = i;
                }

                _flights.Add(new FlightState
                {
                    Blob = blob,
                    From = origin,
                    To = destination,
                    BlobParticles = CollectBlobParticles(blob),
                    TimeOffset = Random.Range(0f, 100f)
                });
            }
        }

        public bool Tick(float delta)
        {
            var flying = TickFlights(delta);
            var splashing = TickSplashes(delta);
            return flying || splashing;
        }

        public void CleanUp()
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

                var snapshot = PaintSplashView.ComputeBlobFlight(
                    flight.Progress, flight.From, flight.To, _ctx.Settings);
                flight.Blob.transform.position = snapshot.Position;
                flight.Blob.transform.localScale = Vector3.one * snapshot.Scale;

                var blobRenderer = flight.Blob.GetComponentInChildren<Renderer>();
                if (blobRenderer != null)
                {
                    PaintSplashView.ApplyBlobMaterial(blobRenderer, flight.TimeOffset, snapshot);
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
                Debug.LogWarning("[PaintSplash] _splashParticlePrefab is not assigned.", _view);
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
                Object.DestroyImmediate(go);
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
            rootMain.startColor = _ctx.Tint;

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

        private class FlightState
        {
            public ColorableRenderer Blob;
            public ParticleSystem[] BlobParticles;
            public Vector3 From;
            public bool Landed;
            public float Progress;
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



