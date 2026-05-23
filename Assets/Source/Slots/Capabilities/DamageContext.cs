namespace BalloonParty.Slots.Capabilities
{
    public readonly struct DamageContext
    {
        public readonly int Damage;
        public readonly DamageFlags Flags;

        public DamageContext(int damage, DamageFlags flags = DamageFlags.Normal)
        {
            Damage = damage;
            Flags = flags;
        }
    }
}
