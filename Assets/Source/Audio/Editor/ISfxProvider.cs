using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Audio.Editor
{
    // Strategy seam: a source of candidate clips for a fetch prompt. Freesound is the first impl;
    // a text-to-SFX generator could be a second, selected in the inspector, with no other changes.
    internal interface ISfxProvider
    {
        UniTask<IReadOnlyList<SfxCandidate>> FetchAsync(SfxFetchRequest request, CancellationToken cancellationToken);
    }
}
