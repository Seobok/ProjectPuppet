using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerCombat : MonoBehaviour
{
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Recoiling = Animator.StringToHash("isRecoiling");

    [Header("기본 공격")]
    [Tooltip("한 번의 공격이 대상에게 주는 피해량입니다. 같은 공격으로 같은 대상을 중복 타격하지 않습니다.")]
    [SerializeField, Min(1)] private int attackDamage = 10;

    [Header("공격 판정 범위")]
    [Tooltip("월드 단위. 공격 판정 사각형의 가로(X)·세로(Y) 크기입니다. 선택 시 노란 기즈모로 표시됩니다.")]
    [SerializeField] private Vector2 attackSize = new Vector2(1f, 1f);
    [Tooltip("공격 중심 기준점입니다. 로컬 X 위치가 바라보는 방향에 맞춰 좌우 반전됩니다.")]
    [SerializeField] private Transform attackTransform;

    [Header("연결 및 대상 설정")]
    [Tooltip("공격을 받을 수 있는 대상의 레이어입니다. 적의 Attackable 레이어를 포함합니다.")]
    [SerializeField] private LayerMask targetLayer;
    [Tooltip("플레이어 애니메이터입니다. 수치 조정 시 변경할 필요가 없습니다.")]
    [SerializeField] private Animator animator;
    [Tooltip("공격 방향을 판별할 캐릭터 스프라이트입니다. 수치 조정 시 변경할 필요가 없습니다.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private PlayerController controller;
    private bool hitboxActive;
    private readonly HashSet<IAttackReceiver> hitTargets = new HashSet<IAttackReceiver>();

    private float FacingDirection => spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
    private Vector2 AttackCenter => (Vector2)transform.position + (attackTransform != null ? new Vector2(attackTransform.localPosition.x * FacingDirection, attackTransform.localPosition.y) : Vector2.zero);

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if(spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

    }

    public void TickHitbox()
    {
        if (!hitboxActive)
            return;

        Collider2D[] hits = Physics2D.OverlapBoxAll(AttackCenter, attackSize, 0f, targetLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit.transform.IsChildOf(transform))
                continue;

            IAttackReceiver target = hit.GetComponentInParent<IAttackReceiver>();

            if(target == null)
                continue;

            if (hitTargets.Add(target))
            {
                AttackResult result = target.ReceiveAttack(new AttackInfo(gameObject, attackDamage, hit.transform.position - transform.position));

                Debug.Log(hit.name + " " + result.ToString());

                // 패리 당함
                if (result == AttackResult.Parried)
                {
                    controller.EnterRecoil();
                    break;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(AttackCenter, new Vector3(attackSize.x, attackSize.y, 0f));
    }

    public void BeginAttack()
    {
        hitboxActive = false;
        hitTargets.Clear();
        if (animator != null) animator.SetTrigger(Attack);
    }

    public void FaceAction(float horizontal)
    {
        if (spriteRenderer != null && Mathf.Abs(horizontal) > 0.01f)
            spriteRenderer.flipX = horizontal < 0f;
    }

    public void BeginHitbox()
    {
        if(controller != null && controller.State == PlayerActionState.Attack)
            hitboxActive = true;
    }

    public void EndHitbox()
    {
        hitboxActive = false;
    }

    public void SetRecoil(bool active)
    {
        if (animator != null) animator.SetBool(Recoiling, active);
    }

    public void CancelAttack()
    {
        hitboxActive = false;
        if (animator != null)
        {
            animator.ResetTrigger(Attack);
        }
    }

    private void OnDisable()
    {
        CancelAttack();
        SetRecoil(false);
    }

}
