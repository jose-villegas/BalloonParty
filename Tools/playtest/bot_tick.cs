// One tick of the playtest bot, sent to a running Editor in Play mode through `unity command eval`
// by playtest_bot.py. Not part of the Unity project (it lives outside Assets/): the Pipeline package
// compiles it on each call. Most game types are internal, so it reaches them by reflection.
//
// Each tick: on the game-over screen it restarts the run; in play, if the thrower's held ball is ready,
// it fires the game's own best-shot solver (the "Fire Best Shot" cheat). It returns one line of state:
//   <time> L<level> score <score> hearts <hearts> <navigation>/<phase> fired <0|1>
// or "<time> RESTART", or "nogame <navigation>" while the game scope isn't up.
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
var time = UnityEngine.Time.time.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
var nav = Value(navType.GetProperty("Current", staticFlags).GetValue(null)).ToString();
VContainer.IObjectResolver game = null;
foreach (var scope in UnityEngine.Object.FindObjectsByType<VContainer.Unity.LifetimeScope>())
{
    if (scope.Container != null && !(scope is BalloonParty.Thrower.ThrowerLifetimeScope)
        && scope.Container.TryResolve(progressType, out _))
    {
        game = scope.Container;
        break;
    }
}
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
var fired = 0;
var thrower = UnityEngine.Object.FindAnyObjectByType<BalloonParty.Thrower.ThrowerLifetimeScope>();
if (nav == "Game" && phase == "Playing" && thrower != null && thrower.Container != null)
{
    var controller = thrower.Container.Resolve(controllerType);
    if ((bool)controllerType.GetMethod("IsReadyToFire", flags).Invoke(controller, null))
    {
        var cheats = (System.Collections.IEnumerable)game.Resolve(
            typeof(System.Collections.Generic.IEnumerable<BalloonParty.Cheats.ICheat>));
        foreach (BalloonParty.Cheats.ICheat cheat in cheats)
        {
            if (cheat.Name == "Fire Best Shot")
            {
                cheat.Execute();
                fired = 1;
                break;
            }
        }
    }
}
return time + " L" + level + " score " + score + " hearts " + hearts + " " + nav + "/" + phase + " fired " + fired;
