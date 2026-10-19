using System;
using System.Collections.Generic;
using UnityEngine;

public enum BossPattern { None, Combo, Heavy, Counter }

[Serializable]
public sealed class BossAttackStep
{
    [Header("공격 시간 (초)")]
    [Min(.05f)] public float prepareDuration = .4f;
    [Min(.05f)] public float activeDuration = .15f;
    [Min(.05f)] public float recoveryDuration = .7f;
    [Header("피해 및 이동")]
    [Min(1)] public int damage = 12;
    [Tooltip("타격 구간 중 전진할 최대 거리입니다. 벽과 낭떠러지 앞에서는 멈춥니다.")]
    [Min(0f)] public float lungeDistance;
    [Header("판정 범위 (월드 단위)")]
    public Vector2 hitboxOffset = new Vector2(.8f, .65f);
    public Vector2 hitboxSize = new Vector2(1.45f, 1.1f);
    [Header("애니메이션 연결")]
    public int animationId;
    public AnimationClip prepareAnimation;
    public AnimationClip strikeAnimation;
    public AnimationClip recoveryAnimation;
}

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(EnemyMovement2D), typeof(BossAnimation))]
public sealed class BossCombat : MonoBehaviour
{
    [Header("2연격 (첫 타격 → 마지막 타격)")]
    [SerializeField] private BossAttackStep[] combo = { new BossAttackStep(), new BossAttackStep() };
    [Header("전진 강공격")]
    [SerializeField] private BossAttackStep heavy = new BossAttackStep();
    [Header("패링 성공 후 반격")]
    [SerializeField] private BossAttackStep counter = new BossAttackStep();
    [Header("공격 레이어")]
    [SerializeField] private LayerMask targetLayer = ~0;
    [SerializeField] private LayerMask obstacleLayer = 1 << 8;

    private EnemyHealth health;
    private EnemyMovement2D movement;
    private BossAnimation view;
    private BossController controller;
    private BossAttackStep step;
    private int direction;
    private float watchdog;
    private float activeElapsed;
    private readonly HashSet<IAttackReceiver> hitTargets = new HashSet<IAttackReceiver>();
    public BossPattern Pattern { get; private set; }
    public EnemyAttackPhase Phase { get; private set; }
    public int StepIndex { get; private set; }
    public bool IsAttacking => Phase != EnemyAttackPhase.None;
    public Vector2 HitboxCenter => (Vector2)transform.position + (step != null ? new Vector2(step.hitboxOffset.x * direction, step.hitboxOffset.y) : Vector2.zero);

    private void Awake()
    {
        health = GetComponent<EnemyHealth>(); movement = GetComponent<EnemyMovement2D>();
        view = GetComponent<BossAnimation>(); controller = GetComponent<BossController>();
    }
    private void OnEnable() => health.Died += CancelAttack;
    private void OnDisable() { health.Died -= CancelAttack; CancelAttack(); }

    public bool BeginPattern(BossPattern pattern)
    {
        if (!isActiveAndEnabled || health.IsDead || IsAttacking || !movement.IsGrounded ||
            pattern == BossPattern.None || (controller != null && !controller.CanBeginAttack)) return false;
        var first = pattern == BossPattern.Combo ? (combo != null && combo.Length > 0 ? combo[0] : null) : pattern == BossPattern.Heavy ? heavy : counter;
        if (!Valid(first) || (pattern == BossPattern.Combo && (combo.Length != 2 || !Valid(combo[1])))) return false;
        Pattern = pattern; StepIndex = 0; direction = movement.FacingDirection;
        BeginStep(first);
        return true;
    }
    private static bool Valid(BossAttackStep value) => value != null && value.prepareAnimation != null && value.strikeAnimation != null && value.recoveryAnimation != null;
    private void BeginStep(BossAttackStep next)
    {
        step = next; activeElapsed = 0f; hitTargets.Clear(); movement.Stop();
        Phase = EnemyAttackPhase.Prepare;
        watchdog = step.prepareDuration + step.activeDuration + step.recoveryDuration + .5f;
        view.BeginStep(step);
    }
    public void Tick(float deltaTime)
    {
        if (!IsAttacking) return;
        watchdog -= deltaTime;
        if (health.IsDead || watchdog <= 0f) { CancelAttack(); return; }
        if (Phase != EnemyAttackPhase.Active) { movement.Stop(); return; }
        float advance = Mathf.Min(deltaTime, Mathf.Max(0f, step.activeDuration - activeElapsed));
        activeElapsed += deltaTime;
        if (advance > 0f && step.lungeDistance > 0f)
            movement.MoveAtSpeed(direction, step.lungeDistance / step.activeDuration * advance / Mathf.Max(deltaTime, .0001f), deltaTime);
        else movement.Stop();
        TickHitbox();
    }
    public void BeginHitbox()
    {
        if (!isActiveAndEnabled || health.IsDead || Phase != EnemyAttackPhase.Prepare) return;
        Phase = EnemyAttackPhase.Active; TickHitbox();
    }
    public void EndHitbox()
    {
        if (Phase != EnemyAttackPhase.Active) return;
        Phase = EnemyAttackPhase.Recovery; movement.Stop();
    }
    public void EndStep()
    {
        if (!isActiveAndEnabled || health.IsDead || Phase != EnemyAttackPhase.Recovery) return;
        if (Pattern == BossPattern.Combo && StepIndex + 1 < combo.Length)
        {
            StepIndex++; BeginStep(combo[StepIndex]);
        }
        else CancelAttack();
    }
    private void TickHitbox()
    {
        foreach (var hit in Physics2D.OverlapBoxAll(HitboxCenter, step.hitboxSize, 0f, targetLayer))
        {
            if (hit.transform.IsChildOf(transform) || hit.GetComponentInParent<PlayerController>() == null) continue;
            var receiver = hit.GetComponentInParent<IAttackReceiver>();
            if (receiver == null || hitTargets.Contains(receiver)) continue;
            var block = Physics2D.Linecast((Vector2)transform.position + Vector2.up * step.hitboxOffset.y, hit.bounds.center, obstacleLayer);
            if (block.collider != null && !block.collider.isTrigger) continue;
            hitTargets.Add(receiver);
            var result = receiver.ReceiveAttack(new AttackInfo(gameObject, step.damage, Vector2.right * direction));
            if (result == AttackResult.Parried)
            {
                if (Pattern == BossPattern.Combo && StepIndex + 1 < combo.Length)
                {
                    Phase = EnemyAttackPhase.Recovery; movement.Stop(); view.DeflectStrike();
                }
                else
                {
                    var interruptedPattern = Pattern;
                    CancelAttack(); controller?.EnterParried(interruptedPattern);
                }
                break;
            }
            if (!IsAttacking || health.IsDead) break;
        }
    }
    public void CancelAttack()
    {
        Phase = EnemyAttackPhase.None; Pattern = BossPattern.None; watchdog = 0;
        if (movement != null) movement.Stop();
        if (view != null) view.CancelAttack();
    }
    private void OnDrawGizmosSelected()
    {
        if (step == null) return;
        Gizmos.color = Phase == EnemyAttackPhase.Active ? Color.red : Color.yellow;
        Gizmos.DrawWireCube(HitboxCenter, step.hitboxSize);
    }
}
