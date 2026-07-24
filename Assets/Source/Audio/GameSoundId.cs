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
