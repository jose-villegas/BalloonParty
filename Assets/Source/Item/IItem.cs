using BalloonParty.Configuration;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Item
{
    public interface IItem
    {
        ItemType Type { get; }
        UniTask Activate();
    }
}
