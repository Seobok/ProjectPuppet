using UnityEngine;

public enum EnemyActionState { Idle, Approach, Guard, ParrySuccess, AttackPrepare, AttackActive, AttackRecovery, Hurt, Parried, Dead }

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(EnemyMovement2D), typeof(EnemyAnimation))]
[RequireComponent(typeof(EnemyCombat))]
[RequireComponent(typeof(EnemyGuard))]
public sealed class EnemyController : MonoBehaviour
{

    [Header("플레이어 감지 및 추적")]
    [Tooltip("월드 단위. 플레이어 추적을 시작하는 좌우 거리입니다. 높이 차이는 별도로 검사합니다.")]
    [SerializeField, Min(0.1f)] private float detectionDistance = 5f;
    [Tooltip("월드 단위. 추적 중인 플레이어를 놓치는 좌우 거리입니다. 감지 거리 이상으로 자동 보정됩니다.")]
    [SerializeField, Min(0.1f)] private float loseTargetDistance = 7f;
    [Tooltip("월드 단위. 적과 플레이어의 발 위치 높이 차이가 이 값보다 크면 추적을 중단합니다.")]
    [SerializeField, Min(0f)] private float maxHeightDifference = 1f;

    [Header("접근 및 거리 유지")]
    [Tooltip("월드 단위. 일반 접근을 멈추는 좌우 거리입니다. 공격 거리 안에서는 방어·공격이 우선합니다.")]
    [SerializeField, Min(0.1f)] private float stopDistance = 1f;
    [Tooltip("월드 단위. 멈춘 뒤 다시 접근하는 거리입니다. 방어 중에도 이 거리 밖이면 방어를 풀고 접근합니다. 멈춤 거리보다 크게 자동 보정됩니다.")]
    [SerializeField, Min(0.1f)] private float resumeDistance = 1.2f;

    [Header("방어 및 패링 성공 후 반격")]
    [Tooltip("초. 공격 전에 정면 방어 자세를 유지하는 시간입니다. 방어 중에는 방향을 고정합니다.")]
    [SerializeField, Min(0.05f)] private float guardDuration = 0.5f;
    [Tooltip("초. 플레이어 공격을 패링한 뒤 받아치기 동작을 유지하는 시간입니다. 적 공격의 반격 준비 시간과 합쳐 타격까지의 시간을 결정합니다.")]
    [SerializeField, Min(0.05f)] private float parrySuccessDuration = 0.10f;

    [Header("피격 및 패링당한 경직")]
    [Tooltip("초. 적이 피해를 받은 뒤 이동·공격·방향 전환이 제한되는 시간입니다.")]
    [SerializeField, Min(0f)] private float hurtDuration = 0.3f;
    [Tooltip("초. 적의 공격을 플레이어가 패링했을 때 생기는 경직 시간입니다. 적이 패링에 성공한 시간과는 다릅니다.")]
    [SerializeField, Min(0f)] private float parriedDuration = 0.6f;

    [Header("추적 대상 연결")]
    [Tooltip("추적할 플레이어입니다. 비워 두면 실행 시 활성 플레이어를 자동으로 찾습니다.")]
    [SerializeField] private PlayerController target;

    private EnemyHealth health;
    private EnemyMovement2D movement;
    private EnemyAnimation animationView;
    private EnemyCombat combat;
    private PlayerHealth targetHealth;
    private Collider2D targetCollider;
    private PlayerController cachedTarget;
    private bool tracking;
    private bool approaching;
    private float hurtRemaining;
    private float searchRemaining;
    private float guardRemaining;
    private float parryRemaining;
    private bool counterPending;
    private int counterDirection;
    public bool IsTracking => tracking;
    public bool IsHurt => hurtRemaining > 0f;
    public EnemyActionState State { get; private set; }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement2D>();
        animationView = GetComponent<EnemyAnimation>();
        combat = GetComponent<EnemyCombat>();
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
        ExitGuard();
        parryRemaining = 0f;
        counterPending = false;
        State = EnemyActionState.Idle;
        Stop();
    }

    private void FixedUpdate() => Tick(Time.fixedDeltaTime);

    public void Tick(float deltaTime)
    {
        if (health.IsDead) { State = EnemyActionState.Dead; combat.CancelAttack(); Stop(); return; }
        if (hurtRemaining > 0f)
        {
            hurtRemaining = Mathf.Max(0f, hurtRemaining - deltaTime);
            Stop(); return;
        }
        if (parryRemaining > 0f)
        {
            parryRemaining = Mathf.Max(0f, parryRemaining - deltaTime);
            State = EnemyActionState.ParrySuccess;
            Stop();
            if (parryRemaining <= 0f)
            {
                bool startCounter = counterPending;
                counterPending = false;
                State = startCounter && combat.BeginCounterAttack(counterDirection)
                    ? EnemyActionState.AttackPrepare : EnemyActionState.Idle;
            }
            return;
        }
        if (combat.IsAttacking)
        {
            Stop(); combat.Tick(deltaTime);
            if (hurtRemaining > 0f) return;
            State = combat.Phase == EnemyAttackPhase.Prepare ? EnemyActionState.AttackPrepare :
                combat.Phase == EnemyAttackPhase.Active ? EnemyActionState.AttackActive :
                combat.Phase == EnemyAttackPhase.Recovery ? EnemyActionState.AttackRecovery : EnemyActionState.Idle;
            return;
        }
        bool wasGuarding = State == EnemyActionState.Guard;
        State = EnemyActionState.Idle;
        searchRemaining -= deltaTime;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            tracking = approaching = false;
            if (searchRemaining <= 0f)
            {
                target = FindFirstObjectByType<PlayerController>();
                searchRemaining = 0.5f;
            }
        }
        if (target == null) { ExitGuard(); Stop(); return; }
        if (cachedTarget != target)
        {
            cachedTarget = target;
            targetHealth = target.GetComponent<PlayerHealth>();
            targetCollider = target.GetComponent<Collider2D>();
        }
        if (!target.gameObject.activeInHierarchy || (targetHealth != null && targetHealth.IsDead))
        {
            tracking = approaching = false; ExitGuard(); Stop(); return;
        }
        Vector2 offset = target.transform.position - transform.position;
        float height = targetCollider != null ? targetCollider.bounds.min.y : target.transform.position.y;
        float horizontalDistance = Mathf.Abs(offset.x);
        if (Mathf.Abs(height - movement.FeetY) > maxHeightDifference ||
            horizontalDistance > (tracking ? loseTargetDistance : detectionDistance))
        {
            tracking = approaching = false; ExitGuard(); Stop(); return;
        }
        if (!tracking) { tracking = true; approaching = horizontalDistance > stopDistance; }
        if (wasGuarding)
        {
            if (horizontalDistance <= resumeDistance && combat.isActiveAndEnabled)
            {
                guardRemaining -= deltaTime;
                if (guardRemaining > 0f)
                {
                    State = EnemyActionState.Guard; Stop(); return;
                }
                ExitGuard();
                movement.Face(offset.x);
                if (horizontalDistance <= combat.AttackDistance && combat.BeginAttack())
                {
                    State = EnemyActionState.AttackPrepare; Stop(); return;
                }
            }
            else ExitGuard();
        }
        movement.Face(offset.x);
        if (!wasGuarding && combat.isActiveAndEnabled && horizontalDistance <= combat.AttackDistance && movement.IsGrounded)
        {
            guardRemaining = guardDuration;
            State = EnemyActionState.Guard;
            animationView.SetGuarding(true);
            Stop(); return;
        }
        if (horizontalDistance <= stopDistance) approaching = false;
        else if (horizontalDistance >= resumeDistance) approaching = true;
        if (approaching) movement.Move(offset.x, horizontalDistance - stopDistance, deltaTime);
        else movement.Stop();
        animationView.SetMoving(movement.IsMoving);
        State = movement.IsMoving ? EnemyActionState.Approach : EnemyActionState.Idle;
    }

    private void OnDamaged()
    {
        ExitGuard(); parryRemaining = 0f; counterPending = false;
        hurtRemaining = hurtDuration;
        State = EnemyActionState.Hurt;
        combat.CancelAttack();
        Stop();
    }

    private void OnDied()
    {
        ExitGuard(); parryRemaining = 0f; counterPending = false;
        State = EnemyActionState.Dead;
        combat.CancelAttack();
        Stop();
    }

    public void EnterParried()
    {
        if (health.IsDead) return;
        ExitGuard(); parryRemaining = 0f; counterPending = false;
        combat.CancelAttack();
        hurtRemaining = parriedDuration;
        State = EnemyActionState.Parried;
        animationView.PlayStagger();
        Stop();
    }

    public void OnParrySucceeded()
    {
        if (State != EnemyActionState.Guard || health.IsDead) return;
        ExitGuard();
        State = EnemyActionState.ParrySuccess;
        parryRemaining = parrySuccessDuration;
        counterDirection = movement.FacingDirection;
        counterPending = true;
        combat.CancelAttack();
        Stop();
        animationView.PlayParry(parrySuccessDuration);
    }

    private void ExitGuard()
    {
        guardRemaining = 0f;
        animationView.SetGuarding(false);
    }

    private void Stop()
    {
        movement.Stop();
        animationView.SetMoving(false);
    }

    private void OnValidate()
    {
        loseTargetDistance = Mathf.Max(loseTargetDistance, detectionDistance);
        resumeDistance = Mathf.Max(resumeDistance, stopDistance + 0.05f);
    }
}
