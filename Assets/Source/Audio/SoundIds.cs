namespace BalloonParty.Audio
{
    internal static class SoundIds
    {
        // Sizes the per-id ordinal arrays the audio helpers key by (int)GameSoundId.
        // One reflection call at type load, off the hot path.
        internal static readonly int Count = System.Enum.GetValues(typeof(GameSoundId)).Length;
    }
}
