using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     Loose soap bubbles, one per sprite, for the views that send bubbles off on their own
    ///     (<see cref="BubbleMergeView" />, <see cref="RipeBubbleBurstView" />). Each is a plain
    ///     <see cref="SpriteRenderer" /> under the owning view, disabled and kept when returned, so a busy board reuses
    ///     them rather than creating one each time. The caller that rents a bubble returns it.
    /// </summary>
    internal sealed class SoapBubbleSprites
    {
        private readonly Stack<SpriteRenderer> _free = new();
        private readonly MaterialPropertyBlock _block = new();
        private readonly Transform _parent;
        private readonly string _name;

        internal SoapBubbleSprites(Transform parent, string name)
        {
            _parent = parent;
            _name = name;
        }

        /// <summary>A single bubble in <paramref name="source" />'s look: its material, sprite, tint, sorting layer and
        /// the float and spin its property block carries, drawn as one bubble.</summary>
        internal SpriteRenderer RentLike(SpriteRenderer source, int sortingOrder)
        {
            var bubble = Take(source.sharedMaterial, source.sprite, source.sortingLayerID, sortingOrder);
            bubble.color = source.color;
            source.GetPropertyBlock(_block);
            _block.SetFloat(SoapBubbleShader.BubbleCountId, 1f);
            bubble.SetPropertyBlock(_block);
            return bubble;
        }

        /// <summary>A single bubble of <paramref name="material" />, floating and spinning on its own clock
        /// (<see cref="SoapBubbleShader.SetLoneBubble" />). It is clear unless <paramref name="flush" /> has an alpha:
        /// then it is filled with that colour, by that much, as a ripe cluster is with its warning.</summary>
        internal SpriteRenderer Rent(
            Material material, Sprite sprite, int sortingLayerId, int sortingOrder, Color flush = default)
        {
            var bubble = Take(material, sprite, sortingLayerId, sortingOrder);
            bubble.color = Color.white;
            _block.Clear();
            SoapBubbleShader.SetLoneBubble(_block);
            if (flush.a > 0f)
            {
                _block.SetColor(SoapBubbleShader.FlushId, flush);
            }

            bubble.SetPropertyBlock(_block);
            return bubble;
        }

        internal void Return(SpriteRenderer bubble)
        {
            if (bubble == null)
            {
                return;
            }

            bubble.enabled = false;
            _free.Push(bubble);
        }

        private SpriteRenderer Take(Material material, Sprite sprite, int sortingLayerId, int sortingOrder)
        {
            SpriteRenderer bubble;
            if (_free.Count > 0)
            {
                bubble = _free.Pop();
            }
            else
            {
                bubble = new GameObject(_name).AddComponent<SpriteRenderer>();
                bubble.transform.SetParent(_parent, worldPositionStays: false);
            }

            bubble.sprite = sprite;
            bubble.sharedMaterial = material;
            bubble.sortingLayerID = sortingLayerId;
            bubble.sortingOrder = sortingOrder;
            bubble.enabled = true;
            return bubble;
        }
    }
}
