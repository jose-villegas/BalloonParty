using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Audio.Editor
{
    // Strategy seam: a source of candidate clips for a fetch prompt. Freesound is the first impl;
    // a text-to-SFX generator could be a second, chosen from the window, with no other changes.
    internal interface ISfxProvider
    {
        UniTask<IReadOnlyList<SfxCandidate>> FetchAsync(SfxFetchRequest request, CancellationToken cancellationToken);
    }
}
