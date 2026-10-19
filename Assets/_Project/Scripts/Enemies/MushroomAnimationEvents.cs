using UnityEngine;

public sealed class MushroomAnimationEvents : MonoBehaviour
{
    private MushroomCombat combat;
    private SpriteRenderer spriteRenderer;
    private void Awake()
    {
        combat = GetComponentInParent<MushroomCombat>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }
    private void LateUpdate()
    {
        // The source uses cropped sprites inside 80px frames. Mirror the crop offset too,
        // otherwise a left-facing lunge shifts the art away from its collider.
        var sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        if (sprite == null) return;
        float x = (sprite.rect.x % 80f + sprite.pivot.x - 40f) * transform.localScale.x / sprite.pixelsPerUnit;
        var position = transform.localPosition;
        position.x = spriteRenderer.flipX ? -x : x;
        transform.localPosition = position;
    }
    public void BeginHitbox() { if (combat != null) combat.BeginHitbox(); }
    public void EndHitbox() { if (combat != null) combat.EndHitbox(); }
    public void EndAttack() { if (combat != null) combat.EndAttack(); }
}
