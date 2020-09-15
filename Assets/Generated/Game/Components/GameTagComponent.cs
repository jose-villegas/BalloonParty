//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Entitas.CodeGeneration.Plugins.ComponentEntityApiGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
public partial class GameEntity {

    public TagComponent tag { get { return (TagComponent)GetComponent(GameComponentsLookup.Tag); } }
    public bool hasTag { get { return HasComponent(GameComponentsLookup.Tag); } }

    public void AddTag(string newValue) {
        var index = GameComponentsLookup.Tag;
        var component = (TagComponent)CreateComponent(index, typeof(TagComponent));
        component.Value = newValue;
        AddComponent(index, component);
    }

    public void ReplaceTag(string newValue) {
        var index = GameComponentsLookup.Tag;
        var component = (TagComponent)CreateComponent(index, typeof(TagComponent));
        component.Value = newValue;
        ReplaceComponent(index, component);
    }

    public void RemoveTag() {
        RemoveComponent(GameComponentsLookup.Tag);
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

    static Entitas.IMatcher<GameEntity> _matcherTag;

    public static Entitas.IMatcher<GameEntity> Tag {
        get {
            if (_matcherTag == null) {
                var matcher = (Entitas.Matcher<GameEntity>)Entitas.Matcher<GameEntity>.AllOf(GameComponentsLookup.Tag);
                matcher.componentNames = GameComponentsLookup.componentNames;
                _matcherTag = matcher;
            }

            return _matcherTag;
        }
    }
}