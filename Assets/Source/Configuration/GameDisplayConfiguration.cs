using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Display Configuration", fileName = "DisplayConfiguration")]
    public class GameDisplayConfiguration : ScriptableObject
    {
        [SerializeField] private float _referenceWorldWidth = 10f;
        [SerializeField] private float _referenceWorldHeight = 16f;

        public float ReferenceWorldWidth => _referenceWorldWidth;
        public float ReferenceWorldHeight => _referenceWorldHeight;

        public float GetOrthogonalSize()
        {
            var aspect = (float)Screen.width / Screen.height;
            var sizeToFitWidth = _referenceWorldWidth / (2f * aspect);
            var sizeToFitHeight = _referenceWorldHeight / 2f;
            return Mathf.Max(sizeToFitWidth, sizeToFitHeight);
        }
    }
}
