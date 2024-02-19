//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Entitas.CodeGeneration.Plugins.ComponentEntityApiGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
public partial class GameEntity {

    public ProjectileBounceShieldComponent projectileBounceShield { get { return (ProjectileBounceShieldComponent)GetComponent(GameComponentsLookup.ProjectileBounceShield); } }
    public bool hasProjectileBounceShield { get { return HasComponent(GameComponentsLookup.ProjectileBounceShield); } }

    public void AddProjectileBounceShield(float newValue) {
        var index = GameComponentsLookup.ProjectileBounceShield;
        var component = (ProjectileBounceShieldComponent)CreateComponent(index, typeof(ProjectileBounceShieldComponent));
        component.Value = newValue;
        AddComponent(index, component);
    }

    public void ReplaceProjectileBounceShield(float newValue) {
        var index = GameComponentsLookup.ProjectileBounceShield;
        var component = (ProjectileBounceShieldComponent)CreateComponent(index, typeof(ProjectileBounceShieldComponent));
        component.Value = newValue;
        ReplaceComponent(index, component);
    }

    public void RemoveProjectileBounceShield() {
        RemoveComponent(GameComponentsLookup.ProjectileBounceShield);
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

    static Entitas.IMatcher<GameEntity> _matcherProjectileBounceShield;

    public static Entitas.IMatcher<GameEntity> ProjectileBounceShield {
        get {
            if (_matcherProjectileBounceShield == null) {
                var matcher = (Entitas.Matcher<GameEntity>)Entitas.Matcher<GameEntity>.AllOf(GameComponentsLookup.ProjectileBounceShield);
                matcher.componentNames = GameComponentsLookup.componentNames;
                _matcherProjectileBounceShield = matcher;
            }

            return _matcherProjectileBounceShield;
        }
    }
}
