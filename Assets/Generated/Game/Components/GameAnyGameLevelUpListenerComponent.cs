//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Entitas.CodeGeneration.Plugins.ComponentEntityApiGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
public partial class GameEntity {

    public AnyGameLevelUpListenerComponent anyGameLevelUpListener { get { return (AnyGameLevelUpListenerComponent)GetComponent(GameComponentsLookup.AnyGameLevelUpListener); } }
    public bool hasAnyGameLevelUpListener { get { return HasComponent(GameComponentsLookup.AnyGameLevelUpListener); } }

    public void AddAnyGameLevelUpListener(System.Collections.Generic.List<IAnyGameLevelUpListener> newValue) {
        var index = GameComponentsLookup.AnyGameLevelUpListener;
        var component = (AnyGameLevelUpListenerComponent)CreateComponent(index, typeof(AnyGameLevelUpListenerComponent));
        component.value = newValue;
        AddComponent(index, component);
    }

    public void ReplaceAnyGameLevelUpListener(System.Collections.Generic.List<IAnyGameLevelUpListener> newValue) {
        var index = GameComponentsLookup.AnyGameLevelUpListener;
        var component = (AnyGameLevelUpListenerComponent)CreateComponent(index, typeof(AnyGameLevelUpListenerComponent));
        component.value = newValue;
        ReplaceComponent(index, component);
    }

    public void RemoveAnyGameLevelUpListener() {
        RemoveComponent(GameComponentsLookup.AnyGameLevelUpListener);
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

    static Entitas.IMatcher<GameEntity> _matcherAnyGameLevelUpListener;

    public static Entitas.IMatcher<GameEntity> AnyGameLevelUpListener {
        get {
            if (_matcherAnyGameLevelUpListener == null) {
                var matcher = (Entitas.Matcher<GameEntity>)Entitas.Matcher<GameEntity>.AllOf(GameComponentsLookup.AnyGameLevelUpListener);
                matcher.componentNames = GameComponentsLookup.componentNames;
                _matcherAnyGameLevelUpListener = matcher;
            }

            return _matcherAnyGameLevelUpListener;
        }
    }
}

//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Entitas.CodeGeneration.Plugins.EventEntityApiGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
public partial class GameEntity {

    public void AddAnyGameLevelUpListener(IAnyGameLevelUpListener value) {
        var listeners = hasAnyGameLevelUpListener
            ? anyGameLevelUpListener.value
            : new System.Collections.Generic.List<IAnyGameLevelUpListener>();
        listeners.Add(value);
        ReplaceAnyGameLevelUpListener(listeners);
    }

    public void RemoveAnyGameLevelUpListener(IAnyGameLevelUpListener value, bool removeComponentWhenEmpty = true) {
        var listeners = anyGameLevelUpListener.value;
        listeners.Remove(value);
        if (removeComponentWhenEmpty && listeners.Count == 0) {
            RemoveAnyGameLevelUpListener();
        } else {
            ReplaceAnyGameLevelUpListener(listeners);
        }
    }
}