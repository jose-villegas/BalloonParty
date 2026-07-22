namespace BalloonParty.EditorUI.Tables
{
    /// <summary>Interface for items that can be selected in a table row.</summary>
    public interface ISelectable
    {
        bool Selected { get; set; }
    }
}
