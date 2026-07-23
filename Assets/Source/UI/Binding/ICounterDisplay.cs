namespace BalloonParty.UI.Binding
{
    /// <summary>Strategy for how a counter label renders its numeric value.</summary>
    internal interface ICounterDisplay
    {
        void Display(int value);
        void ShowPlaceholder();
    }
}
