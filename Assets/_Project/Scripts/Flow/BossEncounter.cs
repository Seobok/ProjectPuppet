using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class BossEncounter : MonoBehaviour
{
    [SerializeField] private GameObject boss;
    [SerializeField] private GameObject entranceGate;
    [SerializeField] private RetryController flow;
    [SerializeField] private TutorialHUD hud;
    [SerializeField] private TutorialDirector tutorial;
    private EnemyHealth health;
    public bool Started { get; private set; }
    public bool Completed { get; private set; }
    public EnemyHealth Health => health;
    private void Awake() => health = boss.GetComponent<EnemyHealth>();
    private void OnEnable() => health.Died += OnBossDied;
    private void OnDisable() => health.Died -= OnBossDied;
    private void Start() { entranceGate.SetActive(false); boss.SetActive(false); }
    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController>();
        if (player != null) TryEnter(player);
    }
    public bool TryEnter(PlayerController player)
    {
        if (Started || flow.State != RunState.Playing || player != flow.Player || player.GetComponent<PlayerHealth>().IsDead) return false;
        Started = true; entranceGate.SetActive(true); boss.SetActive(true);
        hud.SetBoss(health); tutorial.EnterLesson(TutorialLesson.Boss);
        return true;
    }
    private void OnBossDied()
    {
        if (!Started || Completed) return;
        Completed = true; entranceGate.SetActive(false); flow.CompleteRun();
    }
}
