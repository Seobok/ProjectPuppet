using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public abstract class CombatTuningEditor : Editor
{
    protected VisualElement Begin(string title)
    {
        var root = new VisualElement();
        var script = new PropertyField(serializedObject.FindProperty("m_Script"), "스크립트");
        script.SetEnabled(false);
        root.Add(script);
        root.Add(new Label(title) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6, marginBottom = 4 } });
        root.Add(new HelpBox("아래 항목은 같은 오브젝트의 컴포넌트 설정과 연결됩니다. 시간은 초, 거리는 월드 단위입니다. 마우스를 올리면 설명이 표시됩니다. 플레이 중 바꾼 값은 종료 시 되돌아갑니다.", HelpBoxMessageType.Info));
        return root;
    }

    protected Foldout Group<T>(string title, params (string path, string label)[] fields) where T : Component
    {
        var group = new Foldout { text = title, value = true, viewDataKey = GetType().Name + ":" + typeof(T).Name + ":" + title };
        group.style.marginTop = 4;
        var components = targets.Select(o => ((Component)o).GetComponent<T>()).ToArray();
        if (components.Any(c => c == null))
        {
            group.Add(new HelpBox(typeof(T).Name + " 컴포넌트가 없는 대상이 있습니다. 프리팹의 구성 요소를 확인하세요.", HelpBoxMessageType.Warning));
            return group;
        }
        var settings = new SerializedObject(components.Cast<UnityEngine.Object>().ToArray());
        foreach (var item in fields)
        {
            var property = settings.FindProperty(item.path);
            if (property == null) throw new InvalidOperationException(typeof(T).Name + "." + item.path + " 설정을 찾을 수 없습니다.");
            var field = new PropertyField(property, item.label) { name = typeof(T).Name + "." + item.path, tooltip = property.tooltip };
            field.BindProperty(property);
            group.Add(field);
        }
        return group;
    }

    protected Foldout AttackCenterGroup()
    {
        var group = new Foldout { text = "공격 중심 위치", value = true, viewDataKey = "PlayerTuning.AttackCenter" };
        var centers = targets.Select(o =>
        {
            var combat = ((Component)o).GetComponent<PlayerCombat>();
            if (combat == null) return null;
            return new SerializedObject(combat).FindProperty("attackTransform").objectReferenceValue;
        }).ToArray();
        if (centers.Any(c => c == null))
        {
            group.Add(new HelpBox("공격 중심 Transform이 연결되지 않은 대상이 있습니다. 개발용 연결에서 기준점을 지정하세요.", HelpBoxMessageType.Warning));
            return group;
        }
        var settings = new SerializedObject(centers);
        var position = settings.FindProperty("m_LocalPosition");
        var field = new PropertyField(position, "공격 중심 (로컬 위치)")
        {
            name = "AttackCenter.m_LocalPosition",
            tooltip = "공격 기준점의 부모 기준 위치입니다. X는 전방 거리이며 바라보는 방향에 맞춰 좌우 반전됩니다. Y는 공격 높이입니다. 2D 전투에서는 Z를 보통 0으로 유지합니다. 연결된 기준점 Transform의 실제 위치를 수정합니다."
        };
        field.BindProperty(position);
        group.Add(field);
        return group;
    }
}

[CustomEditor(typeof(PlayerController)), CanEditMultipleObjects]
public sealed class PlayerControllerTuningEditor : CombatTuningEditor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = Begin("플레이어 수치 조정");
        root.Add(Group<PlayerHealth>("체력", ("maxHealth", "최대 체력")));
        root.Add(Group<PlayerMovement2D>("이동 · 점프", ("moveSpeed", "이동 속도 (유닛/초)"), ("jumpSpeed", "점프 초기 속도"), ("wallSlideSpeed", "벽 미끄러짐 속도")));
        root.Add(Group<PlayerCombat>("기본 공격", ("attackDamage", "공격 피해량"), ("attackSize", "공격 판정 크기")));
        root.Add(AttackCenterGroup());
        root.Add(Group<PlayerParry>("패링 · 실패 회복", ("windowDuration", "성공 판정 시간 (초)"), ("recoveryDuration", "실패 회복 시간 (초)")));
        root.Add(Group<PlayerDodge>("회피 · 무적 · 회복", ("distance", "회피 거리"), ("moveDuration", "이동 시간 (초)"), ("invulnerableDuration", "무적 시간 (초)"), ("recoveryDuration", "이동 후 회복 시간 (초)")));
        root.Add(Group<PlayerController>("반동 · 피격 경직", ("recoilDuration", "공격이 패링당한 반동 (초)"), ("hurtDuration", "피격 경직 (초)")));
        root.Add(Group<PlayerController>("후속 입력 예약", ("attackBufferDuration", "회피 후 공격 예약 (초)"), ("parryBufferDuration", "반동 후 패링 예약 (초)")));
        root.Add(Group<PlayerParry>("패링 표현 · 소리 연결", ("onParryStarted", "패링 시작"), ("onParrySucceeded", "패링 성공"), ("onParryFailed", "패링 실패")));
        root.Add(Group<PlayerHealth>("피격 · 사망 표현 연결", ("onHit", "피격"), ("onDeath", "사망")));
        var advanced = new Foldout { text = "개발용 연결 · 안전장치", value = false, viewDataKey = "PlayerTuning.Advanced" };
        advanced.Add(Group<PlayerController>("공격 종료 안전장치", ("attackTimeout", "강제 종료 시간 (초)")));
        advanced.Add(Group<PlayerMovement2D>("지면 판정", ("groundCheck", "판정 중심"), ("groundCheckSize", "판정 크기"), ("groundLayer", "바닥 · 벽 레이어")));
        advanced.Add(Group<PlayerCombat>("공격 대상 · 기준점 연결", ("targetLayer", "공격 대상 레이어"), ("attackTransform", "공격 중심 Transform")));
        advanced.Add(Group<PlayerDodge>("회피 연결", ("enemyBodyLayers", "몸 통과 대상 레이어"), ("dodgeAnimation", "회피 애니메이션")));
        advanced.Add(Group<PlayerInputReader>("입력 연결", ("inputActions", "입력 액션 에셋")));
        root.Add(advanced);
        return root;
    }
}

[CustomEditor(typeof(EnemyController)), CanEditMultipleObjects]
public sealed class EnemyControllerTuningEditor : CombatTuningEditor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = Begin("적 수치 조정");
        root.Add(Group<EnemyHealth>("체력", ("maxHealth", "최대 체력")));
        root.Add(Group<EnemyMovement2D>("접근 이동", ("moveSpeed", "이동 속도 (유닛/초)")));
        root.Add(Group<EnemyController>("감지 · 추적", ("detectionDistance", "감지 거리"), ("loseTargetDistance", "추적 해제 거리"), ("maxHeightDifference", "허용 높이 차이")));
        root.Add(Group<EnemyController>("접근 · 거리 유지", ("stopDistance", "접근 정지 거리"), ("resumeDistance", "접근 재개 거리")));
        root.Add(Group<EnemyCombat>("기본 공격 · 피해", ("attackDistance", "공격 시작 거리"), ("damage", "공격 · 반격 피해량"), ("prepareDuration", "일반 공격 준비 (초)")));
        var guard = Group<EnemyController>("정면 방어 · 패링 후 반격", ("guardDuration", "공격 전 방어 유지 (초)"), ("parrySuccessDuration", "패링 성공 동작 (초)"));
        guard.Add(Group<EnemyCombat>("반격 준비", ("counterPrepareDuration", "반격 준비 (초)")));
        guard.Add(new HelpBox("패링 성공부터 반격 타격까지의 시간은 '패링 성공 동작 + 반격 준비'입니다. 일반 공격 준비 시간과 별도로 적용됩니다.", HelpBoxMessageType.Info));
        root.Add(guard);
        root.Add(Group<EnemyCombat>("공격 · 반격 공통 타격과 빈틈", ("activeDuration", "타격 판정 시간 (초)"), ("recoveryDuration", "공격 후 회복 · 빈틈 (초)")));
        root.Add(Group<EnemyCombat>("공격 판정 범위", ("hitboxOffset", "판정 중심 오프셋"), ("hitboxSize", "판정 크기")));
        root.Add(Group<EnemyController>("피격 · 패링당한 경직", ("hurtDuration", "피격 경직 (초)"), ("parriedDuration", "플레이어에게 패링당한 경직 (초)")));
        root.Add(Group<EnemyMovement2D>("벽 · 낭떠러지 정지", ("wallCheckDistance", "벽 감지 거리"), ("ledgeLookAhead", "발판 앞쪽 검사 거리"), ("groundCheckDepth", "바닥 검사 깊이")));
        root.Add(Group<EnemyHealth>("피격 · 사망 표현 연결", ("onHit", "피격"), ("onDeath", "사망")));
        var advanced = new Foldout { text = "개발용 연결 · 레이어", value = false, viewDataKey = "EnemyTuning.Advanced" };
        advanced.Add(Group<EnemyGuard>("정면 판정 보정", ("frontTolerance", "좌우 겹침 허용 거리")));
        advanced.Add(Group<EnemyController>("추적 대상", ("target", "플레이어 · 비우면 자동 검색")));
        advanced.Add(Group<EnemyCombat>("공격 레이어", ("targetLayer", "공격 대상 레이어"), ("obstacleLayer", "타격 차단 지형 레이어")));
        advanced.Add(Group<EnemyMovement2D>("지형 레이어", ("groundLayer", "지면 · 벽 레이어")));
        root.Add(advanced);
        return root;
    }
}
