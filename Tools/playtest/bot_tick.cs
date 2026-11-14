// One tick of the playtest bot, sent to a running Editor in Play mode through `unity command eval`
// by playtest_bot.py, which fills in the __PLACEHOLDERS__. Not part of the Unity project (it lives
// outside Assets/): the Pipeline package compiles it on each call. Most game types are internal, so it
// reaches them by reflection.
//
// The shooting runs in the Editor, not in the tick: the first tick installs a handler on the Editor's
// update loop that checks the cannon every 50 ms, so a shot's timing isn't rounded to the tick length.
// Once the held ball is ready the handler fires:
// - best:  the game's best-shot solver (the "Fire Best Shot" cheat), at once.
// - human: after a reaction time (a floor plus an exponential wait, so shots come as a Poisson
//          process), at the solver's angle plus a normal aim error, or now and then (wildChance) at
//          any angle, unaimed.
// The handler removes itself when Play mode ends, when a newer bot replaces it, or when ticks stop
// coming (the heartbeat). On the game-over screen a tick presses Restart (restart on) or just reports it.
// Each tick returns one line:
//   <time> L<level> score <score> hearts <hearts> <navigation>/<phase> shots <shots fired so far> fill <board fill> inflight <balls in the air> breather <seconds left> rewards <golden placed> <golden popped> <heart placed> <heart popped>
// (board fill: the share of open slots balloons fill, IBoardFill.Occupancy; a full board is where hearts are lost;
// breather: seconds left in the spawn clock's breather after a big clear, ISpawnClock.BreatherSecondsLeft,
// 0 outside one)
// or "<time> RESTART" / "<time> GAMEOVER", "<time> LAUNCH" while it presses Play on the launch screen, or
// "nogame <navigation>" while the game scope isn't up.
var mode = "__MODE__";
var reactFloor = __REACT_FLOOR__f;
var reactMean = __REACT_MEAN__f;
var aimSd = __AIM_SD__f;
var wildChance = __WILD__f;
var botId = "__BOT_ID__";
var restart = __RESTART__;
var purple = __PURPLE__;
var startHearts = __HEARTS__;
const string IdKey = "BalloonPartyBot.Id";
const string SetupKey = "BalloonPartyBot.Setup.";
const string RewardsKey = "BalloonPartyBot.Rewards.";
const string HeartbeatKey = "BalloonPartyBot.Heartbeat";
const string ShotsKey = "BalloonPartyBot.Shots";
const float CheckInterval = 0.05f;
const float HeartbeatTimeout = 10f;
const string LaunchSeenKey = "BalloonPartyBot.LaunchSeen.";
const string LaunchTapKey = "BalloonPartyBot.LaunchTap.";
const float LaunchPressDelay = 1.5f;
const float LaunchRetapDelay = 10f;

var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var staticFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
var asm = typeof(BalloonParty.Cheats.ICheat).Assembly;
var progressType = asm.GetType("BalloonParty.Game.Level.ILevelProgress");
var healthType = asm.GetType("BalloonParty.Game.Health.IPlayerHealth");
var scoreType = asm.GetType("BalloonParty.Game.Run.IRunScore");
var navType = asm.GetType("BalloonParty.Shared.GameState.Navigation");
var controllerType = asm.GetType("BalloonParty.Thrower.ThrowerController");
object Value(object reactive) { return reactive.GetType().GetProperty("Value").GetValue(reactive); }
object Prop(object o, System.Type t, string name) { return t.GetProperty(name).GetValue(o); }
string Nav() { return Value(navType.GetProperty("Current", staticFlags).GetValue(null)).ToString(); }

VContainer.IObjectResolver FindGame()
{
    foreach (var scope in UnityEngine.Object.FindObjectsByType<VContainer.Unity.LifetimeScope>())
    {
        if (scope.Container != null && !(scope is BalloonParty.Thrower.ThrowerLifetimeScope)
            && scope.Container.TryResolve(progressType, out _))
        {
            return scope.Container;
        }
    }

    return null;
}

object FindController()
{
    var thrower = UnityEngine.Object.FindAnyObjectByType<BalloonParty.Thrower.ThrowerLifetimeScope>();
    return thrower != null && thrower.Container != null ? thrower.Container.Resolve(controllerType) : null;
}

BalloonParty.Cheats.ICheat FindSolver(VContainer.IObjectResolver resolver)
{
    var cheats = (System.Collections.IEnumerable)resolver.Resolve(
        typeof(System.Collections.Generic.IEnumerable<BalloonParty.Cheats.ICheat>));
    foreach (BalloonParty.Cheats.ICheat cheat in cheats)
    {
        if (cheat.Name == "Fire Best Shot")
        {
            return cheat;
        }
    }

    return null;
}

bool IsReady(object controller)
{
    return controller != null && (bool)controllerType.GetMethod("IsReadyToFire", flags).Invoke(controller, null);
}

bool InPlay(VContainer.IObjectResolver resolver)
{
    return Nav() == "Game" && Value(Prop(resolver.Resolve(progressType), progressType, "Phase")).ToString() == "Playing";
}

// The solver's angle, read back from the aim override it sets, with a normal aim error added. A wild
// shot (wildChance) ignores the board and goes anywhere in the aim range: a player who fires without
// reading where the balloons are.
void FireLikeAPerson(VContainer.IObjectResolver resolver, object controller)
{
    var flight = (BalloonParty.Shared.IProjectileFlightConfig)resolver.Resolve(typeof(BalloonParty.Shared.IProjectileFlightConfig));
    float angle;
    if (UnityEngine.Random.value < wildChance)
    {
        angle = UnityEngine.Random.Range(flight.AimAngleMinDegrees, flight.AimAngleMaxDegrees);
    }
    else
    {
        var solver = FindSolver(resolver);
        var overrideProperty = controllerType.GetProperty("DirectionOverride", flags);
        solver.GetType().GetMethod("SetDirectionOverride", flags).Invoke(solver, null);
        var best = (UnityEngine.Vector3?)overrideProperty.GetValue(controller);
        overrideProperty.SetValue(controller, null);

        angle = best.HasValue ? UnityEngine.Mathf.Atan2(best.Value.y, best.Value.x) * UnityEngine.Mathf.Rad2Deg : 90f;
        var u1 = 1f - UnityEngine.Random.value;
        var u2 = UnityEngine.Random.value;
        var normal = UnityEngine.Mathf.Sqrt(-2f * UnityEngine.Mathf.Log(u1)) * UnityEngine.Mathf.Cos(2f * UnityEngine.Mathf.PI * u2);
        angle = UnityEngine.Mathf.Clamp(angle + normal * aimSd, flight.AimAngleMinDegrees, flight.AimAngleMaxDegrees);
    }

    var radians = angle * UnityEngine.Mathf.Deg2Rad;
    var direction = new UnityEngine.Vector3(UnityEngine.Mathf.Cos(radians), UnityEngine.Mathf.Sin(radians), 0f);
    controllerType.GetMethod("FireAt", flags).Invoke(controller, new object[] { direction });
}

// The reward balloons, counted on the shooter's loop so one placed and popped between two ticks still
// counts: a placement is the service's on-board flag turning on, a pop the payout's last-paid balloon changing.
object watchedGame = null;
object rewardService = null;
object rewardPayout = null;
var goldenWasAlive = false;
var heartWasAlive = false;
object lastGoldenPaid = null;
object lastHeartPaid = null;
var rewardsBroken = false;
void CountUp(string key)
{
    UnityEditor.SessionState.SetInt(RewardsKey + key, UnityEditor.SessionState.GetInt(RewardsKey + key, 0) + 1);
}

void WatchRewards(VContainer.IObjectResolver resolver)
{
    if (!object.ReferenceEquals(watchedGame, resolver))
    {
        watchedGame = resolver;
        rewardService = resolver.Resolve(asm.GetType("BalloonParty.Balloon.Reward.IRewardBalloonRequests"));
        var payoutType = asm.GetType("BalloonParty.Balloon.Reward.RewardBalloonPayout");
        rewardPayout = null;
        foreach (var startable in (System.Collections.IEnumerable)resolver.Resolve(
                     typeof(System.Collections.Generic.IEnumerable<VContainer.Unity.IStartable>)))
        {
            if (startable.GetType() == payoutType)
            {
                rewardPayout = startable;
            }
        }

        lastGoldenPaid = rewardPayout?.GetType().GetField("_lastGoldenPaid", flags).GetValue(rewardPayout);
        lastHeartPaid = rewardPayout?.GetType().GetField("_lastHeartPaid", flags).GetValue(rewardPayout);
    }

    var goldenAlive = (bool)rewardService.GetType().GetField("_goldenAlive", flags).GetValue(rewardService);
    var heartAlive = (bool)rewardService.GetType().GetField("_heartAlive", flags).GetValue(rewardService);
    if (goldenAlive && !goldenWasAlive)
    {
        CountUp("GoldenPlaced");
    }

    if (heartAlive && !heartWasAlive)
    {
        CountUp("HeartPlaced");
    }

    goldenWasAlive = goldenAlive;
    heartWasAlive = heartAlive;
    if (rewardPayout == null)
    {
        return;
    }

    var goldenPaid = rewardPayout.GetType().GetField("_lastGoldenPaid", flags).GetValue(rewardPayout);
    var heartPaid = rewardPayout.GetType().GetField("_lastHeartPaid", flags).GetValue(rewardPayout);
    if (goldenPaid != null && !object.ReferenceEquals(goldenPaid, lastGoldenPaid))
    {
        CountUp("GoldenPaid");
    }

    if (heartPaid != null && !object.ReferenceEquals(heartPaid, lastHeartPaid))
    {
        CountUp("HeartPaid");
    }

    lastGoldenPaid = goldenPaid;
    lastHeartPaid = heartPaid;
}

string Rewards()
{
    int Count(string key) { return UnityEditor.SessionState.GetInt(RewardsKey + key, 0); }
    return " rewards " + Count("GoldenPlaced") + " " + Count("GoldenPaid") + " " + Count("HeartPlaced") + " "
           + Count("HeartPaid");
}

var now = UnityEngine.Time.realtimeSinceStartup;
UnityEditor.SessionState.SetFloat(HeartbeatKey, now);
if (UnityEditor.SessionState.GetString(IdKey, "") != botId)
{
    UnityEditor.SessionState.SetString(IdKey, botId);
    UnityEditor.SessionState.SetInt(ShotsKey, 0);
    foreach (var key in new[] { "GoldenPlaced", "GoldenPaid", "HeartPlaced", "HeartPaid" })
    {
        UnityEditor.SessionState.EraseInt(RewardsKey + key);
    }

    var nextCheck = 0f;
    var fireAt = -1f;
    UnityEditor.EditorApplication.CallbackFunction shooter = null;
    shooter = () =>
    {
        var clock = UnityEngine.Time.realtimeSinceStartup;
        if (!UnityEditor.EditorApplication.isPlaying
            || UnityEditor.SessionState.GetString(IdKey, "") != botId
            || clock - UnityEditor.SessionState.GetFloat(HeartbeatKey, 0f) > HeartbeatTimeout)
        {
            UnityEditor.EditorApplication.update -= shooter;
            if (UnityEditor.SessionState.GetString(IdKey, "") == botId)
            {
                UnityEditor.SessionState.EraseString(IdKey);
            }

            return;
        }

        if (clock < nextCheck)
        {
            return;
        }

        nextCheck = clock + CheckInterval;
        var resolver = FindGame();
        if (resolver != null && !rewardsBroken)
        {
            // A renamed field would throw here every 50 ms and stop the shooting: the counters go, the bot plays on.
            try
            {
                WatchRewards(resolver);
            }
            catch (System.Exception)
            {
                rewardsBroken = true;
            }
        }

        var controller = FindController();
        if (resolver == null || !InPlay(resolver) || !IsReady(controller))
        {
            // A reaction is to a ready cannon: one that stops being ready (a level-up) starts it over.
            fireAt = -1f;
            return;
        }

        if (fireAt < 0f)
        {
            fireAt = mode == "best" ? clock : clock + reactFloor - reactMean * UnityEngine.Mathf.Log(1f - UnityEngine.Random.value);
        }

        if (clock < fireAt)
        {
            return;
        }

        fireAt = -1f;
        if (mode == "best")
        {
            FindSolver(resolver).Execute();
        }
        else
        {
            FireLikeAPerson(resolver, controller);
        }

        UnityEditor.SessionState.SetInt(ShotsKey, UnityEditor.SessionState.GetInt(ShotsKey, 0) + 1);
    };
    UnityEditor.EditorApplication.update += shooter;
}

var time = UnityEngine.Time.time.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
var nav = Nav();
if (nav == "Launch")
{
    // Play mode entered from the Launcher scene waits on its Play button. The bot taps it the way a finger
    // does (AutoplayTap, as the device autoplay), so its press animation and the ascend play, after a
    // moment on the screen, and taps again only if a tap plainly didn't take.
    var now = (float)UnityEditor.EditorApplication.timeSinceStartup;
    var seenKey = LaunchSeenKey + botId;
    var seen = UnityEditor.SessionState.GetFloat(seenKey, -1f);
    if (seen < 0f)
    {
        UnityEditor.SessionState.SetFloat(seenKey, now);
        seen = now;
    }

    var tapKey = LaunchTapKey + botId;
    if (now - seen >= LaunchPressDelay && now - UnityEditor.SessionState.GetFloat(tapKey, -100f) >= LaunchRetapDelay)
    {
        var tap = asm.GetType("BalloonParty.Cheats.Autoplay.AutoplayTap").GetMethod("ButtonThatCalls", staticFlags)
            .MakeGenericMethod(typeof(BalloonParty.Game.LaunchPlayTrigger));
        if ((bool)tap.Invoke(null, new object[] { "Play" }))
        {
            UnityEditor.SessionState.SetFloat(tapKey, now);
            return time + " LAUNCH tapped Play";
        }
    }

    return time + " LAUNCH";
}

var game = FindGame();
if (game == null)
{
    return "nogame " + nav;
}

if (nav == "GameOver")
{
    if (!restart)
    {
        return time + " GAMEOVER";
    }

    // A finger can only press Restart once the screen is up: after the loss cinematic, the fade-in and its
    // input guard, when its canvas group turns interactable. Pressing earlier skips the cinematic.
    var screen = UnityEngine.Object.FindAnyObjectByType<BalloonParty.UI.GameOver.GameOverScreen>();
    var group = screen != null
        ? typeof(BalloonParty.UI.GameOver.GameOverScreen)
            .GetField("_canvasGroup", flags)?.GetValue(screen) as UnityEngine.CanvasGroup
        : null;
    if (screen != null && screen.isActiveAndEnabled && group != null && group.interactable)
    {
        screen.OnRestartPressed();
        return time + " RESTART";
    }

    return time + " GAMEOVER waiting for the screen";
}

// The session's setup, once its first run is in play: purple restarts the run on the first level from the
// level-up where purple can roll that the run's seed makes purple (Start From Level, keeping the seed, so the
// same roll holds); hearts then sets the hearts the player has, so the session starts as low as asked.
var setupKey = SetupKey + botId;
var setupStage = UnityEditor.SessionState.GetInt(setupKey, 0);
if (setupStage < 2 && InPlay(game))
{
    if (setupStage == 0 && purple)
    {
        var seed = (int)Prop(game.Resolve(asm.GetType("BalloonParty.Game.Run.IRunSeed")), asm.GetType("BalloonParty.Game.Run.IRunSeed"), "Value");
        var pacingType = asm.GetType("BalloonParty.Configuration.Level.LevelPacingConfiguration");
        var pacing = UnityEngine.Resources.FindObjectsOfTypeAll(pacingType)[0];
        var generation = pacingType.GetProperty("Generation", flags).GetValue(pacing);
        var extraColor = generation.GetType().GetProperty("ExtraColor", flags).GetValue(generation);
        var isPurple = extraColor.GetType().GetMethod("IsExtraColorLevel", flags);
        var purpleLevel = 1;
        while (purpleLevel < 200 && !(bool)isPurple.Invoke(extraColor, new object[] { purpleLevel, seed }))
        {
            purpleLevel++;
        }

        if (purpleLevel >= 200)
        {
            // No purple level in reach (the extra colour's chance is 0): play on as configured.
            UnityEditor.SessionState.SetInt(setupKey, 1);
            UnityEditor.SessionState.SetInt(setupKey + ".Level", -1);
            return time + " SETUP no purple level";
        }

        var cheatState = asm.GetType("BalloonParty.Cheats.CheatState");
        cheatState.GetField("StartLevel", staticFlags).SetValue(null, purpleLevel);
        cheatState.GetField("KeepRunSeed", staticFlags).SetValue(null, true);
        var runController = game.Resolve(asm.GetType("BalloonParty.Game.Run.RunController"));
        runController.GetType().GetMethod("RestartRun", flags).Invoke(runController, new object[] { true });
        UnityEditor.SessionState.SetInt(setupKey, 1);
        UnityEditor.SessionState.SetInt(setupKey + ".Level", purpleLevel);
        UnityEditor.SessionState.SetFloat(setupKey + ".Since", UnityEngine.Time.realtimeSinceStartup);
        return time + " SETUP purple level " + purpleLevel;
    }

    // The restart takes a moment: the hearts wait for the run it starts, or its reset would refill them. A run
    // that never reaches the level (a retry level wins over the start level) gives up after a while.
    var levelNow = (int)Value(Prop(game.Resolve(progressType), progressType, "Level"));
    var wanted = UnityEditor.SessionState.GetInt(setupKey + ".Level", 0);
    var waited = UnityEngine.Time.realtimeSinceStartup - UnityEditor.SessionState.GetFloat(setupKey + ".Since", 0f);
    if (setupStage == 1 && wanted > 0 && levelNow != wanted && waited < 10f)
    {
        return time + " SETUP waiting for level " + wanted;
    }

    UnityEditor.SessionState.SetInt(setupKey, 2);
    var cheatReset = asm.GetType("BalloonParty.Cheats.CheatState");
    cheatReset.GetField("StartLevel", staticFlags).SetValue(null, 1);
    if (startHearts > 0)
    {
        var health = game.Resolve(asm.GetType("BalloonParty.Game.Health.PlayerHealthController"));
        var max = (int)health.GetType().GetProperty("Max").GetValue(health);
        var startWith = System.Math.Min(startHearts, max);
        var current = health.GetType().GetField("_current", flags).GetValue(health);
        current.GetType().GetProperty("Value").SetValue(current, startWith);
        return time + " SETUP hearts " + startWith + (purple && levelNow != wanted && wanted > 0 ? " (not on the purple level)" : "");
    }

    if (purple && wanted > 0 && levelNow != wanted)
    {
        return time + " SETUP gave up waiting for level " + wanted;
    }
}

var progress = game.Resolve(progressType);
var level = (int)Value(Prop(progress, progressType, "Level"));
var phase = Value(Prop(progress, progressType, "Phase")).ToString();
var hearts = (int)Value(Prop(game.Resolve(healthType), healthType, "Current"));
var score = (int)Value(Prop(game.Resolve(scoreType), scoreType, "TotalScore"));
var shots = UnityEditor.SessionState.GetInt(ShotsKey, 0);
var fillType = asm.GetType("BalloonParty.Balloon.Spawner.IBoardFill");
var occupancy = (float)Prop(game.Resolve(fillType), fillType, "Occupancy");
var fill = occupancy.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
var registryType = asm.GetType("BalloonParty.Projectile.IProjectileRegistry");
var inFlight = (int)Prop(game.Resolve(registryType), registryType, "InFlightCount");
var clockType = asm.GetType("BalloonParty.Balloon.Spawner.ISpawnClock");
var breatherLeft = (float)Prop(game.Resolve(clockType), clockType, "BreatherSecondsLeft");
var breather = breatherLeft.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
return time + " L" + level + " score " + score + " hearts " + hearts + " " + nav + "/" + phase + " shots " + shots
       + " fill " + fill + " inflight " + inFlight + " breather " + breather + Rewards();
