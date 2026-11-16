using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     The nuke's mushroom cloud (PLAN-NukeBalloon) on the <c>BalloonParty/Sprite/NukeCloud</c> material, seen from
    ///     above as the board is: one square quad centred where the nuke went off, its shader drawing the cap, its
    ///     shadow and the dust ring from the progress pushed here (0 at the blast, 1 when it has eroded away) over
    ///     <see cref="INukeBlastSettings.CloudSeconds" />. It runs on scaled time, so a pause holds it, and outlives
    ///     the blast that started it.
    /// </summary>
    internal sealed class NukeCloudView : MonoBehaviour, INukeCloudView
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");

        private INukeBlastSettings _settings;
        private Mesh _quad;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private float _elapsed;
        private bool _playing;

        private void Update()
        {
            if (!_playing)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(_elapsed / _settings.CloudSeconds);
            SetProgress(progress);
            if (progress >= 1f)
            {
                Stop();
            }
        }

        private void OnDestroy()
        {
            if (_quad != null)
            {
                Destroy(_quad);
            }
        }

        // Built here, not in Awake: the settings arrive after AddComponent's Awake.
        [Inject]
        internal void Construct(IBalloonsConfiguration balloons)
        {
            _settings = balloons.NukeBlast;
            _quad = BuildQuad(_settings.CloudReach);
            gameObject.AddComponent<MeshFilter>().sharedMesh = _quad;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _settings.CloudMaterial;
            if (_settings.CloudMaterial == null)
            {
                Log.Warn(nameof(NukeCloudView), "No cloud material on BalloonsConfiguration: no mushroom cloud rises.");
            }

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.sortingLayerName = _settings.SortingLayerName;
            _renderer.sortingOrder = _settings.CloudSortingOrder;
            _renderer.enabled = false;
            _block = new MaterialPropertyBlock();
        }

        public void Play(Vector3 center)
        {
            transform.position = center;
            _elapsed = 0f;
            _playing = true;
            SetProgress(0f);
            _renderer.enabled = true;
        }

        public void Stop()
        {
            _playing = false;
            _renderer.enabled = false;
        }

        private void SetProgress(float progress)
        {
            _block.SetFloat(ProgressId, progress);
            _renderer.SetPropertyBlock(_block);
        }

        private static Mesh BuildQuad(float reach)
        {
            var mesh = new Mesh { name = "NukeCloudQuad" };
            mesh.SetVertices(new[]
            {
                new Vector3(-reach, -reach, 0f), new Vector3(reach, -reach, 0f),
                new Vector3(-reach, reach, 0f), new Vector3(reach, reach, 0f),
            });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
