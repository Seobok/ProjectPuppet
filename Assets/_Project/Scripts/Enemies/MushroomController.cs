using UnityEngine;

public enum MushroomActionState { Idle, Approach, AttackPrepare, AttackActive, AttackRecovery, Hurt, Parried, Dead }

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(EnemyMovement2D), typeof(MushroomCombat))]
public sealed class MushroomController : MonoBehaviour
{
    [Header("플레이어 감지 및 추적")]
    [SerializeField, Min(0.1f)] private float detectionDistance = 5f;
    [SerializeField, Min(0.1f)] private float loseTargetDistance = 7f;
    [SerializeField, Min(0f)] private float maxHeightDifference = 1f;
    [SerializeField] private PlayerController target;

    [Header("피격 및 패링당한 경직")]
    [Tooltip("초. 피해를 받으면 공격을 취소하고 이 시간 동안 경직됩니다.")]
    [SerializeField, Min(0.01f)] private float hurtDuration = 0.25f;
    [Tooltip("초. 플레이어가 돌진을 패링하면 이 시간 동안 빈틈을 노출합니다.")]
    [SerializeField, Min(0.01f)] private float parriedDuration = 0.65f;

    [Header("애니메이션 연결")]
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip hurtAnimation;
    [SerializeField] private AnimationClip stunAnimation;

    private EnemyHealth health;
    private EnemyMovement2D movement;
    private MushroomCombat combat;
    private PlayerController cachedTarget;
    private PlayerHealth targetHealth;
    private Collider2D targetCollider;
    private bool tracking;
    private float searchRemaining;
    private float staggerRemaining;
    public MushroomActionState State { get; private set; }
    public bool IsTracking => tracking;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement2D>();
        combat = GetComponent<MushroomCombat>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
        combat.CancelAttack();
        Stop();
    }

    private void FixedUpdate() => Tick(Time.fixedDeltaTime);

    public void Tick(float deltaTime)
    {
        if (health.IsDead) { State = MushroomActionState.Dead; Stop(); return; }
        if (staggerRemaining > 0f)
        {
            staggerRemaining = Mathf.Max(0f, staggerRemaining - deltaTime);
            Stop();
            if (staggerRemaining <= 0f)
            {
                State = MushroomActionState.Idle;
                if (animator != null) animator.SetBool("IsStaggered", false);
            }
            return;
        }
        if (combat.IsAttacking)
        {
            combat.Tick(deltaTime);
            if (health.IsDead || staggerRemaining > 0f) return;
            State = combat.Phase == EnemyAttackPhase.Prepare ? MushroomActionState.AttackPrepare :
                combat.Phase == EnemyAttackPhase.Active ? MushroomActionState.AttackActive :
                combat.Phase == EnemyAttackPhase.Recovery ? MushroomActionState.AttackRecovery : MushroomActionState.Idle;
            return;
        }
        searchRemaining -= deltaTime;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            tracking = false;
            if (searchRemaining <= 0f)
            {
                target = FindFirstObjectByType<PlayerController>();
                searchRemaining = 0.5f;
            }
        }
        if (target == null) { Idle(); return; }
        if (cachedTarget != target)
        {
            cachedTarget = target;
            targetHealth = target.GetComponent<PlayerHealth>();
            targetCollider = target.GetComponent<Collider2D>();
        }
        if (!target.gameObject.activeInHierarchy || (targetHealth != null && targetHealth.IsDead))
        {
            tracking = false; Idle(); return;
        }
        Vector2 offset = target.transform.position - transform.position;
        float feet = targetCollider != null ? targetCollider.bounds.min.y : target.transform.position.y;
        float distance = Mathf.Abs(offset.x);
        if (Mathf.Abs(feet - movement.FeetY) > maxHeightDifference ||
            distance > (tracking ? loseTargetDistance : detectionDistance))
        {
            tracking = false; Idle(); return;
        }
        tracking = true;
        movement.Face(offset.x);
        if (distance <= combat.AttackDistance)
        {
            Stop();
            State = combat.BeginAttack() ? MushroomActionState.AttackPrepare : MushroomActionState.Idle;
        }
        else
        {
            movement.Move(offset.x, distance - combat.AttackDistance, deltaTime);
            State = movement.IsMoving ? MushroomActionState.Approach : MushroomActionState.Idle;
            if (animator != null) animator.SetBool("IsMoving", movement.IsMoving);
        }
    }

    private void Idle() { State = MushroomActionState.Idle; Stop(); }
    private void Stop()
    {
        movement.Stop();
        if (animator != null) animator.SetBool("IsMoving", false);
    }

    private void OnDamaged() => Stagger(false);
    public void EnterParried() { if (!health.IsDead) Stagger(true); }
    private void Stagger(bool parried)
    {
        combat.CancelAttack();
        Stop();
        staggerRemaining = parried ? parriedDuration : hurtDuration;
        State = parried ? MushroomActionState.Parried : MushroomActionState.Hurt;
        if (animator == null) return;
        animator.ResetTrigger("Hurt");
        animator.ResetTrigger("Stun");
        animator.SetBool("IsStaggered", true);
        var clip = parried ? stunAnimation : hurtAnimation;
        animator.SetFloat("StaggerSpeed", clip != null ? clip.length / staggerRemaining : 1f);
        animator.SetTrigger(parried ? "Stun" : "Hurt");
    }

    private void OnDied()
    {
        combat.CancelAttack();
        Stop();
        State = MushroomActionState.Dead;
        if (animator == null) return;
        animator.ResetTrigger("Hurt"); animator.ResetTrigger("Stun");
        animator.SetBool("IsStaggered", false);
        animator.SetBool("IsDead", true);
    }

    private void OnValidate() => loseTargetDistance = Mathf.Max(detectionDistance, loseTargetDistance);
}
