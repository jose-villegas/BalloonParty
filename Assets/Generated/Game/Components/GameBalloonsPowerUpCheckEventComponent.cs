//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Entitas.CodeGeneration.Plugins.ComponentEntityApiGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
public partial class GameEntity {

    static readonly BalloonsPowerUpCheckEventComponent balloonsPowerUpCheckEventComponent = new BalloonsPowerUpCheckEventComponent();

    public bool isBalloonsPowerUpCheckEvent {
        get { return HasComponent(GameComponentsLookup.BalloonsPowerUpCheckEvent); }
        set {
            if (value != isBalloonsPowerUpCheckEvent) {
                var index = GameComponentsLookup.BalloonsPowerUpCheckEvent;
                if (value) {
                    var componentPool = GetComponentPool(index);
                    var component = componentPool.Count > 0
                            ? componentPool.Pop()
                            : balloonsPowerUpCheckEventComponent;

                    AddComponent(index, component);
                } else {
                    RemoveComponent(index);
                }
            }
        }
    }
}

//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Entitas.CodeGeneration.Plugins.ComponentMatcherApiGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
public sealed partial class GameMatcher {

    static Entitas.IMatcher<GameEntity> _matcherBalloonsPowerUpCheckEvent;

    public static Entitas.IMatcher<GameEntity> BalloonsPowerUpCheckEvent {
        get {
            if (_matcherBalloonsPowerUpCheckEvent == null) {
                var matcher = (Entitas.Matcher<GameEntity>)Entitas.Matcher<GameEntity>.AllOf(GameComponentsLookup.BalloonsPowerUpCheckEvent);
                matcher.componentNames = GameComponentsLookup.componentNames;
                _matcherBalloonsPowerUpCheckEvent = matcher;
            }

            return _matcherBalloonsPowerUpCheckEvent;
        }
    }
}
