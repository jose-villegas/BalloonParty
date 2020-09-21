//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Entitas.CodeGeneration.Plugins.ComponentEntityApiGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
public partial class GameEntity {

    public RightComponent right { get { return (RightComponent)GetComponent(GameComponentsLookup.Right); } }
    public bool hasRight { get { return HasComponent(GameComponentsLookup.Right); } }

    public void AddRight(UnityEngine.Vector3 newValue) {
        var index = GameComponentsLookup.Right;
        var component = (RightComponent)CreateComponent(index, typeof(RightComponent));
        component.Value = newValue;
        AddComponent(index, component);
    }

    public void ReplaceRight(UnityEngine.Vector3 newValue) {
        var index = GameComponentsLookup.Right;
        var component = (RightComponent)CreateComponent(index, typeof(RightComponent));
        component.Value = newValue;
        ReplaceComponent(index, component);
    }

    public void RemoveRight() {
        RemoveComponent(GameComponentsLookup.Right);
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

    static Entitas.IMatcher<GameEntity> _matcherRight;

    public static Entitas.IMatcher<GameEntity> Right {
        get {
            if (_matcherRight == null) {
                var matcher = (Entitas.Matcher<GameEntity>)Entitas.Matcher<GameEntity>.AllOf(GameComponentsLookup.Right);
                matcher.componentNames = GameComponentsLookup.componentNames;
                _matcherRight = matcher;
            }

            return _matcherRight;
        }
    }
}