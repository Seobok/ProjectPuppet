
public enum AttackResult
{
    Hit,      // 명중
    Parried,  // 패링
    Evaded,   // 회피
    Ignored   // 이미 사망한 대상 등 처리 대상이 아님
}

public interface IAttackReceiver
{
    AttackResult ReceiveAttack(AttackInfo attack);
}