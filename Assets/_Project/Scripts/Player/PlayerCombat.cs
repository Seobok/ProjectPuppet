using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerCombat : MonoBehaviour
{
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Recoiling = Animator.StringToHash("isRecoiling");

    [Header("Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Header("Attack")]
    [SerializeField] private Transform attackTransform;
    [SerializeField] private Vector2 attackSize = new Vector2(1f, 1f);
    [SerializeField] private LayerMask targetLayer;
    [SerializeField, Min(1)] private int attackDamage = 10;

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
