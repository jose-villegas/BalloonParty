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
// coming (the heartbeat). Each tick restarts the run on the game-over screen and returns one line:
//   <time> L<level> score <score> hearts <hearts> <navigation>/<phase> shots <shots fired so far> fill <board fill> inflight <balls in the air>
// (board fill: the share of open slots balloons fill, IBoardFill.Occupancy; a full board is where hearts are lost)
// or "<time> RESTART", or "nogame <navigation>" while the game scope isn't up.
var mode = "__MODE__";
var reactFloor = __REACT_FLOOR__f;
var reactMean = __REACT_MEAN__f;
var aimSd = __AIM_SD__f;
var wildChance = __WILD__f;
var botId = "__BOT_ID__";
const string IdKey = "BalloonPartyBot.Id";
const string HeartbeatKey = "BalloonPartyBot.Heartbeat";
const string ShotsKey = "BalloonPartyBot.Shots";
const float CheckInterval = 0.05f;
const float HeartbeatTimeout = 10f;

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

var now = UnityEngine.Time.realtimeSinceStartup;
UnityEditor.SessionState.SetFloat(HeartbeatKey, now);
if (UnityEditor.SessionState.GetString(IdKey, "") != botId)
{
    UnityEditor.SessionState.SetString(IdKey, botId);
    UnityEditor.SessionState.SetInt(ShotsKey, 0);
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
var game = FindGame();
if (game == null)
{
    return "nogame " + nav;
}

if (nav == "GameOver")
{
    var screen = UnityEngine.Object.FindAnyObjectByType<BalloonParty.UI.GameOver.GameOverScreen>();
    if (screen != null && screen.isActiveAndEnabled)
    {
        screen.OnRestartPressed();
        return time + " RESTART";
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
return time + " L" + level + " score " + score + " hearts " + hearts + " " + nav + "/" + phase + " shots " + shots
       + " fill " + fill + " inflight " + inFlight;
