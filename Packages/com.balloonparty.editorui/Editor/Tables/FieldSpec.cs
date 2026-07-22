namespace BalloonParty.EditorUI.Tables
{
    public enum FieldDrawMode
    {
        Float,
        Int,
        Curve,
        Property,
        RangedInt
    }

    public readonly struct FieldSpec
    {
        public readonly string PropertyPath;
        public readonly string SubHeader;
        public readonly float Width;
        public readonly FieldDrawMode DrawMode;

        public FieldSpec(string propertyPath, string subHeader, float width, FieldDrawMode drawMode)
        {
            PropertyPath = propertyPath;
            SubHeader = subHeader;
            Width = width;
            DrawMode = drawMode;
        }
    }
}
