using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class PlayerHealth : MonoBehaviour, IAttackReceiver
{

    [Header("체력")]
    [Tooltip("플레이 시작 시 설정되는 최대 체력입니다. 실행 중 값을 바꿔도 현재 체력이 즉시 회복되지는 않습니다.")]
    [SerializeField, Min(1)] private int maxHealth = 100;

    [Header("피격 및 사망 표현 연결")]
    [Tooltip("실제로 피해를 받았을 때 호출됩니다. 회피·패링 성공 시에는 호출하지 않습니다.")]
    [SerializeField] private UnityEvent onHit = new UnityEvent();
    [Tooltip("체력이 0이 되어 사망할 때 한 번 호출됩니다.")]
    [SerializeField] private UnityEvent onDeath = new UnityEvent();
    
    private PlayerController controller;
    public event Action<bool> Damaged;
    public event Action<AttackResult> AttackResolved;
    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDead => CurrentHealth <= 0;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        CurrentHealth = maxHealth;
    }

    public AttackResult ReceiveAttack(AttackInfo attack)
    {
        if (!isActiveAndEnabled || IsDead || attack.Damage <= 0 || attack.Attacker == gameObject)
            return AttackResult.Ignored;
        if (controller != null && controller.IsInvulnerable)
        {
            AttackResolved?.Invoke(AttackResult.Evaded);
            return AttackResult.Evaded;
        }
        var parry = GetComponent<PlayerParry>();
        if (parry != null && parry.TryReceive(attack))
        {
            AttackResolved?.Invoke(AttackResult.Parried);
            return AttackResult.Parried;
        }

        CurrentHealth = Mathf.Max(0, CurrentHealth - attack.Damage);
        Damaged?.Invoke(IsDead);
        onHit.Invoke();
        if (IsDead) onDeath.Invoke();
        AttackResolved?.Invoke(AttackResult.Hit);
        
        Debug.Log("CurrentHealth = " + CurrentHealth);
        
        return AttackResult.Hit;
    }

    public bool HealToFull()
    {
        if (IsDead) return false;
        CurrentHealth = maxHealth;
        return true;
    }
}
