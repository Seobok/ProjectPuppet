using UnityEngine;

public sealed class FallRespawn : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private float killY = -5f;
    private void Update()
    {
        if (player != null && !player.IsDead && player.transform.position.y < killY)
            player.ReceiveAttack(new AttackInfo(gameObject, player.MaxHealth, Vector2.zero));
    }
}
