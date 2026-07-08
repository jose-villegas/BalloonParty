using BalloonParty.Editor.EffectPreview;
using BalloonParty.Item.Laser;
using NaughtyAttributes.Editor;
using UnityEditor;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Editor
{
    [CustomEditor(typeof(LaserView))]
    public class LaserViewEditor : NaughtyInspector
    {
        private EffectViewPreviewPlayer _player;

        private LaserView Target => (LaserView)target;

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

            var module = new LaserPreviewModule(Target);
            _player = new EffectViewPreviewPlayer(
                module,
                "Laser Preview",
                ItemType.Laser,
                Repaint);
        }
    }
}
