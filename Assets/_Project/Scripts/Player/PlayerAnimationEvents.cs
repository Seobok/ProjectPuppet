using UnityEngine;

public sealed class PlayerAnimationEvents : MonoBehaviour
{
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerController controller;

    private void Awake()
    {
        if (combat == null)
            combat = GetComponentInParent<PlayerCombat>();
        if (controller == null)
            controller = GetComponentInParent<PlayerController>();
    }

    // 공격 애니메이션의 마지막 프레임에 호출
    // Attack1
    public void EndAttack()
    {
        if (controller != null)
            controller.EndAttack();
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
