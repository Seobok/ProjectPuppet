using UnityEngine;

public enum BossActionState { Idle, Approach, Guard, ParrySuccess, AttackPrepare, AttackActive, AttackRecovery, Hurt, Parried, Dead }

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(EnemyMovement2D), typeof(BossAnimation))]
[RequireComponent(typeof(BossCombat))]
public sealed class BossController : MonoBehaviour
{
    [Header("감지 및 공격 선택 거리")]
    [SerializeField, Min(.1f)] private float detectionDistance = 8f;
    [SerializeField, Min(.1f)] private float loseTargetDistance = 11f;
    [SerializeField, Min(0f)] private float maxHeightDifference = 1.5f;
    [SerializeField, Min(.1f)] private float comboDistance = 1.55f;
    [SerializeField, Min(.1f)] private float heavyDistance = 3.2f;
    [SerializeField, Min(.1f)] private float guardDistance = 2f;
    [SerializeField, Min(1)] private int maxPatternRepeats = 2;
    [Header("방어 및 패링 후 반격")]
    [Tooltip("초. 패턴 사이에 정면 방어 자세를 유지하는 시간입니다. 뒤쪽 공격은 명중합니다.")]
    [SerializeField, Min(.05f)] private float guardDuration = .6f;
    [Tooltip("초. 이 시간이 끝나면 반격 준비 동작을 시작합니다.")]
    [SerializeField, Min(.01f)] private float parrySuccessDuration = .1f;
    [Header("피격 및 패링당한 빈틈")]
    [Tooltip("초. 방어·대기 중 피격 경직. 패턴 진행 중에는 피해만 받아 연격과 회복 빈틈의 길이를 유지합니다.")]
    [SerializeField, Min(.01f)] private float hurtDuration = .18f;
    [SerializeField, Min(.01f)] private float parriedDuration = .7f;
    [Tooltip("초. 강공격 패링 시 경직. 강공격의 회복 시간보다 짧아 회피 후 뒤잡기의 이득을 남깁니다.")]
    [SerializeField, Min(.01f)] private float heavyParriedDuration = .4f;
    [Header("추적 대상 (비우면 자동 검색)")]
    [SerializeField] private PlayerController target;

    private EnemyHealth health;
    private EnemyMovement2D movement;
    private BossCombat combat;
    private BossAnimation view;
    private PlayerController cachedTarget;
    private PlayerHealth targetHealth;
    private Collider2D targetCollider;
    private float staggerRemaining;
    private float guardRemaining;
    private float parryRemaining;
    private float searchRemaining;
    private bool tracking;
    private bool needsGuard = true;
    private BossPattern lastPattern;
    private int repeatCount;
    public BossActionState State { get; private set; }
    public bool IsTracking => tracking;
    public bool CanBeginAttack => isActiveAndEnabled && !health.IsDead && staggerRemaining <= 0f && parryRemaining <= 0f && State != BossActionState.Guard;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>(); movement = GetComponent<EnemyMovement2D>();
        combat = GetComponent<BossCombat>(); view = GetComponent<BossAnimation>();
    }
    private void OnEnable() { health.Damaged += OnDamaged; health.Died += OnDied; }
    private void OnDisable()
    {
        health.Damaged -= OnDamaged; health.Died -= OnDied;
        combat.CancelAttack(); ExitGuard(); Stop();
        parryRemaining = 0f; staggerRemaining = 0f; view.EndStagger();
        tracking = false; needsGuard = true; State = health.IsDead ? BossActionState.Dead : BossActionState.Idle;
    }
    private void FixedUpdate() => Tick(Time.fixedDeltaTime);
    public void Tick(float deltaTime)
    {
        if (health.IsDead) { State = BossActionState.Dead; Stop(); return; }
        if (staggerRemaining > 0f)
        {
            staggerRemaining = Mathf.Max(0f, staggerRemaining - deltaTime); Stop();
            if (staggerRemaining <= 0f) { view.EndStagger(); State = BossActionState.Idle; }
            return;
        }
        if (parryRemaining > 0f)
        {
            parryRemaining = Mathf.Max(0f, parryRemaining - deltaTime); Stop();
            if (parryRemaining <= 0f)
            {
                State = BossActionState.Idle;
                if (combat.BeginPattern(BossPattern.Counter)) State = BossActionState.AttackPrepare;
            }
            return;
        }
        if (combat.IsAttacking)
        {
            combat.Tick(deltaTime);
            if (health.IsDead || staggerRemaining > 0f) return;
            State = combat.Phase == EnemyAttackPhase.Prepare ? BossActionState.AttackPrepare :
                combat.Phase == EnemyAttackPhase.Active ? BossActionState.AttackActive :
                combat.Phase == EnemyAttackPhase.Recovery ? BossActionState.AttackRecovery : BossActionState.Idle;
            if (!combat.IsAttacking) needsGuard = true;
            return;
        }
        // EndStep is an animation event, so an attack can finish between physics ticks.
        if (State == BossActionState.AttackPrepare || State == BossActionState.AttackActive || State == BossActionState.AttackRecovery) needsGuard = true;
        searchRemaining -= deltaTime;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            tracking = false;
            if (searchRemaining <= 0f) { target = FindFirstObjectByType<PlayerController>(); searchRemaining = .5f; }
        }
        if (target == null) { Idle(); return; }
        if (cachedTarget != target)
        {
            cachedTarget = target; targetHealth = target.GetComponent<PlayerHealth>(); targetCollider = target.GetComponent<Collider2D>();
        }
        if (!target.gameObject.activeInHierarchy || (targetHealth != null && targetHealth.IsDead)) { tracking = false; Idle(); return; }
        Vector2 offset = target.transform.position - transform.position;
        float feet = targetCollider != null ? targetCollider.bounds.min.y : target.transform.position.y;
        float distance = Mathf.Abs(offset.x);
        if (Mathf.Abs(feet - movement.FeetY) > maxHeightDifference || distance > (tracking ? loseTargetDistance : detectionDistance))
        { tracking = false; Idle(); return; }
        tracking = true;
        if (State == BossActionState.Guard)
        {
            guardRemaining -= deltaTime;
            if (guardRemaining > 0f && distance <= guardDistance) { Stop(); return; }
            ExitGuard(); State = BossActionState.Idle;
        }
        movement.Face(offset.x);
        if (!movement.IsGrounded) { State = BossActionState.Idle; Stop(); return; }
        if (needsGuard && distance <= guardDistance)
        {
            needsGuard = false; guardRemaining = guardDuration;
            State = BossActionState.Guard; Stop(); view.SetGuarding(true); return;
        }
        var pattern = distance <= comboDistance ? BossPattern.Combo : BossPattern.Heavy;
        if (pattern == lastPattern && repeatCount >= maxPatternRepeats) pattern = pattern == BossPattern.Combo ? BossPattern.Heavy : BossPattern.Combo;
        float range = pattern == BossPattern.Combo ? comboDistance : heavyDistance;
        if (distance > range)
        {
            movement.Move(offset.x, distance - range, deltaTime);
            view.SetMoving(movement.IsMoving); State = movement.IsMoving ? BossActionState.Approach : BossActionState.Idle; return;
        }
        Stop();
        if (combat.BeginPattern(pattern))
        {
            repeatCount = pattern == lastPattern ? repeatCount + 1 : 1; lastPattern = pattern;
            State = BossActionState.AttackPrepare;
        }
    }
    public void OnParrySucceeded()
    {
        if (State != BossActionState.Guard || health.IsDead) return;
        ExitGuard(); Stop(); State = BossActionState.ParrySuccess;
        parryRemaining = parrySuccessDuration; view.PlayParry(parrySuccessDuration);
    }
    public void EnterParried(BossPattern pattern)
    {
        if (health.IsDead) return;
        Stagger(pattern == BossPattern.Heavy ? heavyParriedDuration : parriedDuration, BossActionState.Parried);
    }
    private void OnDamaged()
    {
        view.FlashHit();
        // Keep both the remaining combo and the full recovery opening when ordinary hits land.
        if (State == BossActionState.Parried || combat.IsAttacking) return;
        Stagger(hurtDuration, BossActionState.Hurt);
    }
    private void Stagger(float duration, BossActionState state)
    {
        combat.CancelAttack(); ExitGuard(); Stop(); parryRemaining = 0f;
        staggerRemaining = duration; needsGuard = true; State = state; view.PlayStagger(duration);
    }
    private void OnDied()
    {
        combat.CancelAttack(); ExitGuard(); Stop(); staggerRemaining = parryRemaining = 0f;
        State = BossActionState.Dead; view.PlayDeath();
    }
    private void ExitGuard() { guardRemaining = 0f; view.SetGuarding(false); }
    private void Stop() { movement.Stop(); view.SetMoving(false); }
    private void Idle() { ExitGuard(); Stop(); State = BossActionState.Idle; }
    private void OnValidate()
    {
        loseTargetDistance = Mathf.Max(detectionDistance, loseTargetDistance);
        heavyDistance = Mathf.Max(comboDistance, heavyDistance);
    }
}
