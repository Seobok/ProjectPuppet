using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossAnimation : MonoBehaviour
{
    [Header("애니메이션 연결")]
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip hurtAnimation;
    [SerializeField] private AnimationClip parryAnimation;
    [SerializeField] private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private float flashRemaining;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
    }
    public void SetMoving(bool moving) { if (animator != null) animator.SetBool("IsMoving", moving); }
    public void SetGuarding(bool guarding) { if (animator != null) animator.SetBool("IsGuarding", guarding); }
    public void BeginStep(BossAttackStep step)
    {
        if (animator == null) return;
        animator.ResetTrigger("StrikeParried");
        animator.SetBool("IsMoving", false);
        animator.SetBool("IsGuarding", false);
        animator.SetBool("IsAttacking", true);
        animator.SetInteger("Step", step.animationId);
        animator.SetFloat("PrepareSpeed", step.prepareAnimation.length / step.prepareDuration);
        animator.SetFloat("StrikeSpeed", step.strikeAnimation.length / step.activeDuration);
        animator.SetFloat("RecoverySpeed", step.recoveryAnimation.length / step.recoveryDuration);
        animator.ResetTrigger("Attack"); animator.SetTrigger("Attack");
    }
    public void DeflectStrike()
    {
        if (animator != null) animator.SetTrigger("StrikeParried");
        Flash(new Color(1f, .9f, .3f, baseColor.a));
    }
    public void CancelAttack()
    {
        if (animator == null) return;
        animator.ResetTrigger("Attack"); animator.ResetTrigger("StrikeParried");
        animator.SetBool("IsAttacking", false);
    }
    public void PlayParry(float duration)
    {
        if (animator == null) return;
        animator.SetBool("IsGuarding", false);
        animator.SetFloat("ParrySpeed", parryAnimation.length / duration);
        animator.ResetTrigger("Parry"); animator.SetTrigger("Parry");
        Flash(new Color(1f, .9f, .3f, baseColor.a));
    }
    public void PlayStagger(float duration)
    {
        if (animator == null) return;
        animator.ResetTrigger("Parry");
        animator.SetBool("IsStaggered", true);
        animator.SetFloat("HurtSpeed", hurtAnimation.length / Mathf.Max(.01f, duration));
        animator.ResetTrigger("Hurt"); animator.SetTrigger("Hurt");
    }
    public void EndStagger() { if (animator != null) animator.SetBool("IsStaggered", false); }
    public void PlayDeath()
    {
        CancelAttack(); SetMoving(false); SetGuarding(false);
        if (animator == null) return;
        animator.ResetTrigger("Hurt"); animator.ResetTrigger("Parry");
        animator.SetBool("IsStaggered", false); animator.SetBool("IsDead", true);
    }
    public void FlashHit() => Flash(new Color(1f, .45f, .45f, baseColor.a));
    private void Flash(Color color)
    {
        if (spriteRenderer == null) return;
        spriteRenderer.color = color; flashRemaining = .09f;
    }
    private void Update()
    {
        if (flashRemaining <= 0f) return;
        flashRemaining -= Time.deltaTime;
        if (flashRemaining <= 0f && spriteRenderer != null) spriteRenderer.color = baseColor;
    }
    private void OnDisable()
    {
        flashRemaining = 0f;
        if (spriteRenderer != null) spriteRenderer.color = baseColor;
    }
}
