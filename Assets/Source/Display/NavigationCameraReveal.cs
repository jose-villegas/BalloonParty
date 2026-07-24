using BalloonParty.Shared.GameState;
using UniRx;
using UnityEngine;

namespace BalloonParty.Display
{
    /// <summary>
    ///     Reveals gameplay on the SINGLE persistent camera by widening its culling mask when Navigation
    ///     leaves <see cref="NavigationState.Launch" /> — instead of swapping to a second camera. During
    ///     Launch only the backdrop + launch-UI layers render; on Game the full gameplay mask is applied. One
    ///     camera (one GI/tint/post pipeline) throughout, so there is no compositing pop at Play. Sits on the
    ///     persistent camera; reads the static <see cref="Navigation" />, so it needs no DI.
    /// </summary>
    internal class NavigationCameraReveal : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        [Tooltip("Layers rendered while at the Launch begin-screen (backdrop + launch UI only).")]
        [SerializeField] private LayerMask _launchMask;

        [Tooltip("Layers rendered once gameplay is revealed (full).")]
        [SerializeField] private LayerMask _gameMask;

        private readonly CompositeDisposable _disposables = new();

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }
        }

        private void OnEnable()
        {
            // Fires the current state immediately, so the mask is correct from the first frame.
            Navigation.Current
                .Subscribe(ApplyMask)
                .AddTo(_disposables);
        }

        private void OnDisable()
        {
            _disposables.Clear();
        }

        private void ApplyMask(NavigationState state)
        {
            if (_camera == null)
            {
                return;
            }

            _camera.cullingMask = state == NavigationState.Launch ? _launchMask.value : _gameMask.value;
        }
    }
}
