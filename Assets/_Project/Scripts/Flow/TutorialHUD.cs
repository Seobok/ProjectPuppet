using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class TutorialHUD : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private RetryController flow;
    [SerializeField] private TMP_Text playerHealthText;
    [SerializeField] private UnityEngine.UI.Image playerHealthFill;
    [SerializeField] private TMP_Text checkpointText;
    [SerializeField] private TMP_Text lessonTitle;
    [SerializeField] private TMP_Text lessonBody;
    [SerializeField] private TMP_Text noticeText;
    [SerializeField] private GameObject bossPanel;
    [SerializeField] private TMP_Text bossHealthText;
    [SerializeField] private UnityEngine.UI.Image bossHealthFill;
    [SerializeField] private GameObject modal;
    [SerializeField] private TMP_Text modalTitle;
    [SerializeField] private TMP_Text modalBody;
    [SerializeField] private UnityEngine.UI.Button primaryButton;
    [SerializeField] private TMP_Text primaryLabel;
    [SerializeField] private UnityEngine.UI.Button restartButton;
    private EnemyHealth boss;
    private int playerHp = -1, bossHp = -1;
    private float noticeRemaining;
    private void Awake()
    {
        primaryButton.onClick.AddListener(OnPrimary);
        restartButton.onClick.AddListener(OnRestart);
        modal.SetActive(false); bossPanel.SetActive(false); noticeText.text = "";
    }
    private void OnDestroy() { primaryButton.onClick.RemoveListener(OnPrimary); restartButton.onClick.RemoveListener(OnRestart); }
    private void OnPrimary() { if (flow.State == RunState.Cleared) flow.RestartFromBeginning(); else flow.Retry(); }
    private void OnRestart() => flow.RestartFromBeginning();
    private void Update()
    {
        if (player.CurrentHealth != playerHp)
        {
            playerHp = player.CurrentHealth; playerHealthText.text = $"체력  {playerHp} / {player.MaxHealth}";
            playerHealthFill.fillAmount = (float)playerHp / player.MaxHealth;
        }
        if (boss != null && boss.CurrentHealth != bossHp)
        {
            bossHp = boss.CurrentHealth; bossHealthText.text = $"기사 보스  {bossHp} / {boss.MaxHealth}";
            bossHealthFill.fillAmount = (float)bossHp / boss.MaxHealth;
        }
        if (noticeRemaining > 0f)
        {
            noticeRemaining -= Time.unscaledDeltaTime;
            if (noticeRemaining <= 0f) noticeText.text = "";
        }
    }
    public void SetLesson(string title, string body) { lessonTitle.text = title; lessonBody.text = body; }
    public void SetCheckpoint(string location) => checkpointText.text = "복귀 위치 · " + location;
    public void Notify(string text) { noticeText.text = text; noticeRemaining = 3f; }
    public void SetBoss(EnemyHealth health) { boss = health; bossHp = -1; bossPanel.SetActive(true); }
    private void ShowModal(string title, string body, string action, bool allowRestart)
    {
        modalTitle.text = title; modalBody.text = body; primaryLabel.text = action;
        primaryButton.interactable = true; restartButton.interactable = true; restartButton.gameObject.SetActive(allowRestart);
        modal.SetActive(true);
        Canvas.ForceUpdateCanvases();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(primaryButton.gameObject);
    }
    public void ShowDeath() => ShowModal("다시 도전", RunSession.CheckpointOrder > 0 ? "보스 앞 체크포인트에서 체력을 회복하고 다시 시작합니다." : "훈련장 입구에서 적과 함께 다시 시작합니다.", "재도전", true);
    public void ShowClear()
    {
        bossPanel.SetActive(false);
        ShowModal("훈련 완료", "기사 보스를 처치했습니다. 패링과 회피를 연결해 공방을 이어갔습니다.", "처음부터 다시 시작", false);
    }
    public void ShowLoading()
    {
        modalBody.text = "훈련장을 준비하고 있습니다…";
        primaryButton.interactable = false; restartButton.interactable = false;
    }
}
