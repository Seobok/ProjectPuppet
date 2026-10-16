using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class PlayerHealth : MonoBehaviour, IAttackReceiver
{
    [Header("Health Settings")]
    [SerializeField, Min(1)] private int maxHealth = 100;
    [Header("Events")]
    [SerializeField] private UnityEvent onHit = new UnityEvent();
    [SerializeField] private UnityEvent onDeath = new UnityEvent();
    
    private PlayerController controller;
    public event Action<bool> Damaged;
    public int CurrentHealth { get; private set; }
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
            return AttackResult.Evaded;

        CurrentHealth = Mathf.Max(0, CurrentHealth - attack.Damage);
        Damaged?.Invoke(IsDead);
        onHit.Invoke();
        if (IsDead) onDeath.Invoke();
        return AttackResult.Hit;
    }
}
