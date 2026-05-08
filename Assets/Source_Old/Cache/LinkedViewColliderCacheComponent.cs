using System.Collections.Generic;
using Entitas;
using UnityEngine;

public sealed class LinkedViewColliderCacheComponent : IComponent
{
    private Dictionary<Collider2D, ILinkedView> _colliderCache;
    private Dictionary<ILinkedView, Collider2D> _colliderCacheMirror;

    public void Cache(ILinkedView linkedView, Collider2D collider2D)
    {
        if (_colliderCache == null)
        {
            _colliderCache = new Dictionary<Collider2D, ILinkedView>();
            _colliderCacheMirror = new Dictionary<ILinkedView, Collider2D>();
        }

        if (!_colliderCache.TryAdd(collider2D, linkedView) && !_colliderCacheMirror.TryAdd(linkedView, collider2D))
        {
            _colliderCache[collider2D] = linkedView;
            _colliderCacheMirror[linkedView] = collider2D;
        }
    }
    
    public void Remove(Collider2D collider2D)
    {
        if (_colliderCache.TryGetValue(collider2D, out var view))
        {
            _colliderCacheMirror.Remove(view);
            _colliderCache.Remove(collider2D);
        }
    }

        public void Remove(ILinkedView view)
    {
        if (_colliderCacheMirror.TryGetValue(view, out var collider2D))
        {
            _colliderCache.Remove(collider2D);
            _colliderCacheMirror.Remove(view);
        }
    }

    public ILinkedView Fetch(Collider2D collider2D)
    {
        if (_colliderCache != null)
        {
            if (_colliderCache.TryGetValue(collider2D, out var linkedView))
            {
                return linkedView;
            }
            else
            {
                return collider2D.GetComponent<ILinkedView>();
            }
        }

        return null;
    }

    public Collider2D Fetch(ILinkedView linkedView)
    {
        if (_colliderCacheMirror != null)
        {
            if (_colliderCacheMirror.TryGetValue(linkedView, out var collider2D))
            {
                return collider2D;
            }
            else
            {
                return (linkedView as LinkedViewController).GetComponent<Collider2D>();
            }
        }

        return null;
    }
}