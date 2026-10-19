using System.Collections.Generic;
using UnityEngine;

public enum EnemyAttackPhase { None, Prepare, Active, Recovery }

[DisallowMultipleComponent]
[DefaultExecutionOrder(-25)]
[RequireComponent(typeof(EnemyHealth), typeof(EnemyMovement2D), typeof(EnemyAnimation))]
public sealed class EnemyCombat : MonoBehaviour
{

    [Header("기본 공격 시작 및 피해")]
    [Tooltip("월드 단위. 이 좌우 거리 안에서 방어 후 공격을 시작합니다. 실제로 닿는 범위는 공격 판정 크기와 중심으로 정합니다.")]
    [SerializeField, Min(0.1f)] private float attackDistance = 1.1f;
    [Tooltip("기본 공격과 반격이 한 번 명중했을 때 주는 피해량입니다.")]
    [SerializeField, Min(1)] private int damage = 10;

    [Header("일반 공격 준비")]
    [Tooltip("초. 일반 공격의 준비 동작 시간입니다. 반격 준비 시간과 별도로 적용됩니다.")]
    [SerializeField, Min(0.05f)] private float prepareDuration = 0.35f;

    [Header("반격 준비")]
    [Tooltip("초. 패링 성공 동작이 끝난 뒤 반격 타격까지의 준비 시간입니다. EnemyController의 패링 성공 동작 시간과 합산됩니다.")]
    [SerializeField, Min(0.05f)] private float counterPrepareDuration = 0.20f;

    [Header("일반 공격과 반격 공통")]
    [Tooltip("초. 공격 판정을 켜는 타격 구간입니다. 애니메이션 구간 재생 속도에 반영됩니다.")]
    [SerializeField, Min(0.05f)] private float activeDuration = 0.1f;
    [Tooltip("초. 타격 후 이동·방어·방향 전환이 제한되는 빈틈입니다. 일반 공격과 반격이 공유합니다.")]
    [SerializeField, Min(0.05f)] private float recoveryDuration = 0.5f;

    [Header("공격 판정 범위")]
    [Tooltip("월드 단위. 적 루트에서 공격 중심까지의 거리입니다. X는 바라보는 방향에 맞춰 반전되고 Y는 높이입니다.")]
    [SerializeField] private Vector2 hitboxOffset = new Vector2(0.72f, 0.5f);
    [Tooltip("월드 단위. 공격 판정 사각형의 가로(X)·세로(Y) 크기입니다. 선택하면 기즈모로 표시됩니다.")]
    [SerializeField] private Vector2 hitboxSize = new Vector2(1.05f, 0.85f);

    [Header("대상 및 장애물 레이어")]
    [Tooltip("공격 판정으로 검색할 레이어입니다. 실제 피해는 플레이어의 공격 수신 컴포넌트에만 전달합니다.")]
    [SerializeField] private LayerMask targetLayer = ~0;
    [Tooltip("공격자와 대상 사이를 가로막아 타격을 차단하는 지형 레이어입니다.")]
    [SerializeField] private LayerMask obstacleLayer = 1 << 8;

    private EnemyHealth health;
    private EnemyMovement2D movement;
    private EnemyAnimation animationView;
    private readonly HashSet<IAttackReceiver> hitTargets = new HashSet<IAttackReceiver>();
    private float remaining;
    private int attackDirection;
    public float AttackDistance => attackDistance;
    public EnemyAttackPhase Phase { get; private set; }
    public bool IsAttacking => Phase != EnemyAttackPhase.None;
    public bool IsCounterAttack { get; private set; }
    public Vector2 HitboxCenter => (Vector2)transform.position + new Vector2(hitboxOffset.x * (IsAttacking ? attackDirection : movement != null ? movement.FacingDirection : 1), hitboxOffset.y);

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement2D>();
        animationView = GetComponent<EnemyAnimation>();
    }

    private void OnEnable()
    {
        health.Damaged += CancelAttack;
        health.Died += CancelAttack;
    }

    private void OnDisable()
    {
        health.Damaged -= CancelAttack;
        health.Died -= CancelAttack;
        CancelAttack();
    }

    public bool BeginAttack()
    {
        return BeginAttackInternal(prepareDuration, false, movement.FacingDirection);
    }

    public bool BeginCounterAttack(int direction)
    {
        return BeginAttackInternal(counterPrepareDuration, true, direction);
    }

    private bool BeginAttackInternal(float preparation, bool counter, int direction)
    {
        if (!isActiveAndEnabled || health.IsDead || IsAttacking || !movement.IsGrounded) return false;
        movement.Stop();
        movement.Face(direction);
        attackDirection = movement.FacingDirection;
        IsCounterAttack = counter;
        hitTargets.Clear();
        Phase = EnemyAttackPhase.Prepare;
        // Animation events control the hit window. This only recovers from missing events.
        remaining = preparation + activeDuration + recoveryDuration + 0.5f;
        animationView.BeginAttack(preparation, activeDuration, recoveryDuration);
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (!IsAttacking) return;
        if (health.IsDead) { CancelAttack(); return; }
        remaining -= deltaTime;
        if (remaining <= 0f) { CancelAttack(); return; }
        if (Phase == EnemyAttackPhase.Active) TickHitbox();
    }

    public void BeginHitbox()
    {
        if (!isActiveAndEnabled || health.IsDead || Phase != EnemyAttackPhase.Prepare) return;
        Phase = EnemyAttackPhase.Active;
        TickHitbox();
    }

    public void EndHitbox()
    {
        if (Phase == EnemyAttackPhase.Active) Phase = EnemyAttackPhase.Recovery;
    }

    public void EndAttack()
    {
        if (Phase == EnemyAttackPhase.Recovery) CancelAttack();
    }

    private void TickHitbox()
    {
        foreach (var hit in Physics2D.OverlapBoxAll(HitboxCenter, hitboxSize, 0f, targetLayer))
        {
            if (hit.transform.IsChildOf(transform) || hit.GetComponentInParent<PlayerController>() == null) continue;
            var receiver = hit.GetComponentInParent<IAttackReceiver>();
            if (receiver == null || hitTargets.Contains(receiver)) continue;
            Vector2 origin = (Vector2)transform.position + Vector2.up * hitboxOffset.y;
            var obstruction = Physics2D.Linecast(origin, hit.bounds.center, obstacleLayer);
            if (obstruction.collider != null && !obstruction.collider.isTrigger) continue;
            hitTargets.Add(receiver);
            var result = receiver.ReceiveAttack(new AttackInfo(gameObject, damage, Vector2.right * attackDirection));
            if (result == AttackResult.Parried)
            {
                CancelAttack();
                GetComponent<EnemyController>()?.EnterParried();
                break;
            }
            if (!IsAttacking || health.IsDead) break;
        }
    }

    public void CancelAttack()
    {
        Phase = EnemyAttackPhase.None;
        IsCounterAttack = false;
        remaining = 0f;
        if (animationView != null) animationView.CancelAttack();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Phase == EnemyAttackPhase.Active ? Color.red : Color.yellow;
        Gizmos.DrawWireCube(HitboxCenter, hitboxSize);
    }
}
