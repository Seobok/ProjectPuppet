using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class PlayerHealth : MonoBehaviour, IAttackReceiver
{
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField] private UnityEvent onHit = new UnityEvent();
    [SerializeField] private UnityEvent onDeath = new UnityEvent();
    private PlayerDodge dodge;
    private PlayerCombat combat;
    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    private void Awake()
    {
        dodge = GetComponent<PlayerDodge>();
        combat = GetComponent<PlayerCombat>();
        CurrentHealth = maxHealth;
    }

    public AttackResult ReceiveAttack(AttackInfo attack)
    {
        if (!isActiveAndEnabled || IsDead || attack.Damage <= 0 || attack.Attacker == gameObject)
            return AttackResult.Ignored;
        if (dodge != null && dodge.IsInvulnerable)
            return AttackResult.Evaded;

        CurrentHealth = Mathf.Max(0, CurrentHealth - attack.Damage);
        dodge?.Cancel();
        combat?.CancelAttackForDodge();
        onHit.Invoke();
        if (IsDead) onDeath.Invoke();
        return AttackResult.Hit;
    }
}
