using UnityEngine;

public sealed class PlayerAnimationEvents : MonoBehaviour
{

    [Header("공격 애니메이션 이벤트 연결")]
    [Tooltip("애니메이션에서 공격 판정 시작·종료를 전달할 컴포넌트입니다.")]
    [SerializeField] private PlayerCombat combat;
    [Tooltip("애니메이션에서 공격 종료를 전달할 행동 제어 컴포넌트입니다.")]
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
