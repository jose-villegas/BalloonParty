using UnityEngine;
using UnityEngine.UI;

public class ScoreNotice : MonoBehaviour
{
    [SerializeField] private Graphic[] _graphicsToSetColor;
    [SerializeField] private Animator _animator;
    [SerializeField] private Text _label;
    [SerializeField] private Text _shadow;
    [SerializeField] private float _maxScale;
    [SerializeField] private float _maxScaleScore;

    public bool IsUsable { get; private set; }

    public Animator Animator => _animator;

    public void Awake()
    {
        IsUsable = false;

        InvokeRepeating("CheckAvailability", 0, .15f);
    }

    private void CheckAvailability()
    {
        IsUsable = Animator.GetCurrentAnimatorStateInfo(0).IsTag("Available");
    }

    public void ScoreUp(int score)
    {
        Animator.SetTrigger("Score");

        _label.text = _shadow.text = score.ToString("N0");

        transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * _maxScale, score / _maxScaleScore);
    }
}