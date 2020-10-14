using UnityEngine;
using UnityEngine.UI;

public class ScoreNotice : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private Text[] _labels;

    public bool IsUsable { get; private set; }

    public Animator Animator => _animator;

    public void Awake()
    {
        IsUsable = false;

        InvokeRepeating("CheckAvailability", 0, .15f);
    }

    private void CheckAvailability()
    {
        IsUsable = !(Animator.GetCurrentAnimatorStateInfo(0).length >
                     Animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
    }
}