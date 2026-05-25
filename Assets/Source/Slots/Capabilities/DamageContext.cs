namespace BalloonParty.Slots.Capabilities
{
    public readonly struct DamageContext
    {
        public readonly int Damage;
        public readonly DamageFlags Flags;
        public readonly string SourceColorId;

        public DamageContext(int damage, DamageFlags flags = DamageFlags.Normal, string sourceColorId = "")
        {
            Damage = damage;
            Flags = flags;
            SourceColorId = sourceColorId;
        }
    }
}
