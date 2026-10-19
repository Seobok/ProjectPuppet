using UnityEngine;

public sealed class EnemyAnimationEvents : MonoBehaviour
{
    private EnemyCombat combat;
    private void Awake() => combat = GetComponentInParent<EnemyCombat>();
    public void BeginHitbox() { if (combat != null) combat.BeginHitbox(); }
    public void EndHitbox() { if (combat != null) combat.EndHitbox(); }
    public void EndAttack() { if (combat != null) combat.EndAttack(); }
}
