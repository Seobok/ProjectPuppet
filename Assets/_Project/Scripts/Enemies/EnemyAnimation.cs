using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyAnimation : MonoBehaviour
{
    private static readonly int Hurt = Animator.StringToHash("Hurt");
    private static readonly int IsDead = Animator.StringToHash("IsDead");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int IsAttacking = Animator.StringToHash("IsAttacking");
    private static readonly int IsGuarding = Animator.StringToHash("IsGuarding");
    private static readonly int Parry = Animator.StringToHash("Parry");

    [Header("패링 성공 동작 연결")]
    [Tooltip("패링 성공 동작입니다. 재생 시간은 EnemyController의 패링 성공 동작 시간으로 조절합니다.")]
    [SerializeField] private AnimationClip parryAnimation;

    [Header("공격 구간 연결")]
    [Tooltip("공격 준비 구간 애니메이션입니다. 준비 시간은 EnemyCombat에서 조절합니다.")]
    [SerializeField] private AnimationClip attackPrepare;
    [Tooltip("타격 구간 애니메이션입니다. 공격 판정 시작·종료 이벤트가 필요합니다.")]
    [SerializeField] private AnimationClip attackStrike;
    [Tooltip("공격 회복 구간 애니메이션입니다. 종료 이벤트가 필요합니다.")]
    [SerializeField] private AnimationClip attackRecovery;

    [Header("애니메이터 연결")]
    [Tooltip("적 애니메이터입니다. 전투 시간 수치 조정은 각 행동 설정에서 진행합니다.")]
    [SerializeField] private Animator animator;
    private EnemyHealth health;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        health.Damaged += PlayHurt;
        health.Died += PlayDeath;
        if (animator != null)
        {
            animator.ResetTrigger(Hurt);
            CancelAttack();
            ClearGuard();
            animator.SetBool(IsMoving, false);
            animator.SetBool(IsDead, health.IsDead);
        }
    }

    private void OnDisable()
    {
        health.Damaged -= PlayHurt;
        health.Died -= PlayDeath;
    }

    private void PlayHurt()
    {
        if (animator == null || health.IsDead) return;
        CancelAttack();
        ClearGuard();
        animator.ResetTrigger(Hurt);
        animator.SetBool(IsMoving, false);
        animator.SetTrigger(Hurt);
    }

    private void PlayDeath()
    {
        if (animator == null) return;
        CancelAttack();
        ClearGuard();
        animator.ResetTrigger(Hurt);
        animator.SetBool(IsMoving, false);
        animator.SetBool(IsDead, true);
    }

    public void SetMoving(bool moving)
    {
        if (animator != null) animator.SetBool(IsMoving, moving && !health.IsDead);
    }

    public void BeginAttack(float prepare, float strike, float recovery)
    {
        if (animator == null) return;
        ClearGuard();
        SetMoving(false);
        animator.SetFloat("PrepareSpeed", attackPrepare.length / prepare);
        animator.SetFloat("StrikeSpeed", attackStrike.length / strike);
        animator.SetFloat("RecoverySpeed", attackRecovery.length / recovery);
        animator.SetBool(IsAttacking, true);
        animator.ResetTrigger(Attack);
        animator.SetTrigger(Attack);
    }

    public void CancelAttack()
    {
        if (animator == null) return;
        animator.ResetTrigger(Attack);
        animator.SetBool(IsAttacking, false);
    }

    public void PlayStagger() => PlayHurt();

    public void SetGuarding(bool guarding)
    {
        if (animator != null) animator.SetBool(IsGuarding, guarding && !health.IsDead);
    }

    public void PlayParry(float duration)
    {
        if (animator == null || health.IsDead) return;
        animator.SetBool(IsGuarding, false);
        animator.SetFloat("ParrySpeed", parryAnimation.length / duration);
        animator.ResetTrigger(Parry);
        animator.SetTrigger(Parry);
    }

    private void ClearGuard()
    {
        if (animator == null) return;
        animator.SetBool(IsGuarding, false);
        animator.ResetTrigger(Parry);
    }
}
