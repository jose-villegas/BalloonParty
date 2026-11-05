using BalloonParty.Projectile.Model;

namespace BalloonParty.Slots.Capabilities
{
    public readonly struct DamageContext
    {
        public readonly int Damage;
        public readonly DamageFlags Flags;
        public readonly string SourceColorId;

        // The shot behind this damage — the one that hit, or whose hit set off the item doing the damage.
        // Null for sources no shot caused (cheats, board effects). Several shots can be in flight, so
        // anything acting on "the shot that did this" reads it from here, never from the thrower.
        public readonly IProjectileModel Projectile;

        public DamageContext(
            int damage, DamageFlags flags = DamageFlags.Normal, string sourceColorId = "",
            IProjectileModel projectile = null)
        {
            Damage = damage;
            Flags = flags;
            SourceColorId = sourceColorId;
            Projectile = projectile;
        }
    }
}
