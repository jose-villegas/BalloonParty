using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Shared.Diagnostics;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BalloonParty.UI.CardReveal
{
    /// <summary>
    ///     Keeps in memory only the pop sheets the reveal needs: the one playing and the one the next reveal will
    ///     play (<see cref="Game.CardReveal.ICardReveals.NextPop" />), loaded ahead while the game plays so a
    ///     reveal starts on time. Each sheet is several megabytes, so all of them loaded with the scene would be
    ///     tens. Read from <c>Resources</c> by the paths in the settings; a sheet let go is unloaded, or, if it's
    ///     still loading, as soon as it arrives.
    /// </summary>
    internal sealed class PopSheetLoader : IDisposable
    {
        private readonly IReadOnlyList<string> _paths;
        private readonly Func<string, UniTask<Texture2D>> _load;
        private readonly Action<Texture2D> _unload;
        private readonly Dictionary<int, Sheet> _sheets = new();
        private readonly List<int> _letGo = new();

        internal PopSheetLoader(IReadOnlyList<string> paths)
            : this(paths, LoadFromResources, Resources.UnloadAsset)
        {
        }

        internal PopSheetLoader(IReadOnlyList<string> paths, Func<string, UniTask<Texture2D>> load,
            Action<Texture2D> unload)
        {
            _paths = paths;
            _load = load;
            _unload = unload;
        }

        public void Dispose()
        {
            Hold(-1, -1);
        }

        /// <summary>
        ///     Keeps the sheets of <paramref name="playing" /> and <paramref name="next" /> (either -1 for none),
        ///     starting to load any not yet held, and lets the others go.
        /// </summary>
        internal void Hold(int playing, int next)
        {
            foreach (var (pop, sheet) in _sheets)
            {
                sheet.Wanted = pop == playing || pop == next;
            }

            Want(playing);
            Want(next);

            _letGo.Clear();
            foreach (var (pop, sheet) in _sheets)
            {
                if (!sheet.Wanted && sheet.Loaded)
                {
                    _letGo.Add(pop);
                }
            }

            foreach (var pop in _letGo)
            {
                Unload(pop);
            }
        }

        /// <summary>
        ///     Pop <paramref name="pop" />'s sheet once it has loaded: at once if it was held ahead. Held from here
        ///     on if it wasn't. Null if there is no such pop, its sheet is missing, or it was let go before it
        ///     arrived (then it's unloaded).
        /// </summary>
        internal async UniTask<Texture2D> GetAsync(int pop, CancellationToken token)
        {
            var sheet = Want(pop);
            if (sheet == null)
            {
                return null;
            }

            if (!sheet.Loaded)
            {
                await sheet.Loading.AttachExternalCancellation(token);
            }

            return sheet.Wanted ? sheet.Texture : null;
        }

        // Holds pop's sheet, loading it if it isn't already. A sheet let go while it was still loading is still
        // here, and is simply kept: a second load of the same path would hand back the same texture, which the
        // first one's arrival would then unload.
        private Sheet Want(int pop)
        {
            if (pop < 0 || pop >= _paths.Count)
            {
                return null;
            }

            if (_sheets.TryGetValue(pop, out var sheet))
            {
                sheet.Wanted = true;
                return sheet;
            }

            // Wanted before the load starts: a sheet already in memory arrives at once.
            sheet = new Sheet { Wanted = true };
            _sheets.Add(pop, sheet);
            sheet.Loading = LoadAsync(pop, sheet).Preserve();
            return sheet;
        }

        private async UniTask LoadAsync(int pop, Sheet sheet)
        {
            var path = _paths[pop];
            Texture2D texture;
            try
            {
                texture = await _load(path);
            }
            catch (Exception e)
            {
                // Taken as missing, so the sheet still settles: left loading, it could never be let go.
                Log.Warn("CardReveal", $"Loading the pop sheet at Resources/{path} failed: {e.Message}");
                texture = null;
            }

            if (texture == null)
            {
                Log.Warn("CardReveal", $"No pop sheet at Resources/{path}.");
            }

            sheet.Texture = texture;
            sheet.Loaded = true;
            if (!sheet.Wanted)
            {
                Unload(pop);
            }
        }

        private void Unload(int pop)
        {
            var texture = _sheets[pop].Texture;
            _sheets.Remove(pop);
            if (texture != null)
            {
                _unload(texture);
            }
        }

        private static async UniTask<Texture2D> LoadFromResources(string path)
        {
            return await Resources.LoadAsync<Texture2D>(path) as Texture2D;
        }

        private sealed class Sheet
        {
            internal UniTask Loading;
            internal Texture2D Texture;
            internal bool Loaded;
            internal bool Wanted;
        }
    }
}
