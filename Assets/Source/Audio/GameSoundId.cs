namespace BalloonParty.Audio
{
    // Append only — members are serialized by ordinal (SoundBankConfiguration indexes its
    // entry array by this value, and SfxEntry stores it). Never reorder or insert mid-list.
    internal enum GameSoundId
    {
        None = 0,
        BalloonPop,
        BalloonDeflect,
        BalloonResist,
        ShotFired,
        ShotReload,
        CruiseLoopStart,

        // Reserved, not currently played: the cruise loop ends via ISoundPlayer.Stop() on the
        // CruiseLoopStart handle, not by playing a distinct cue. Kept so a proper stop sound can
        // be authored later without renumbering the bank.
        CruiseLoopStop,
        DoomedWarn,
        PierceDischarge,
        ShieldGained,
        ShieldLost,
        ItemBomb,
        ItemLaser,
        ItemLightning,
        ItemPaint,
        ItemSnipe,
        ItemShield,
        StreakStep,
        ScoreChime,
        LevelUp,
        LevelUpGlow,
        LevelTransition,
        BoardClear,
        GameOver,
        HeartDrain,
        OverflowThud,
        UiConfirm
    }
}
