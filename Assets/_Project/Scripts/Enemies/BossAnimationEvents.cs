using UnityEngine;

public sealed class BossAnimationEvents : MonoBehaviour
{
    private BossCombat combat;
    private SpriteRenderer spriteRenderer;
    private void Awake()
    {
        combat = GetComponentInParent<BossCombat>(); spriteRenderer = GetComponent<SpriteRenderer>();
    }
    private void LateUpdate()
    {
        // Colour2 is cropped from 120x80 frames; keep the body anchored at source x=54.
        var sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        if (sprite == null) return;
        float units = transform.localScale.x / sprite.pixelsPerUnit;
        float x = (sprite.rect.x % 120f + sprite.pivot.x - 54f) * units;
        transform.localPosition = new Vector3(spriteRenderer.flipX ? -x : x, (sprite.rect.y + sprite.pivot.y) * units, 0f);
    }
    public void BeginHitbox() { if (combat != null) combat.BeginHitbox(); }
    public void EndHitbox() { if (combat != null) combat.EndHitbox(); }
    public void EndStep() { if (combat != null) combat.EndStep(); }
}
