using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Display Configuration", fileName = "DisplayConfiguration")]
    public class GameDisplayConfiguration : ScriptableObject, IGameDisplayConfiguration
    {
        [SerializeField] private float _referenceWorldWidth = 10f;
        [SerializeField] private float _referenceWorldHeight = 16f;

        [Header("Scene capture")]
        [Tooltip("Screen resolution divisor for the shared scene-capture RT — consumers like the Unbreakable chrome tolerate very low resolutions.")]
        [SerializeField, Range(2, 16)] private int _sceneCaptureDownscale = 8;

        [Tooltip("Render the capture every Nth frame; skipped frames keep the last capture.")]
        [SerializeField, Range(1, 4)] private int _sceneCaptureFrameInterval = 2;

        public float ReferenceWorldWidth => _referenceWorldWidth;
        public float ReferenceWorldHeight => _referenceWorldHeight;
        public int SceneCaptureDownscale => _sceneCaptureDownscale;
        public int SceneCaptureFrameInterval => _sceneCaptureFrameInterval;

        public float GetOrthogonalSize()
        {
            var aspect = (float)Screen.width / Screen.height;
            var sizeToFitWidth = _referenceWorldWidth / (2f * aspect);
            var sizeToFitHeight = _referenceWorldHeight / 2f;
            return Mathf.Max(sizeToFitWidth, sizeToFitHeight);
        }
    }
}
