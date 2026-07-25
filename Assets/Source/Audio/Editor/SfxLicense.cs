using System;

namespace BalloonParty.Audio.Editor
{
    // Commercial-shippable Creative Commons licenses the fetcher will accept. Deliberately an
    // allowlist (never a denylist) so unknown/legacy Freesound license values are excluded by default.
    [Flags]
    internal enum SfxLicense
    {
        None = 0,
        Cc0 = 1 << 0,
        AttributionBy = 1 << 1,
    }
}
