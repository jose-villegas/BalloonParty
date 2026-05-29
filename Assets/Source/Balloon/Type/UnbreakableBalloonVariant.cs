#if UNITY_EDITOR
using UnityEditor;
#endif
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Type
{
    /// <summary>
    /// Balloon variant for <c>BalloonType.Unbreakable</c>.
    /// Pushes <c>_SphereCenter</c>, <c>_SphereRadius</c>, and
    /// <c>_TimeOffset</c> to every quadrant <see cref="SpriteRenderer"/>
    /// so the <c>BalloonParty/Balloon/UnbreakableBalloon</c> shader can
    /// compute metallic gradient, specular, reflection, and rim effects
    /// relative to the composed sphere rather than world origin.
    ///
    /// Inner renderers receive the same sphere data so effects that depend
    /// on sphere-local position stay coherent across both layers.
    ///
    /// <c>[ExecuteAlways]</c> keeps the shader animation running in edit
    /// mode without entering Play mode.
    /// </summary>
    [ExecuteAlways]
    internal class UnbreakableBalloonVariant : MonoBehaviour, IBalloonVariant, IBalloonViewBinding
    {
        private static readonly int SphereCenterId = Shader.PropertyToID("_SphereCenter");
        private static readonly int SphereRadiusId = Shader.PropertyToID("_SphereRadius");
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");

        [SerializeField] private SpriteRenderer[] _renderers;
        [SerializeField] private SpriteRenderer[] _innerRenderers;

        [Tooltip("Sphere radius in world units. If zero, computed from " +
                 "the outer renderers' bounds at Awake.")]
        [SerializeField] private float _sphereRadius;

        private MaterialPropertyBlock _block;
        private float _instancePhase;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _instancePhase = Random.value * 100f;
            ComputeRadiusIfNeeded();
        }

        private void Update()
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                return;
            }

            var currentTime = Time.time;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                currentTime = (float)EditorApplication.timeSinceStartup;
                SceneView.RepaintAll();
            }
#endif

            var center = (Vector4)transform.position;
            var timeOffset = currentTime + _instancePhase;

            PushPropertyBlock(_renderers, center, timeOffset);
            PushPropertyBlock(_innerRenderers, center, timeOffset);
        }

        private void OnValidate()
        {
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            ComputeRadiusIfNeeded();
        }

        public void Initialize(IWriteableBalloonModel model) { }

        public void Bind(IBalloonModel model, CompositeDisposable disposables)
        {
            _instancePhase = Random.value * 100f;
            ComputeRadiusIfNeeded();
        }

        private void PushPropertyBlock(SpriteRenderer[] renderers, Vector4 center, float timeOffset)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (var r in renderers)
            {
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(_block);
                _block.SetVector(SphereCenterId, center);
                _block.SetFloat(SphereRadiusId, _sphereRadius);
                _block.SetFloat(TimeOffsetId, timeOffset);
                r.SetPropertyBlock(_block);
            }
        }

        private void ComputeRadiusIfNeeded()
        {
            if (_sphereRadius > 0f)
            {
                return;
            }

            if (_renderers == null || _renderers.Length == 0)
            {
                return;
            }

            // The composed sphere spans the union of all quadrant bounds;
            // half the longest axis gives a good approximation.
            var bounds = _renderers[0].bounds;
            for (var i = 1; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    bounds.Encapsulate(_renderers[i].bounds);
                }
            }

            _sphereRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);
        }
    }
}
