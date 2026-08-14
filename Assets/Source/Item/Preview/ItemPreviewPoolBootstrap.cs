using BalloonParty.Configuration;
using BalloonParty.Shared.Pool;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Registers and pre-warms the <see cref="HighlightTrail" /> pen pool to
    ///     <see cref="IItemPreviewConfig.MaxPens" /> — the same hard ceiling <see cref="ItemPreviewTicker" />
    ///     already never exceeds — so the first telegraph never instantiates a pen mid-frame. Mirrors
    ///     <c>SfxVoicePoolBootstrap</c>: a cap that doubles as the prewarm count, read from injected config.
    /// </summary>
    internal sealed class ItemPreviewPoolBootstrap : IStartable
    {
        private readonly PoolManager _poolManager;
        private readonly HighlightTrail _prefab;
        private readonly IItemPreviewConfig _config;

        [Inject]
        public ItemPreviewPoolBootstrap(PoolManager poolManager, HighlightTrail prefab, IItemPreviewConfig config)
        {
            _poolManager = poolManager;
            _prefab = prefab;
            _config = config;
        }

        public void Start()
        {
            // Unassigned prefab keeps the telegraph opt-in — ItemPreviewTicker no-ops the same way — rather
            // than a hard startup failure before a pen prefab is authored.
            if (_prefab == null)
            {
                return;
            }

            // Same key ItemPreviewTicker derives from the prefab's own name, so the channel this prewarms
            // is the exact one the ticker's own GetOrRegister resolves to on first Show.
            var key = _prefab.name;
            _poolManager.Register(key, new SimplePoolChannel<HighlightTrail>(_prefab));
            _poolManager.Prewarm(key, _config.MaxPens);
        }
    }
}
