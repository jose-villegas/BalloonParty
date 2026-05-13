using BalloonParty.Configuration;
using UnityEngine;

namespace BalloonParty.Balloon.Type
{
    public class ToughBalloonType : MonoBehaviour, IBalloonTypeConfiguration
    {
        [SerializeField] private string _typeName;
        [SerializeField] private int _hitsToPop = 2;

        public string TypeName => _typeName;
        public int HitsToPop => _hitsToPop;
    }
}
