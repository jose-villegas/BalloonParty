//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Entitas.CodeGeneration.Plugins.EventSystemGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
public sealed class BalloonLastColorPopCountEventSystem : Entitas.ReactiveSystem<GameEntity> {

    readonly System.Collections.Generic.List<IBalloonLastColorPopCountListener> _listenerBuffer;

    public BalloonLastColorPopCountEventSystem(Contexts contexts) : base(contexts.game) {
        _listenerBuffer = new System.Collections.Generic.List<IBalloonLastColorPopCountListener>();
    }

    protected override Entitas.ICollector<GameEntity> GetTrigger(Entitas.IContext<GameEntity> context) {
        return Entitas.CollectorContextExtension.CreateCollector(
            context, Entitas.TriggerOnEventMatcherExtension.Added(GameMatcher.BalloonLastColorPopCount)
        );
    }

    protected override bool Filter(GameEntity entity) {
        return entity.hasBalloonLastColorPopCount && entity.hasBalloonLastColorPopCountListener;
    }

    protected override void Execute(System.Collections.Generic.List<GameEntity> entities) {
        foreach (var e in entities) {
            var component = e.balloonLastColorPopCount;
            _listenerBuffer.Clear();
            _listenerBuffer.AddRange(e.balloonLastColorPopCountListener.value);
            foreach (var listener in _listenerBuffer) {
                listener.OnBalloonLastColorPopCount(e, component.Value);
            }
        }
    }
}
