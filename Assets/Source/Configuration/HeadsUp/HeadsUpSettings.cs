using TMPro;
using UnityEngine;

namespace BalloonParty.Configuration.HeadsUp
{
    /// <summary>
    ///     The heads-ups' rules and looks. The defaults follow the visual language in <c>UI/HeadsUp/README.md</c>:
    ///     gains in blue, losses in vermilion, the skill shots in plum, the streak in ochre, and the frequent,
    ///     quiet ones in graphite.
    /// </summary>
    [CreateAssetMenu(menuName = "Configuration/Heads-Up Settings", fileName = "HeadsUpSettings")]
    internal sealed class HeadsUpSettings : ScriptableObject, IHeadsUpSettings
    {
        private const int Graphite = 0x4A4A52;
        private const int Blue = 0x0072B2;
        private const int Vermilion = 0xB34700;
        private const int Plum = 0x8E3B8E;
        private const int Ochre = 0x9A6700;

        [Tooltip("Heads-ups on screen at once, held ones aside. A burst past it drops or evicts by priority.")]
        [SerializeField] [Min(1)] private int _maxConcurrent = 6;

        [Tooltip("One entry per kind of heads-up.")]
        [SerializeField] private HeadsUpStyleSettings[] _styles = Defaults();

        private HeadsUpStyleSettings[] _byId;

        public int MaxConcurrent => _maxConcurrent;

        private void OnEnable()
        {
            _byId = null;
        }

        private void OnValidate()
        {
            _byId = null;
        }

        public HeadsUpStyleSettings Style(HeadsUpStyleId id)
        {
            _byId ??= Index(_styles);
            var index = (int)id;
            return index < _byId.Length ? _byId[index] : null;
        }

        /// <summary>Every <see cref="HeadsUpStyleId" /> at its slot, so a lookup is an index; a missing id is null.</summary>
        internal static HeadsUpStyleSettings[] Index(HeadsUpStyleSettings[] styles)
        {
            var byId = new HeadsUpStyleSettings[HeadsUpStyleIds.Count];
            foreach (var style in styles)
            {
                var index = (int)style.Id;
                if (index < byId.Length)
                {
                    byId[index] = style;
                }
            }

            return byId;
        }

        internal static HeadsUpStyleSettings[] Defaults()
        {
            return new[]
            {
                new HeadsUpStyleSettings(HeadsUpStyleId.Ready, "ready",
                        Look(Graphite, 1f, 0.85f, 0f, 0.85f, FontStyles.Normal), 0)
                    .HeldUntilReleased().WithOffset(0.75f, 0f),
                new HeadsUpStyleSettings(HeadsUpStyleId.BouncePickup, "+{0} <i>bounce</i>",
                        Look(Blue, 1f, 1f, 0.15f, 1.35f, FontStyles.Bold), 2)
                    .WithOffset(0f, 0.25f),
                new HeadsUpStyleSettings(HeadsUpStyleId.BounceWall, "-1 <i>bounce</i>",
                        Look(Graphite, 0.8f, 0.9f, 0f, 0.9f, FontStyles.Normal), 1)
                    .WithOffset(0f, 0.25f),
                new HeadsUpStyleSettings(HeadsUpStyleId.BounceWallLast, "-1 <i>bounce</i>",
                        Look(Vermilion, 1f, 0.9f, 0f, 0.9f, FontStyles.Bold), 3)
                    .WithOffset(0f, 0.25f),
                new HeadsUpStyleSettings(HeadsUpStyleId.Ricochet, "x{0} <i>ricochet</i>",
                        Look(Plum, 1f, 1.1f, 0.25f, 1.6f, FontStyles.Bold), 3)
                    .WithOffset(0f, 0.25f).EscalatingFrom(2),
                new HeadsUpStyleSettings(HeadsUpStyleId.RicochetPaid,
                        $"x{{0}} <i>ricochet</i>\n<size=65%><color=#{Blue:X6}>+1 <i>bounce</i></color></size>",
                        Look(Plum, 1f, 1.1f, 0.25f, 1.6f, FontStyles.Bold), 3)
                    .WithOffset(0f, 0.25f).EscalatingFrom(2),
                new HeadsUpStyleSettings(HeadsUpStyleId.Streak, "x{0}!",
                        Look(Ochre, 1f, 1.1f, 0.25f, 1.6f, FontStyles.Bold), 3)
                    .WithOffset(0f, 0.3f).WithArgRange(3, 0).EscalatingFrom(3),
                new HeadsUpStyleSettings(HeadsUpStyleId.SpeedTap, "<i>tap!</i>",
                        Look(Graphite, 1f, 0.9f, 0f, 0.9f, FontStyles.Normal), 1)
                    .WithOffset(0f, 0.25f).WithArgRange(0, 3),
                new HeadsUpStyleSettings(HeadsUpStyleId.Pierce, "<i>pierce!</i>",
                        Look(Graphite, 1f, 1f, 0f, 1f, FontStyles.Bold), 2)
                    .WithOffset(0f, 0.3f),
                new HeadsUpStyleSettings(HeadsUpStyleId.Shattered, "x{0} <i>shattered</i>",
                        Look(Plum, 1f, 1.1f, 0.2f, 1.5f, FontStyles.Bold), 3)
                    .WithOffset(0f, 0.25f).WithArgRange(2, 0).EscalatingFrom(2),
                new HeadsUpStyleSettings(HeadsUpStyleId.HeartLost, "-1",
                        Look(Vermilion, 1f, 1.5f, 0f, 1.5f, FontStyles.Bold), 5)
                    .WithOffset(0f, -0.4f),
                new HeadsUpStyleSettings(HeadsUpStyleId.ColorDone, "{0} <i>done</i>",
                        Look(Graphite, 1f, 1f, 0f, 1f, FontStyles.Bold), 2)
                    .WithOffset(0f, -0.35f),
                new HeadsUpStyleSettings(HeadsUpStyleId.Rainbow, "<i>any colour!</i>",
                        Look(Blue, 1f, 1f, 0f, 1f, FontStyles.Bold), 2)
                    .WithOffset(0f, 0.3f)
            };
        }

        private static HeadsUpStyle Look(int rgb, float alpha, float size, float growth, float maxSize, FontStyles fontStyle)
        {
            var color = new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
            return new HeadsUpStyle(color, size, growth, maxSize, fontStyle);
        }
    }
}
