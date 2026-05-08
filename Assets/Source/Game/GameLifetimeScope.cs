using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration;
using BalloonParty.Slots;

namespace BalloonParty.Game
{
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameConfiguration _gameConfiguration;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterMessagePipe();

            builder.RegisterInstance<IGameConfiguration>(_gameConfiguration);

            builder.Register<SlotGrid>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<SlotGridView>();
        }
    }
}