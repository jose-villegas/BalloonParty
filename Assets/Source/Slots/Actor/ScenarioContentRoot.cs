using System;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     Parent transform for scenario static content; a level transition moves this root, not each child.
    ///     <see cref="Outgoing" /> is a child holder for the old level's content a transition carries out (its
    ///     balloons, and snapshots of its scenery), kept apart from the incoming statics but still riding the root.
    ///     The root's transform is composed here from the transition's travel and the mist reveal's depth, so the
    ///     two never fight over it: write <see cref="SetTravel" /> and <see cref="SetDepth" />, never the
    ///     transform.
    /// </summary>
    internal sealed class ScenarioContentRoot : IDisposable
    {
        private float _travel;
        private float _incomingScale = 1f;
        private float _outgoingScale = 1f;

        public Transform Transform { get; }

        public Transform Outgoing { get; }

        /// <summary>How far the transition has carried the root up (negative = down), before any depth scale.</summary>
        public float Travel => _travel;

        internal ScenarioContentRoot()
        {
            Transform = new GameObject("ScenarioContentRoot").transform;
            // DontDestroyOnLoad throws outside play mode, where the EditMode tests build one.
            if (Application.isPlaying)
            {
                UnityEngine.Object.DontDestroyOnLoad(Transform.gameObject);
            }

            Outgoing = new GameObject("Outgoing").transform;
            Outgoing.SetParent(Transform, worldPositionStays: false);
        }

        public void Dispose()
        {
            if (Transform != null)
            {
                UnityEngine.Object.Destroy(Transform.gameObject);
            }
        }

        internal void SetTravel(float travel)
        {
            _travel = travel;
            Compose();
        }

        /// <summary>
        ///     Scales the incoming and the outgoing content about the camera (the world origin), each by its own
        ///     factor. Spawn nothing under the root while either is off 1: a view that places itself in world space
        ///     under a scaled root keeps the wrong local offset once the scale returns.
        /// </summary>
        internal void SetDepth(float incoming, float outgoing)
        {
            _incomingScale = Mathf.Max(1e-3f, incoming);
            _outgoingScale = Mathf.Max(1e-3f, outgoing);
            Compose();
        }

        // Scaling about the origin rather than the root's pivot: content at local offset p shows at
        // scale x (travel + p), so the root sits at scale x travel. The outgoing holder makes up the ratio
        // between the two scales, offset so its content scales about the origin too.
        private void Compose()
        {
            if (Transform == null)
            {
                return;
            }

            Transform.position = new Vector3(0f, _travel * _incomingScale, 0f);
            Transform.localScale = new Vector3(_incomingScale, _incomingScale, 1f);

            var relative = _outgoingScale / _incomingScale;
            Outgoing.localPosition = new Vector3(0f, _travel * (relative - 1f), 0f);
            Outgoing.localScale = new Vector3(relative, relative, 1f);
        }
    }
}
