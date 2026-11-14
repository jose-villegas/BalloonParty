using System.Collections.Generic;
using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.Item.Snipe
{
    /// <summary>
    ///     One pencil stroke along a Snipe lance's run, carrying the aim trace's smoke-and-glitter material so
    ///     the shot's path reads in the same carbon hand as the cannon's aim. The line is built from chunks of
    ///     a fixed length, each its own two-point <see cref="LineRenderer" />, so the erase can wipe them away
    ///     one at a time. Driven entirely by <see cref="SnipeLanceLines" />; it only renders what it is told.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    internal sealed class SnipeLanceLineView : MonoBehaviour, IPoolable
    {
        [SerializeField] private LineRenderer _line;

        [Tooltip("World units a second the pencil tip runs along the lance's run, behind the shot and on to the wall.")]
        [SerializeField] [Min(0f)] private float _tipSpeed = 12f;

        [Tooltip("Seconds the finished line stays whole before it starts being erased.")]
        [SerializeField] [Min(0f)] private float _holdSeconds = 1f;

        [Tooltip("Seconds the erase takes, from the first chunk wiped to the last one gone.")]
        [SerializeField] [Min(0f)] private float _eraseSeconds = 0.7f;

        [Tooltip("World length of one chunk the erase wipes away on its own.")]
        [SerializeField] [Min(0.05f)] private float _chunkLength = 0.7f;

        [Tooltip("Share of the erase each chunk takes to wipe; the rest staggers their turns.")]
        [SerializeField] [Range(0f, 1f)] private float _chunkEraseShare = 0.3f;

        [Tooltip("World distance a chunk smears out to each side as it is rubbed away.")]
        [SerializeField] [Min(0f)] private float _smearDistance = 0.2f;

        [Tooltip("Extra width a chunk spreads to at the height of its rub, as a share of the line's width.")]
        [SerializeField] [Min(0f)] private float _smearSpread = 1.2f;

        [Tooltip("How much less the chunk's start smears than its end, so the rub drags the line askew.")]
        [SerializeField] [Range(0f, 1f)] private float _smearDrag = 0.4f;

        // The prefab's own renderer is the first chunk; the rest are made from it as longer lines need them,
        // and stay with the pooled line from then on.
        private readonly List<LineRenderer> _chunks = new();
        private readonly List<int> _turns = new();
        private readonly List<float> _sides = new();

        private Vector3 _from;
        private Color _color = Color.white;
        private float _width;
        private bool _shuffled;

        internal SnipeLanceStrokeTiming Timing => new(_tipSpeed, _holdSeconds, _eraseSeconds);

        private void Awake()
        {
            if (_line == null)
            {
                _line = GetComponent<LineRenderer>();
            }

            _line.useWorldSpace = true;
            _width = _line.widthMultiplier;
            _chunks.Add(_line);
            HideAll();
        }

        public void OnSpawned()
        {
            HideAll();
        }

        public void OnDespawned()
        {
            HideAll();
        }

        internal void Begin(Vector3 from, Color color)
        {
            _from = from;
            _color = color;
            _shuffled = false;
            HideAll();
        }

        /// <param name="end">The run's end now: the shot while it flies, the wall once reached.</param>
        /// <param name="drawn">How far along the run the pencil tip has drawn, in world units.</param>
        /// <param name="erased">How far the erase has got, 0 (untouched) to 1 (gone).</param>
        internal void Render(Vector3 end, float drawn, float erased)
        {
            var run = end - _from;
            var length = run.magnitude;
            drawn = Mathf.Min(drawn, length);
            if (drawn <= 0f || erased >= 1f)
            {
                HideAll();
                return;
            }

            var direction = run / length;
            var across = new Vector3(-direction.y, direction.x, 0f);
            var count = Mathf.CeilToInt(drawn / _chunkLength);
            EnsureChunks(count);
            if (erased > 0f && !_shuffled)
            {
                ShuffleTurns(count);
                _shuffled = true;
            }

            for (var i = 0; i < _chunks.Count; i++)
            {
                var chunk = _chunks[i];
                if (i >= count)
                {
                    chunk.positionCount = 0;
                    continue;
                }

                var wiped = erased > 0f ? SnipeLanceErase.ChunkErased(erased, _turns[i], count, _chunkEraseShare) : 0f;
                if (wiped >= 1f)
                {
                    chunk.positionCount = 0;
                    continue;
                }

                // The rub only moves a chunk sideways, never along the line: the material tiles from a line's
                // start, so sliding it along would slide the glitter under the eraser.
                var start = i * _chunkLength;
                var stop = Mathf.Min(start + _chunkLength, drawn);
                var smear = across * (_sides[i] * _smearDistance * SnipeLanceErase.Smear(wiped));

                var color = _color;
                color.a *= 1f - (wiped * wiped);
                chunk.startColor = color;
                chunk.endColor = color;
                chunk.widthMultiplier = _width * (1f + (_smearSpread * SnipeLanceErase.Spread(wiped)));
                chunk.positionCount = 2;
                chunk.SetPosition(0, _from + (direction * start) + (smear * (1f - _smearDrag)));
                chunk.SetPosition(1, _from + (direction * stop) + smear);
            }
        }

        private void HideAll()
        {
            foreach (var chunk in _chunks)
            {
                chunk.positionCount = 0;
            }
        }

        private void EnsureChunks(int count)
        {
            while (_chunks.Count < count)
            {
                _chunks.Add(CreateChunk(_chunks.Count));
            }

            while (_turns.Count < _chunks.Count)
            {
                _turns.Add(_turns.Count);
                _sides.Add(1f);
            }
        }

        private LineRenderer CreateChunk(int index)
        {
            var go = new GameObject($"Chunk{index}") { layer = gameObject.layer };
            go.transform.SetParent(transform, false);
            var chunk = go.AddComponent<LineRenderer>();
            chunk.useWorldSpace = true;
            chunk.sharedMaterials = _line.sharedMaterials;
            chunk.widthCurve = _line.widthCurve;
            chunk.widthMultiplier = _line.widthMultiplier;
            chunk.textureMode = _line.textureMode;
            chunk.textureScale = _line.textureScale;
            chunk.numCapVertices = _line.numCapVertices;
            chunk.numCornerVertices = _line.numCornerVertices;
            chunk.alignment = _line.alignment;
            chunk.shadowCastingMode = _line.shadowCastingMode;
            chunk.receiveShadows = false;
            chunk.lightProbeUsage = _line.lightProbeUsage;
            chunk.reflectionProbeUsage = _line.reflectionProbeUsage;
            chunk.sortingLayerID = _line.sortingLayerID;
            chunk.sortingOrder = _line.sortingOrder;
            chunk.positionCount = 0;
            return chunk;
        }

        private void ShuffleTurns(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _turns[i] = i;
                _sides[i] = Random.value < 0.5f ? -1f : 1f;
            }

            for (var i = count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (_turns[i], _turns[j]) = (_turns[j], _turns[i]);
            }
        }
    }
}
