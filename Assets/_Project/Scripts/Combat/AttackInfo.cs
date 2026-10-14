using UnityEngine;

public readonly struct AttackInfo
{
    public readonly GameObject Attacker;
    public readonly int Damage;
    public readonly Vector2 Direction;

    public AttackInfo(GameObject attacker, int damage, Vector2 direction)
    {
        Attacker = attacker;
        Damage = damage;
        Direction = direction;
    }
}