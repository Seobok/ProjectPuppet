using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)] // Initialize health before EnemyAnimation.OnEnable reads IsDead.
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyHealth : MonoBehaviour, IAttackReceiver
{

    [Header("체력")]
    [Tooltip("플레이 시작 시 설정되는 최대 체력입니다. 패링 성공 시에는 체력이 감소하지 않습니다.")]
    [SerializeField, Min(1)] private int maxHealth = 30;

    [Header("피격 및 사망 표현 연결")]
    [Tooltip("실제로 피해를 받았을 때 호출됩니다. 패링 성공 시에는 호출하지 않습니다.")]
    [SerializeField] private UnityEvent onHit = new UnityEvent();
    [Tooltip("사망 시 한 번 호출됩니다. 이 시점에 적의 공격·방어·몸 충돌도 종료됩니다.")]
    [SerializeField] private UnityEvent onDeath = new UnityEvent();

    private Rigidbody2D body;
    private Collider2D[] bodyColliders;
    private EnemyGuard guard;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDead => CurrentHealth <= 0;
    public event Action Damaged;
    public event Action Died;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        body = GetComponent<Rigidbody2D>();
        bodyColliders = GetComponentsInChildren<Collider2D>();
        guard = GetComponent<EnemyGuard>();
    }

    public AttackResult ReceiveAttack(AttackInfo attack)
    {
        if (!isActiveAndEnabled || IsDead || attack.Damage <= 0 ||
            (attack.Attacker != null && attack.Attacker.transform.IsChildOf(transform)))
            return AttackResult.Ignored;

        if (guard != null && guard.TryParry(attack)) return AttackResult.Parried;

        CurrentHealth = Mathf.Max(0, CurrentHealth - attack.Damage);
        if (IsDead)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
            foreach (Collider2D bodyCollider in bodyColliders)
                if (bodyCollider != null) bodyCollider.enabled = false;
            Died?.Invoke();
            onDeath.Invoke();
        }
        else
        {
            Damaged?.Invoke();
        }
        onHit.Invoke();
        return AttackResult.Hit;
    }
}
