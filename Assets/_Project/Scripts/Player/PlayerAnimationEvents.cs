using UnityEngine;

public sealed class PlayerAnimationEvents : MonoBehaviour
{
    [SerializeField] private PlayerCombat combat;

    private void Awake()
    {
        if (combat == null)
            combat = GetComponentInParent<PlayerCombat>();
    }

    // 공격 애니메이션의 마지막 프레임에 호출
    // Attack1
    public void EndAttack()
    {
        if (combat != null)
            combat.EndAttack();
    }

    public void BeginHitbox()
    {
        if (combat != null)
            combat.BeginHitbox();
    }

    public void EndHitbox()
    {
        if (combat != null)
            combat.EndHitbox();
    }
}