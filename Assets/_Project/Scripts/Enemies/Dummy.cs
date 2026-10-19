using UnityEngine;

public sealed class Dummy : MonoBehaviour, IAttackReceiver
{

    [Header("테스트용 공격 결과")]
    [Tooltip("훈련용 더미가 공격을 받았을 때 반환할 결과입니다. 실제 적의 방어 규칙과는 별개입니다.")]
    [SerializeField] private AttackResult dummyType;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public AttackResult ReceiveAttack(AttackInfo attack)
    {
        return dummyType;
    }
}
