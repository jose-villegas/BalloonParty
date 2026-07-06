using BalloonParty.Editor.EffectPreview;
using BalloonParty.Item.Paint;
using NaughtyAttributes.Editor;
using UnityEditor;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Editor
{
    [CustomEditor(typeof(PaintSplashView))]
    public class PaintSplashViewEditor : NaughtyInspector
    {
        private EffectViewPreviewPlayer _player;

        private PaintSplashView Target => (PaintSplashView)target;

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

        private void OnSceneGUI()
        {
            EnsurePlayer();
            _player.DrawSceneGizmos();
        }

        private void EnsurePlayer()
        {
            if (_player != null)
            {
                return;
            }

            var module = new PaintSplashPreviewModule(Target);
            _player = new EffectViewPreviewPlayer(
                module,
                "Paint Splash Preview",
                Configuration.Items.ItemType.Paint,
                Repaint);
        }
    }
}
