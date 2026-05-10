#region

using System;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace BalloonParty.UI.Shields
{
    [RequireComponent(typeof(Text))]
    public class ShieldCounterLabel : MonoBehaviour
    {
        private Text _label;
        private IDisposable _subscription;

        private void Awake()
        {
            _label = GetComponent<Text>();
            _label.text = "--";
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
        }

        public void Bind(IReadOnlyReactiveProperty<int> shields)
        {
            _subscription?.Dispose();
            _subscription = shields.Subscribe(s => _label.text = s.ToString("N0"));
        }

        public void Unbind()
        {
            _subscription?.Dispose();
            _subscription = null;
            _label.text = "--";
        }
    }
}
