using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(EnemyMovement2D))]
public sealed class MushroomCombat : MonoBehaviour
{
    [Header("돌진 공격")]
    [SerializeField, Min(0.1f)] private float attackDistance = 1.65f;
    [SerializeField, Min(1)] private int damage = 10;
    [Tooltip("초. 이 시간 동안 방향을 고정하고 공격을 예고합니다.")]
    [SerializeField, Min(0.05f)] private float prepareDuration = 0.45f;
    [SerializeField, Min(0.05f)] private float activeDuration = 0.22f;
    [Tooltip("초. 공격 후 방향 전환과 행동을 제한하는 빈틈입니다.")]
    [SerializeField, Min(0.05f)] private float recoveryDuration = 0.65f;
    [Tooltip("월드 단위. 타격 구간 중 전진할 최대 거리입니다. 벽과 낭떠러지 앞에서는 멈춥니다.")]
    [SerializeField, Min(0f)] private float lungeDistance = 1.15f;

    [Header("공격 판정")]
    [SerializeField] private Vector2 hitboxOffset = new Vector2(0.45f, 0.45f);
    [SerializeField] private Vector2 hitboxSize = new Vector2(0.8f, 0.65f);
    [SerializeField] private LayerMask targetLayer = ~0;
    [SerializeField] private LayerMask obstacleLayer = 1 << 8;

    [Header("애니메이션 연결")]
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip attackPrepare;
    [SerializeField] private AnimationClip attackStrike;
    [SerializeField] private AnimationClip attackRecovery;
    private EnemyHealth health;
    private EnemyMovement2D movement;
    private readonly HashSet<IAttackReceiver> hitTargets = new HashSet<IAttackReceiver>();
    private float remaining;
    private float activeElapsed;
    private int direction;
    public float AttackDistance => attackDistance;
    public EnemyAttackPhase Phase { get; private set; }
    public bool IsAttacking => Phase != EnemyAttackPhase.None;
    public Vector2 HitboxCenter => (Vector2)transform.position + new Vector2(hitboxOffset.x * (IsAttacking ? direction : movement != null ? movement.FacingDirection : 1), hitboxOffset.y);

    private void Awake()
    {
        health = GetComponent<EnemyHealth>(); movement = GetComponent<EnemyMovement2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }
    private void OnEnable() { health.Damaged += CancelAttack; health.Died += CancelAttack; }
    private void OnDisable() { health.Damaged -= CancelAttack; health.Died -= CancelAttack; CancelAttack(); }

    public bool BeginAttack()
    {
        if (!isActiveAndEnabled || health.IsDead || IsAttacking || !movement.IsGrounded) return false;
        direction = movement.FacingDirection;
        movement.Stop(); hitTargets.Clear(); activeElapsed = 0f;
        Phase = EnemyAttackPhase.Prepare;
        remaining = prepareDuration + activeDuration + recoveryDuration + 0.5f;
        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            animator.SetFloat("PrepareSpeed", attackPrepare.length / prepareDuration);
            animator.SetFloat("StrikeSpeed", attackStrike.length / activeDuration);
            animator.SetFloat("RecoverySpeed", attackRecovery.length / recoveryDuration);
            animator.SetBool("IsAttacking", true);
            animator.ResetTrigger("Attack"); animator.SetTrigger("Attack");
        }
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (!IsAttacking) return;
        remaining -= deltaTime;
        if (health.IsDead || remaining <= 0f) { CancelAttack(); return; }
        if (Phase != EnemyAttackPhase.Active) { movement.Stop(); return; }
        float step = Mathf.Min(deltaTime, Mathf.Max(0f, activeDuration - activeElapsed));
        activeElapsed += deltaTime;
        if (step > 0f) movement.MoveAtSpeed(direction, lungeDistance / activeDuration * step / Mathf.Max(deltaTime, 0.0001f), deltaTime);
        else movement.Stop();
        TickHitbox();
    }

    public void BeginHitbox()
    {
        if (!isActiveAndEnabled || health.IsDead || Phase != EnemyAttackPhase.Prepare) return;
        Phase = EnemyAttackPhase.Active;
        TickHitbox();
    }
    public void EndHitbox()
    {
        if (Phase != EnemyAttackPhase.Active) return;
        Phase = EnemyAttackPhase.Recovery;
        movement.Stop();
    }
    public void EndAttack() { if (Phase == EnemyAttackPhase.Recovery) CancelAttack(); }

    private void TickHitbox()
    {
        foreach (var hit in Physics2D.OverlapBoxAll(HitboxCenter, hitboxSize, 0f, targetLayer))
        {
            if (hit.transform.IsChildOf(transform) || hit.GetComponentInParent<PlayerController>() == null) continue;
            var receiver = hit.GetComponentInParent<IAttackReceiver>();
            if (receiver == null || hitTargets.Contains(receiver)) continue;
            var block = Physics2D.Linecast((Vector2)transform.position + Vector2.up * hitboxOffset.y, hit.bounds.center, obstacleLayer);
            if (block.collider != null && !block.collider.isTrigger) continue;
            hitTargets.Add(receiver);
            var result = receiver.ReceiveAttack(new AttackInfo(gameObject, damage, Vector2.right * direction));
            if (result == AttackResult.Parried)
            {
                CancelAttack(); GetComponent<MushroomController>()?.EnterParried(); break;
            }
            if (!IsAttacking || health.IsDead) break;
        }
    }

    public void CancelAttack()
    {
        Phase = EnemyAttackPhase.None; remaining = 0f;
        if (movement != null) movement.Stop();
        if (animator == null) return;
        animator.ResetTrigger("Attack"); animator.SetBool("IsAttacking", false);
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Phase == EnemyAttackPhase.Active ? Color.red : Color.yellow;
        Gizmos.DrawWireCube(HitboxCenter, hitboxSize);
    }
}
