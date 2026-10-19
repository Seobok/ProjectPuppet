using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(EnemyMovement2D))]
public sealed class EnemyGuard : MonoBehaviour
{

    [Header("정면 판정")]
    [Tooltip("월드 단위. 공격자와 적의 좌우 위치 차이가 이 값 이하면 겹친 것으로 보고 패링하지 않습니다. 방어 각도가 아닙니다.")]
    [SerializeField, Min(0f)] private float frontTolerance = 0.05f;
    private EnemyHealth health;
    private EnemyMovement2D movement;
    private EnemyController controller;
    private EnemyCombat combat;

    public bool IsGuarding => isActiveAndEnabled && controller != null && controller.isActiveAndEnabled &&
        controller.State == EnemyActionState.Guard && !health.IsDead && !combat.IsAttacking;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement2D>();
        controller = GetComponent<EnemyController>();
        combat = GetComponent<EnemyCombat>();
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
