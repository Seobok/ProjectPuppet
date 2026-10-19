using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(EnemyMovement2D), typeof(BossController))]
public sealed class BossGuard : MonoBehaviour, IAttackGuard
{
    [Tooltip("월드 단위. 좌우 위치가 겹친 공격은 정면 패링으로 처리하지 않습니다.")]
    [SerializeField, Min(0f)] private float frontTolerance = 0.05f;
    private EnemyHealth health;
    private EnemyMovement2D movement;
    private BossController controller;
    public bool IsGuarding => isActiveAndEnabled && controller != null && controller.isActiveAndEnabled &&
        controller.State == BossActionState.Guard && !health.IsDead;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement2D>();
        controller = GetComponent<BossController>();
    }
    public bool TryParry(AttackInfo attack)
    {
        if (!IsGuarding || attack.Attacker == null || attack.Damage <= 0) return false;
        float offset = attack.Attacker.transform.position.x - transform.position.x;
        if (offset * movement.FacingDirection <= frontTolerance) return false;
        controller.OnParrySucceeded();
        return true;
    }
}
