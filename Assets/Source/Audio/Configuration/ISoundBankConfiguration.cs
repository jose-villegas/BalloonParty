using System.Collections.Generic;
using BalloonParty.Audio;

namespace BalloonParty.Audio.Configuration
{
    internal interface ISoundBankConfiguration
    {
        IReadOnlyList<int> MelodicScale { get; }
        int MelodicRootSemitone { get; }
        bool TryGet(GameSoundId id, out SfxEntry entry);
    }
}
