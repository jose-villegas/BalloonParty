using TMPro.EditorUtilities;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     The inspector for <c>BalloonParty/Text/Pencil</c>: TMP's own SDF panels, with a Pencil panel on top
    ///     for the grain and the ragged write-on edge.
    /// </summary>
    internal sealed class PencilTextShaderGUI : TMP_SDFShaderGUI
    {
        private static bool _pencilExpanded = true;

        protected override void DoGUI()
        {
            _pencilExpanded = BeginPanel("Pencil", _pencilExpanded);
            if (_pencilExpanded)
            {
                DoTexture2D("_PencilNoiseTex", "Noise (edge RGB, grain A)");
                DoSlider("_PencilGrain", "Grain");
                DoSlider("_PencilGrainScale", "Grain Scale");
                DoSlider("_PencilEdgeRoughness", "Edge Roughness");
                DoSlider("_PencilEdgeScale", "Edge Noise Scale");
            }

            EndPanel();
            base.DoGUI();
        }
    }
}
