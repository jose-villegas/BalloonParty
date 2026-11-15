using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Display;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Type
{
    /// <summary>
    ///     The disco ball's body (<c>BalloonParty/Balloon/DiscoBall</c>): gives each ball its own turn of the mirror
    ///     grid, so two on the board never flash in step, and holds the scene capture its tiles reflect while the ball
    ///     is on screen. The grid turns in the shader, so nothing here runs per frame.
    /// </summary>
    internal class DiscoBallLook : MonoBehaviour, IBalloonViewBinding
    {
        private static readonly int SpinPhaseId = Shader.PropertyToID("_SpinPhase");

        [Tooltip("The body renderer on the DiscoBall material.")]
        [SerializeField] private SpriteRenderer _body;

        private MaterialPropertyBlock _block;
        private SceneCaptureService _sceneCapture;

        // Null-guarded: a pooled instance runs its first OnEnable before injection.
        private void OnEnable()
        {
            _sceneCapture?.Acquire();
        }

        private void OnDisable()
        {
            _sceneCapture?.Release();
        }

        [Inject]
        private void Construct(SceneCaptureService sceneCapture)
        {
            _sceneCapture = sceneCapture;

            // Settle the ref-count for an instance injected while already active.
            if (isActiveAndEnabled)
            {
                _sceneCapture.Acquire();
            }
        }

        public void Bind(IBalloonModel model, CompositeDisposable disposables)
        {
            if (_body == null)
            {
                return;
            }

            _block ??= new MaterialPropertyBlock();
            _body.GetPropertyBlock(_block);
            _block.SetFloat(SpinPhaseId, Random.value * Mathf.PI * 2f);
            _body.SetPropertyBlock(_block);
        }
    }
}
