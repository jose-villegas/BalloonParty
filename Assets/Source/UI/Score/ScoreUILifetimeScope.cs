using BalloonParty.Game;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Score
{
    public class ScoreUILifetimeScope : LifetimeScope
    {
        private void Start()
        {
            var scoreController = Container.Resolve<ScoreController>();

            foreach (var label in GetComponentsInChildren<ScoreCounterLabel>(true))
                label.Bind(scoreController.TotalScore);

            foreach (var label in GetComponentsInChildren<LevelLabel>(true))
                label.Bind(scoreController.Level);
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<ColorProgressBarInstancer>();
            builder.RegisterComponentInHierarchy<LevelUpPopUp>();
        }
    }
}