using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Type
{
    public abstract class ColorableBalloonType : MonoBehaviour, IBalloonTypeConfiguration
    {
        [SerializeField] private BalloonType _typeName;
        [SerializeField] private int _hitsToPop = 1;
        [SerializeField] [PaletteColorMask] private int _allowedColorsMask = ~0;

        [Inject] private GamePalette _palette;

        public BalloonType TypeName => _typeName;
        public int HitsToPop => _hitsToPop;

        public virtual void Initialize(IWriteableBalloonModel model)
        {
            model.TypeName.Value = _typeName;
            model.HitsRemaining.Value = _hitsToPop;
            model.Color.Value = PickColor() ?? "";
        }

        private string PickColor()
        {
            if (_palette == null || _palette.Colors == null || _palette.Colors.Length == 0)
            {
                return null;
            }

            // Collect allowed color names from bitmask
            var colors = _palette.Colors;
            var count = 0;

            for (var i = 0; i < colors.Length; i++)
            {
                if ((_allowedColorsMask & (1 << i)) != 0)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return null;
            }

            var pick = Random.Range(0, count);
            var current = 0;

            for (var i = 0; i < colors.Length; i++)
            {
                if ((_allowedColorsMask & (1 << i)) == 0)
                {
                    continue;
                }

                if (current == pick)
                {
                    return colors[i].Name;
                }

                current++;
            }

            return null;
        }
    }
}
