using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.UI.Score
{
    public class ScoreNotice : MonoBehaviour, IReusable
    {
        [SerializeField] private Graphic[] _graphicsToSetColor;
        [SerializeField] private Animator _animator;
        [SerializeField] private Text _label;
        [SerializeField] private Text _shadow;
        [SerializeField] private float _maxScale = 2f;
        [SerializeField] private float _maxScaleScore = 100f;

        private void Awake()
        {
            IsUsable = false;
            InvokeRepeating(nameof(CheckAvailability), 0f, 0.15f);
        }

        public bool IsUsable { get; private set; }

        private void CheckAvailability()
        {
            IsUsable = _animator.GetCurrentAnimatorStateInfo(0).IsTag("Available");
        }

        public void Show(int score, Color color)
        {
            foreach (var g in _graphicsToSetColor)
                g.color = color;

            _animator.SetTrigger("Score");
            _label.text = _shadow.text = score.ToString("N0");

            transform.localScale = Vector3.one;
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * _maxScale, score / _maxScaleScore);
        }
    }
}