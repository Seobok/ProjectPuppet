using UnityEngine;
using UnityEngine.SceneManagement;

public enum RunState { Playing, Dying, Dead, Cleared, Loading }

[DefaultExecutionOrder(-200)]
public sealed class RetryController : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform startSpawn;
    [SerializeField] private Checkpoint[] checkpoints;
    [SerializeField] private GameObject regularEnemies;
    [SerializeField] private TutorialHUD hud;
    [Tooltip("초. 사망 동작을 보여준 뒤 재도전 메뉴를 표시합니다.")]
    [SerializeField, Min(0f)] private float deathMenuDelay = .8f;
    private PlayerHealth health;
    private PlayerInputReader input;
    private float deathElapsed;
    public RunState State { get; private set; }
    public PlayerController Player => player;
    public AsyncOperation PendingLoad { get; private set; }

    private void Awake() => RunSession.EnterScene(gameObject.scene.path);
    private void Start()
    {
        health = player.GetComponent<PlayerHealth>(); input = player.GetComponent<PlayerInputReader>();
        health.Damaged += OnDamaged;
        var spawn = startSpawn;
        foreach (var checkpoint in checkpoints)
        {
            checkpoint.RefreshVisual();
            if (checkpoint.Id == RunSession.CheckpointId) spawn = checkpoint.SpawnPoint;
        }
        if (RunSession.CheckpointOrder > 0 && regularEnemies != null) regularEnemies.SetActive(false);
        var body = player.GetComponent<Rigidbody2D>();
        player.transform.position = spawn.position;
        body.position = spawn.position; body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        hud.SetCheckpoint(RunSession.CheckpointOrder > 0 ? "보스 앞 체크포인트" : "훈련장 입구");
        if (health.IsDead) OnDamaged(true);
    }
    private void OnDestroy() { if (health != null) health.Damaged -= OnDamaged; }
    private void OnDamaged(bool dead)
    {
        if (!dead || State != RunState.Playing) return;
        State = RunState.Dying; deathElapsed = 0f;
        input.Clear(); input.enabled = false;
    }
    private void Update()
    {
        if (State != RunState.Dying) return;
        deathElapsed += Time.unscaledDeltaTime;
        if (deathElapsed < deathMenuDelay) return;
        State = RunState.Dead; hud.ShowDeath();
    }
    public bool ActivateCheckpoint(Checkpoint checkpoint)
    {
        if (State != RunState.Playing || health.IsDead || !RunSession.ActivateCheckpoint(checkpoint.Id, checkpoint.Order)) return false;
        health.HealToFull();
        if (regularEnemies != null) regularEnemies.SetActive(false);
        foreach (var item in checkpoints) item.RefreshVisual();
        hud.SetCheckpoint("보스 앞 체크포인트");
        hud.Notify("체크포인트 활성화 · 체력 회복");
        return true;
    }
    public bool Retry()
    {
        if (State != RunState.Dead) return false;
        return Reload(false);
    }
    public bool RestartFromBeginning()
    {
        if (State != RunState.Dead && State != RunState.Cleared) return false;
        return Reload(true);
    }
    private bool Reload(bool reset)
    {
        int index = gameObject.scene.buildIndex;
        if (index < 0) { Debug.LogError("Register the tutorial scene in Build Settings before retrying.", this); return false; }
        State = RunState.Loading; hud.ShowLoading();
        if (reset) RunSession.NewRun();
        PendingLoad = SceneManager.LoadSceneAsync(index);
        return true;
    }
    public void CompleteRun()
    {
        if (State != RunState.Playing || health.IsDead) return;
        State = RunState.Cleared;
        RunSession.Complete(TutorialLesson.Boss);
        input.Clear(); input.enabled = false; player.enabled = false;
        hud.ShowClear();
    }
}
