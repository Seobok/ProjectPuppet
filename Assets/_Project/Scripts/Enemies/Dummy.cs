using UnityEngine;

public sealed class Dummy : MonoBehaviour, IAttackReceiver
{
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
