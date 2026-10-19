using UnityEngine;

public sealed class TutorialDirector : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private RetryController flow;
    [SerializeField] private TutorialHUD hud;
    private PlayerInputReader input;
    private PlayerHealth health;
    private Rigidbody2D body;
    private Vector2 lessonOrigin;
    private bool recoilSeen;
    private string bindingGroup;
    public TutorialLesson CurrentLesson { get; private set; }
    private void Start()
    {
        input = player.GetComponent<PlayerInputReader>(); health = player.GetComponent<PlayerHealth>(); body = player.GetComponent<Rigidbody2D>();
        health.AttackResolved += OnAttackResolved;
        EnterLesson(RunSession.CheckpointOrder > 0 ? TutorialLesson.Boss : TutorialLesson.Movement);
    }
    private void OnDestroy() { if (health != null) health.AttackResolved -= OnAttackResolved; }
    public void EnterLesson(TutorialLesson lesson)
    {
        if (flow.State != RunState.Playing) return;
        CurrentLesson = lesson; lessonOrigin = player.transform.position; recoilSeen = false;
        RefreshHint();
    }
    private void Update()
    {
        if (input == null || flow.State != RunState.Playing) return;
        if (bindingGroup != input.BindingGroup) RefreshHint();
        if (CurrentLesson == TutorialLesson.Movement && Mathf.Abs(player.transform.position.x - lessonOrigin.x) > 1.5f) Complete(CurrentLesson);
        if (CurrentLesson == TutorialLesson.Jump && body.linearVelocity.y > 1f && !player.GetComponent<PlayerMovement2D>().IsGrounded) Complete(CurrentLesson);
        if (CurrentLesson == TutorialLesson.Attack && player.State == PlayerActionState.Attack) Complete(CurrentLesson);
        if (!recoilSeen && CurrentLesson == TutorialLesson.Parry && player.State == PlayerActionState.Recoil) { recoilSeen = true; RefreshHint(); }
    }
    private void OnAttackResolved(AttackResult result)
    {
        if (flow.State != RunState.Playing) return;
        if (result == AttackResult.Parried) Complete(TutorialLesson.Parry);
        if (result == AttackResult.Evaded) Complete(TutorialLesson.Dodge);
    }
    private void Complete(TutorialLesson lesson)
    {
        if (RunSession.IsComplete(lesson)) return;
        RunSession.Complete(lesson);
        if (lesson == CurrentLesson) RefreshHint();
    }
    private string Key(string action) => input != null ? input.GetBindingLabel(action) : action;
    private void RefreshHint()
    {
        if (input != null) bindingGroup = input.BindingGroup;
        string title, text;
        switch (CurrentLesson)
        {
            case TutorialLesson.Jump: title = "02  점프"; text = $"{Key("Jump")} · 낮은 발판을 뛰어넘으세요."; break;
            case TutorialLesson.Attack: title = "03  기본 공격"; text = $"{Key("Attack")} · 적을 향해 기본 공격을 사용하세요."; break;
            case TutorialLesson.Dodge: title = "04  회피와 뒤잡기"; text = $"{Key("Dodge")} · Mushroom의 돌진을 통과하고 뒤에서 공격하세요."; break;
            case TutorialLesson.Parry:
                title = "05  패링과 공방";
                text = recoilSeen ? $"공격이 튕겼습니다. {Key("Dodge")}로 회피하거나 반동이 끝난 뒤 {Key("Parry")}로 반격을 패링하세요."
                    : $"{Key("Parry")} · 정면 공격을 받아내세요. 방어 자세의 적을 먼저 공격하면 튕길 수 있습니다.";
                break;
            case TutorialLesson.Boss: title = "06  기사 보스"; text = $"공격 {Key("Attack")} · 패링 {Key("Parry")} · 회피 {Key("Dodge")}\n연격은 두 번 패링하고, 강공격은 회피하세요. 방어 자세를 치면 반격합니다."; break;
            default: title = "01  이동"; text = $"{Key("Move")} · 오른쪽으로 이동해 훈련장을 통과하세요."; break;
        }
        bool complete = RunSession.IsComplete(CurrentLesson);
        hud.SetLesson(title + (complete ? "  / 완료" : ""), text + (complete ? "\n다음 구간으로 진행할 수 있습니다." : ""));
    }
}
