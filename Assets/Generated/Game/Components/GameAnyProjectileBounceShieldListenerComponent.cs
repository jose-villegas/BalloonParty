//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Entitas.CodeGeneration.Plugins.ComponentEntityApiGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
public partial class GameEntity {

    public AnyProjectileBounceShieldListenerComponent anyProjectileBounceShieldListener { get { return (AnyProjectileBounceShieldListenerComponent)GetComponent(GameComponentsLookup.AnyProjectileBounceShieldListener); } }
    public bool hasAnyProjectileBounceShieldListener { get { return HasComponent(GameComponentsLookup.AnyProjectileBounceShieldListener); } }

    public void AddAnyProjectileBounceShieldListener(System.Collections.Generic.List<IAnyProjectileBounceShieldListener> newValue) {
        var index = GameComponentsLookup.AnyProjectileBounceShieldListener;
        var component = (AnyProjectileBounceShieldListenerComponent)CreateComponent(index, typeof(AnyProjectileBounceShieldListenerComponent));
        component.value = newValue;
        AddComponent(index, component);
    }

    public void ReplaceAnyProjectileBounceShieldListener(System.Collections.Generic.List<IAnyProjectileBounceShieldListener> newValue) {
        var index = GameComponentsLookup.AnyProjectileBounceShieldListener;
        var component = (AnyProjectileBounceShieldListenerComponent)CreateComponent(index, typeof(AnyProjectileBounceShieldListenerComponent));
        component.value = newValue;
        ReplaceComponent(index, component);
    }

    public void RemoveAnyProjectileBounceShieldListener() {
        RemoveComponent(GameComponentsLookup.AnyProjectileBounceShieldListener);
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

    static Entitas.IMatcher<GameEntity> _matcherAnyProjectileBounceShieldListener;

    public static Entitas.IMatcher<GameEntity> AnyProjectileBounceShieldListener {
        get {
            if (_matcherAnyProjectileBounceShieldListener == null) {
                var matcher = (Entitas.Matcher<GameEntity>)Entitas.Matcher<GameEntity>.AllOf(GameComponentsLookup.AnyProjectileBounceShieldListener);
                matcher.componentNames = GameComponentsLookup.componentNames;
                _matcherAnyProjectileBounceShieldListener = matcher;
            }

            return _matcherAnyProjectileBounceShieldListener;
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

    public void AddAnyProjectileBounceShieldListener(IAnyProjectileBounceShieldListener value) {
        var listeners = hasAnyProjectileBounceShieldListener
            ? anyProjectileBounceShieldListener.value
            : new System.Collections.Generic.List<IAnyProjectileBounceShieldListener>();
        listeners.Add(value);
        ReplaceAnyProjectileBounceShieldListener(listeners);
    }

    public void RemoveAnyProjectileBounceShieldListener(IAnyProjectileBounceShieldListener value, bool removeComponentWhenEmpty = true) {
        var listeners = anyProjectileBounceShieldListener.value;
        listeners.Remove(value);
        if (removeComponentWhenEmpty && listeners.Count == 0) {
            RemoveAnyProjectileBounceShieldListener();
        } else {
            ReplaceAnyProjectileBounceShieldListener(listeners);
        }
    }
}
