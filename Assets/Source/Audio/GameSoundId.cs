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
        UiConfirm,

        // Per-balloon-type pops (append only, like the rest). Simple balloons use BalloonPop;
        // specific kinds map to their own id via CombatSoundRouter.PopSoundFor.
        BalloonPopTough,

        // Physical wall-bounce impact, driven by WallHitMessage (distinct from the ShieldLost cue).
        WallHit,

        // Sustained warning while the board sits in the danger (future-overflow) state; author as a loop.
        DangerWarn,

        // One-shot: the shot actually died (last shield spent at the death wall), via ProjectileDestroyedMessage.
        ProjectileDeath,

        // Loop whose volume tracks cruise speed (WindSoundRouter drives it via SetVolumeFactor).
        WindLoop,

        // Ambient loop for the Launch state; MusicSoundRouter stops it (fading out) on the move to Game.
        LaunchMusic,

        // Per-balloon-type cues (append only), mapped in CombatSoundRouter.PopSoundFor / DeflectSoundFor;
        // unmapped types fall back to BalloonPop / BalloonDeflect.
        BalloonPopRainbow,
        BalloonPopUnbreakable,
        BalloonDeflectUnbreakable,
        BalloonPopSilver,
        BalloonPopGold,

        // Whoosh/sweep played as the camera ascends during the level-up transition.
        LevelAscend,

        // Whoosh/sweep played as the camera descends during the game-over transition.
        LevelDescend,

        // Looping drone that plays while the projectile is in pierce state; fades in on pierce-gained,
        // fades out on pierce-discharged. Author with Loop=true and long FadeIn/FadeOutSeconds.
        PierceLoop,

        BalloonDeflectTough,

        // One rung of the speed ladder: played whenever a wall hit mints a speed tap, whichever rule
        // earned it (a cruise bounce or a clean sweep). Author with MelodicMode.ScaleWalkUp — the router
        // passes the running tap count as the melodic step, so each tap climbs a degree.
        SpeedTap,

        // UI cue when a color progress bar fills completely (before the level-up sequence fires).
        UiProgressComplete,

        // Accent played when the tipping score trail arrives (the level is now confirmed complete).
        LevelCompletePop,

        // Looping ambient played during active daytime gameplay; ducks during flight and popups.
        GameplayLoopDay,

        // Looping ambient played during active nighttime gameplay; crossfades with GameplayLoopDay.
        GameplayLoopNight,

        BalloonPopTougher,
        BalloonDeflectTougher
    }
}
