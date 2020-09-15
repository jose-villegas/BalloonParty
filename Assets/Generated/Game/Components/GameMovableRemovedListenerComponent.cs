//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Entitas.CodeGeneration.Plugins.ComponentEntityApiGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
public partial class GameEntity {

    public MovableRemovedListenerComponent movableRemovedListener { get { return (MovableRemovedListenerComponent)GetComponent(GameComponentsLookup.MovableRemovedListener); } }
    public bool hasMovableRemovedListener { get { return HasComponent(GameComponentsLookup.MovableRemovedListener); } }

    public void AddMovableRemovedListener(System.Collections.Generic.List<IMovableRemovedListener> newValue) {
        var index = GameComponentsLookup.MovableRemovedListener;
        var component = (MovableRemovedListenerComponent)CreateComponent(index, typeof(MovableRemovedListenerComponent));
        component.value = newValue;
        AddComponent(index, component);
    }

    public void ReplaceMovableRemovedListener(System.Collections.Generic.List<IMovableRemovedListener> newValue) {
        var index = GameComponentsLookup.MovableRemovedListener;
        var component = (MovableRemovedListenerComponent)CreateComponent(index, typeof(MovableRemovedListenerComponent));
        component.value = newValue;
        ReplaceComponent(index, component);
    }

    public void RemoveMovableRemovedListener() {
        RemoveComponent(GameComponentsLookup.MovableRemovedListener);
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

    static Entitas.IMatcher<GameEntity> _matcherMovableRemovedListener;

    public static Entitas.IMatcher<GameEntity> MovableRemovedListener {
        get {
            if (_matcherMovableRemovedListener == null) {
                var matcher = (Entitas.Matcher<GameEntity>)Entitas.Matcher<GameEntity>.AllOf(GameComponentsLookup.MovableRemovedListener);
                matcher.componentNames = GameComponentsLookup.componentNames;
                _matcherMovableRemovedListener = matcher;
            }

            return _matcherMovableRemovedListener;
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

    public void AddMovableRemovedListener(IMovableRemovedListener value) {
        var listeners = hasMovableRemovedListener
            ? movableRemovedListener.value
            : new System.Collections.Generic.List<IMovableRemovedListener>();
        listeners.Add(value);
        ReplaceMovableRemovedListener(listeners);
    }

    public void RemoveMovableRemovedListener(IMovableRemovedListener value, bool removeComponentWhenEmpty = true) {
        var listeners = movableRemovedListener.value;
        listeners.Remove(value);
        if (removeComponentWhenEmpty && listeners.Count == 0) {
            RemoveMovableRemovedListener();
        } else {
            ReplaceMovableRemovedListener(listeners);
        }
    }
}