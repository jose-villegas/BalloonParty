using BalloonParty.Configuration;
using UnityEngine;

namespace BalloonParty.Balloon.Type
{
    public abstract class ColorableBalloonType : MonoBehaviour, IBalloonTypeConfiguration
    {
        [SerializeField] private string _typeName;
        [SerializeField] private int _hitsToPop = 1;
        [SerializeField] private string[] _allowedColorNames;

        public string TypeName => _typeName;
        public int HitsToPop => _hitsToPop;

        public string PickColor(GamePalette palette)
        {
            if (_allowedColorNames == null || _allowedColorNames.Length == 0)
            {
                return null;
            }

            return _allowedColorNames[Random.Range(0, _allowedColorNames.Length)];
        }
    }
}
