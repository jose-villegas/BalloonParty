using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Item.Paint;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Editor.EffectPreview
{
    /// <summary>
    ///     Preview module for <see cref="PaintSplashView" />.
    /// </summary>
    internal sealed class PaintSplashPreviewModule : IEffectPreviewModule
    {
        private const int GizmoMaxBlobs = 256;

        private static readonly FieldInfo BlobRenderersField =
            typeof(PaintSplashView).GetField("_blobRenderers", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo SplashPrefabField =
            typeof(PaintSplashView).GetField("_splashParticlePrefab", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly PaintSplashView _view;
        private readonly ConfigAssetCache<ItemConfiguration> _itemConfigCache = new();

        private float _previewDirectionDegrees = 90f;
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

        private float FlightDuration => (_ctx?.Settings ?? PreStartSettings)?.Paint.FlightDuration ?? 0.35f;
        private float SpinSpeed => _ctx?.Settings?.Paint.SpinSpeed ?? 720f;
        private float SpreadOffset => (_ctx?.Settings ?? PreStartSettings)?.Paint.SpreadOffset ?? 0f;
        private float SpreadLength => (_ctx?.Settings ?? PreStartSettings)?.Paint.SpreadLength ?? 3f;
        private float SpreadBaseWidth => (_ctx?.Settings ?? PreStartSettings)?.Paint.SpreadBaseWidth ?? 2.5f;
        private float SpreadBlobRadius => (_ctx?.Settings ?? PreStartSettings)?.Paint.SpreadBlobRadius ?? 0.35f;

        public bool UsesColorPicker => true;

        internal PaintSplashPreviewModule(PaintSplashView view)
        {
            _view = view;
        }

        public void DrawGUI()
        {
            EditorGUI.BeginChangeCheck();
            _previewDirectionDegrees = EditorGUILayout.Slider("Direction (deg)", _previewDirectionDegrees, 0f, 360f);
            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }

            EditorGUILayout.LabelField("Offset", $"{SpreadOffset:F2}  (from ItemConfiguration)");
            EditorGUILayout.LabelField("Length", $"{SpreadLength:F2}  (from ItemConfiguration)");
            EditorGUILayout.LabelField("Base Width", $"{SpreadBaseWidth:F2}  (from ItemConfiguration)");
            EditorGUILayout.LabelField("Blob Radius", $"{SpreadBlobRadius:F2}  (from ItemConfiguration)");
            EditorGUILayout.LabelField("Flight Duration", $"{FlightDuration:F2}  (from ItemConfiguration)");
        }

        public void Start(EffectPreviewContext context)
        {
            _ctx = context;
            var blobs = Blobs;
            var origin = _view.transform.position;
            var paint = (context.Settings ?? PreStartSettings)?.Paint;

            _flights = new List<FlightState>();
            _splashes = new List<SplashInstance>();

            if (paint == null || blobs == null || blobs.Length == 0)
            {
                return;
            }

            Vector2 direction = VectorMathExtensions.DirectionFromAngle(_previewDirectionDegrees * Mathf.Deg2Rad);
            var triangle = PaintTriangle.Build(origin, direction, paint);

            // Capped at the prefab's seed blobs; runtime pools the full packed density.
            var packed = new List<Vector2>();
            triangle.PackBlobs(paint.SpreadBlobRadius, blobs.Length, packed);

            for (var i = 0; i < packed.Count && i < blobs.Length; i++)
            {
                var blob = blobs[i];

                if (blob == null)
                {
                    continue;
                }

                var destination = new Vector3(packed[i].x, packed[i].y, origin.z);

                blob.gameObject.SetActive(true);
                blob.transform.position = origin;
                blob.transform.localScale = Vector3.one * (paint.ScaleCurve?.Evaluate(0f) ?? 1f);
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

        // Outlines the paint triangle and packed blob positions, independent of playback.
        public void DrawSceneGizmos()
        {
            var paint = (_ctx?.Settings ?? PreStartSettings)?.Paint;
            if (paint == null || _view == null)
            {
                return;
            }

            var origin = _view.transform.position;
            Vector2 direction = VectorMathExtensions.DirectionFromAngle(_previewDirectionDegrees * Mathf.Deg2Rad);
            var triangle = PaintTriangle.Build(origin, direction, paint);

            SceneDrawingHelper.DrawWorldTriangle(
                ToWorld(triangle.Apex, origin.z),
                ToWorld(triangle.Left, origin.z),
                ToWorld(triangle.Right, origin.z),
                new Color(1f, 0.4f, 0.1f, 0.9f),
                new Color(1f, 0.4f, 0.1f, 0.08f));

            var packed = new List<Vector2>();
            triangle.PackBlobs(paint.SpreadBlobRadius, GizmoMaxBlobs, packed);

            var discColor = new Color(0.2f, 0.8f, 1f, 0.85f);
            foreach (var centre in packed)
            {
                SceneDrawingHelper.DrawWorldDisc(ToWorld(centre, origin.z), paint.SpreadBlobRadius, discColor);
            }
        }

        private static Vector3 ToWorld(Vector2 point, float z)
        {
            return new Vector3(point.x, point.y, z);
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
                    flight.Progress,
                    flight.From,
                    flight.To,
                    _ctx.Settings);
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

            // Prefab edit mode requires objects to live in the prefab stage scene.
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
