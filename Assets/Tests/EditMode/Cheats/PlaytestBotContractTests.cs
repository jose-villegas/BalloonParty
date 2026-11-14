#if UNITY_ENABLE_CHECKS
using System;
using System.Reflection;
using BalloonParty.Cheats;
using NUnit.Framework;

namespace BalloonParty.Tests.Cheats
{
    // Tools/playtest/bot_tick.cs reaches the game by name, through reflection, from outside the project: a rename
    // here would stop a playtest quietly instead of failing a build. Each case is a member the bot reads or calls;
    // keep the list in step with the tick.
    [TestFixture]
    public class PlaytestBotContractTests
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                                         | BindingFlags.Static;

        private static readonly Assembly Runtime = typeof(ICheat).Assembly;

        [TestCase("BalloonParty.Game.Level.ILevelProgress", "Level")]
        [TestCase("BalloonParty.Game.Level.ILevelProgress", "Phase")]
        [TestCase("BalloonParty.Game.Health.IPlayerHealth", "Current")]
        [TestCase("BalloonParty.Game.Health.PlayerHealthController", "Max")]
        [TestCase("BalloonParty.Game.Run.IRunScore", "TotalScore")]
        [TestCase("BalloonParty.Game.Run.IRunSeed", "Value")]
        [TestCase("BalloonParty.Shared.GameState.Navigation", "Current")]
        [TestCase("BalloonParty.Thrower.ThrowerController", "DirectionOverride")]
        [TestCase("BalloonParty.Balloon.Spawner.IBoardFill", "Occupancy")]
        [TestCase("BalloonParty.Projectile.IProjectileRegistry", "InFlightCount")]
        [TestCase("BalloonParty.Balloon.Spawner.ISpawnClock", "BreatherSecondsLeft")]
        [TestCase("BalloonParty.Game.Run.IFailureOffers", "Current")]
        public void Property_TheBotReads_Exists(string typeName, string property)
        {
            Assert.That(TypeNamed(typeName).GetProperty(property, Any), Is.Not.Null, $"{typeName}.{property}");
        }

        [TestCase("BalloonParty.Balloon.Reward.RewardBalloonService", "_goldenAlive", typeof(bool))]
        [TestCase("BalloonParty.Balloon.Reward.RewardBalloonService", "_heartAlive", typeof(bool))]
        [TestCase("BalloonParty.Game.Health.PlayerHealthController", "_current", typeof(UniRx.ReactiveProperty<int>))]
        [TestCase("BalloonParty.Cheats.CheatState", "StartLevel", typeof(int))]
        [TestCase("BalloonParty.Cheats.CheatState", "KeepRunSeed", typeof(bool))]
        [TestCase("BalloonParty.UI.GameOver.GameOverScreen", "_canvasGroup", typeof(UnityEngine.CanvasGroup))]
        [TestCase("BalloonParty.Game.Run.FailureOffer", "CanRetry", typeof(bool))]
        [TestCase("BalloonParty.Game.Run.FailureOffer", "RetryNeedsAd", typeof(bool))]
        [TestCase("BalloonParty.Game.Run.FailureOffer", "CanBonusRetry", typeof(bool))]
        public void Field_TheBotReadsOrWrites_HasItsType(string typeName, string field, Type type)
        {
            var found = TypeNamed(typeName).GetField(field, Any);

            Assert.That(found, Is.Not.Null, $"{typeName}.{field}");
            Assert.That(found.FieldType, Is.EqualTo(type), $"{typeName}.{field}");
        }

        [TestCase("BalloonParty.Game.Run.RunController", "RestartRun", new[] { typeof(bool) })]
        [TestCase("BalloonParty.Configuration.Level.ILevelPacingConfiguration", "ParametersForLevel",
            new[] { typeof(int), typeof(int) })]
        [TestCase("BalloonParty.Thrower.ThrowerController", "IsReadyToFire", new Type[0])]
        [TestCase("BalloonParty.Thrower.ThrowerController", "FireAt", new[] { typeof(UnityEngine.Vector3) })]
        [TestCase("BalloonParty.Cheats.FireBestShotCheat", "SetDirectionOverride", new Type[0])]
        [TestCase("BalloonParty.UI.GameOver.GameOverScreen", "OnRestartPressed", new Type[0])]
        [TestCase("BalloonParty.UI.GameOver.GameOverScreen", "OnRetryPressed", new Type[0])]
        [TestCase("BalloonParty.UI.GameOver.GameOverScreen", "OnBonusRetryPressed", new Type[0])]
        public void Method_TheBotCalls_TakesItsArguments(string typeName, string method, Type[] arguments)
        {
            Assert.That(TypeNamed(typeName).GetMethod(method, Any, null, arguments, null), Is.Not.Null,
                $"{typeName}.{method}");
        }

        [Test]
        public void TheLevelsParameters_SayWhetherTheLevelIsPurple()
        {
            var parametersFor = TypeNamed("BalloonParty.Configuration.Level.ILevelPacingConfiguration")
                .GetMethod("ParametersForLevel", Any);

            Assert.That(parametersFor.ReturnType.GetProperty("IsExtraColorLevel")?.PropertyType, Is.EqualTo(typeof(bool)));
        }

        [Test]
        public void TheRewardService_IsTheRequestsTheBotResolves()
        {
            var requests = TypeNamed("BalloonParty.Balloon.Reward.IRewardBalloonRequests");

            Assert.That(requests.IsAssignableFrom(TypeNamed("BalloonParty.Balloon.Reward.RewardBalloonService")), Is.True);
        }

        [Test]
        public void TheBestShotCheat_IsNamedAsTheBotLooksItUp()
        {
            var cheat = (ICheat)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(
                TypeNamed("BalloonParty.Cheats.FireBestShotCheat"));

            Assert.That(cheat.Name, Is.EqualTo("Fire Best Shot"));
        }

        [Test]
        public void TheLaunchTap_FindsAButtonByItsHandler()
        {
            Assert.That(TypeNamed("BalloonParty.Cheats.Autoplay.AutoplayTap").GetMethod("ButtonThatCalls", Any),
                Is.Not.Null);
        }

        private static Type TypeNamed(string name)
        {
            var type = Runtime.GetType(name);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }
    }
}
#endif
