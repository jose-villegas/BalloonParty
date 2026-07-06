using BalloonParty.Configuration;
using BalloonParty.Editor.EffectPreview;
using BalloonParty.Item.Lightning;
using NaughtyAttributes.Editor;
using UnityEditor;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Editor
{
    [CustomEditor(typeof(ChainLightningView))]
    public class ChainLightningViewEditor : NaughtyInspector
    {
        private EffectViewPreviewPlayer _player;

        private ChainLightningView Target => (ChainLightningView)target;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EnsurePlayer();
            _player.DrawInspectorGUI();
        }

        protected override void OnDisable()
        {
            _player?.Stop();
            base.OnDisable();
        }

        private void EnsurePlayer()
        {
            if (_player != null)
            {
                return;
            }

            var module = new ChainLightningPreviewModule(Target);
            _player = new EffectViewPreviewPlayer(
                module,
                "Chain Lightning Preview",
                ItemType.Lightning,
                Repaint);
        }
    }
}
