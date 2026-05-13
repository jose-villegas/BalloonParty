using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Balloon.Type
{
    public class ToughBalloonType : MonoBehaviour, IBalloonTypeConfiguration
    {
        [SerializeField] private BalloonType _typeName = BalloonType.Tough;
        [SerializeField] private int _hitsToPop = 2;

        public BalloonType TypeName => _typeName;
        public int HitsToPop => _hitsToPop;

        public void Initialize(IWriteableBalloonModel model)
        {
            model.TypeName.Value = _typeName;
            model.HitsRemaining.Value = _hitsToPop;
        }
    }
}
