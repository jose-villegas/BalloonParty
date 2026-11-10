using UniRx;

namespace BalloonParty.Balloon.Model
{
    /// <summary>A balloon that can join an unbreakable formation; the view reads it to swap its lights.</summary>
    internal interface IFormationMember
    {
        IReadOnlyReactiveProperty<bool> InFormation { get; }
    }
}
