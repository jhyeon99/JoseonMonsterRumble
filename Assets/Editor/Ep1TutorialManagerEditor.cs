using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Ep1TutorialManager))]
internal sealed class Ep1TutorialManagerEditor : Editor
{
    private bool _showLayout;
    private bool _showTiming;
    private bool _showPoints;
    private bool _showFocusSettings;
    private bool _showFocusReferences;
    private bool _showTalismanSettings;
    private bool _showCheckpointSettings;
    private bool _showParrySettings;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("암전 해제 → 장승 시네마틱 / 리스폰 설명 → Enter → 카메라 복귀 / 머리 위 점프 안내 → 실제 장승 체크포인트 활성화 → 첫 번째 요괴 가드 안내 → 일반 가드 성공 → 가드 게이지 설명. 첫 번째 요괴 처치 후 영혼 설명. 두 번째 요괴는 패링 안내 → 패링 성공한 요괴 확대 / X 마무리 공격 설명 → Enter → 마무리 공격. 세 번째 요괴는 추가 안내 없이 처치. 세 마리 처치 후 새가 나타나고 영혼 수집과 화면 진입을 확인하면 부적 튜토리얼이 시작됩니다.", MessageType.Info);
        EditorGUILayout.LabelField("공통 글자 설정", EditorStyles.boldLabel);
        Draw("_font", "폰트 / 글씨체");
        Draw("_fontSize", "글자 크기");
        SerializedProperty style = serializedObject.FindProperty("_fontStyle");
        style.enumValueIndex = EditorGUILayout.Popup("굵기 / 글자 스타일", style.enumValueIndex,
            new[] { "보통", "굵게 (Bold)", "기울임 (Italic)", "굵게 + 기울임" });
        Draw("_textColor", "글자 색");
        Draw("_outlineColor", "외곽선 색");
        Draw("_outlineThickness", "외곽선 두께", "0이면 외곽선을 숨깁니다.");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("튜토리얼 문구", EditorStyles.boldLabel);
        Draw("_highJumpMessage", "높이 점프 안내");
        Draw("_respawnMessage", "장승 시네마틱 설명");
        Draw("_parryMessage", "패링 안내");
        Draw("_parryFinisherMessage", "패링 성공 / X 마무리 공격 설명");
        Draw("_keySeparator", "점프 키 사이 문구");

        _showCheckpointSettings = EditorGUILayout.Foldout(_showCheckpointSettings, "씬 시작 장승 시네마틱", true);
        if (_showCheckpointSettings)
        {
            Draw("_enableCheckpointCinematic", "장승 시네마틱 사용");
            Draw("_respawnHintTarget", "확대 / 활성화할 장승", "비어 있으면 같은 씬의 Jangseung_01을 연결합니다. 이 장승에 실제로 체크포인트를 저장해야 가드 안내로 넘어갑니다.");
            Draw("_checkpointRevealDelay", "암전 해제 후 대기 시간", "암전이 완전히 걷힌 뒤 이 시간만큼 기다리고 장승으로 이동합니다. 0이어도 화면을 한 프레임 보여준 뒤 시작합니다.");
            Draw("_checkpointZoomFactor", "확대 후 카메라 크기 비율");
            Draw("_checkpointZoomDuration", "장승으로 이동 / 확대 시간");
            Draw("_checkpointReturnDuration", "카메라 복귀 시간");
            Draw("_checkpointLetterboxAmount", "시네마틱 위 / 아래 검은 띠");
            EditorGUILayout.HelpBox("암전이 완전히 걷힌 뒤 장승을 확대해 리스폰 설명과 Enter를 표시합니다. 카메라가 돌아오면 기존 Hint 위치와 관계없이 점프 안내가 머리 위에 바로 뜹니다. 장승 체크포인트를 활성화한 뒤 첫 요괴에 접근하면 가드를 안내합니다. 글자 스타일과 Enter 문구는 화면 정지 안내 설정을 공유합니다.", MessageType.None);
        }

        _showParrySettings = EditorGUILayout.Foldout(_showParrySettings, "패링 성공 / 마무리 공격 시네마틱", true);
        if (_showParrySettings)
        {
            Draw("_parryZoomFactor", "확대 후 카메라 크기 비율");
            Draw("_parryZoomDuration", "패링한 요괴로 이동 / 확대 시간");
            Draw("_parryReturnDuration", "카메라 복귀 시간");
            Draw("_parryLetterboxAmount", "시네마틱 위 / 아래 검은 띠");
            EditorGUILayout.HelpBox("두 번째 요괴의 공격을 실제로 패링했을 때 한 번 표시합니다. 화면이 멈춰도 노란 깜빡임은 계속되고 그로기 시간은 줄어들지 않습니다. Enter로 카메라가 돌아오면 X 마무리 공격을 이어갈 수 있습니다. 문구는 위 튜토리얼 문구에서, 글자 스타일은 화면 정지 안내 설정에서 수정합니다.", MessageType.None);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("가드 / 영혼 안내 문구", EditorStyles.boldLabel);
        Draw("_guardHoldMessage", "아래 방향키 / 가드 요청");
        Draw("_guardGaugeMessage", "가드 게이지 설명");
        EditorGUILayout.HelpBox("가드 키를 누르기만 해서는 게이지 설명으로 넘어가지 않습니다. 첫 요괴의 공격을 일반 가드로 실제로 막으면 화면을 멈추고 설명합니다. 패링 성공은 일반 가드 성공으로 세지 않습니다.", MessageType.None);
        Draw("_guardEncounterDistance", "첫 번째 요괴 가드 안내 거리");
        Draw("_parryEncounterDistance", "두 번째 요괴 패링 안내 거리");
        Draw("_soulDropMessage", "처치 영혼 설명");
        Draw("_soulGaugeMessage", "영혼 UI 설명");
        Draw("_continueMessage", "Enter 진행 문구");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("부적 공격 안내 문구", EditorStyles.boldLabel);
        Draw("_talismanIntroMessage", "공중 요괴 발견 / 짧게 누르기 설명");
        Draw("_talismanTapMessage", "부적 한 장 발사 요청");
        Draw("_talismanChargeExplanation", "길게 누르기 / 영혼 소모 설명");
        Draw("_talismanChargeMessage", "충전 발사 요청");
        _showTalismanSettings = EditorGUILayout.Foldout(_showTalismanSettings, "부적 튜토리얼 대상 / 시네마틱 설정", true);
        if (_showTalismanSettings)
        {
            Draw("_enableTalismanTutorial", "부적 튜토리얼 사용");
            EditorGUILayout.HelpBox("연결이 비어 있으면 EnemyMeleeGround / (1) / (2)와 EnemyRangedFlying (3)를 자동 연결합니다. 이름이 같은 요괴가 여러 마리이면 공중 요괴 앞쪽에서 가장 가까운 대상을 선택합니다. 다른 구간에서는 아래 요괴를 직접 지정하세요.", MessageType.None);
            Draw("_talismanPrerequisiteEnemies", "처치할 지상 요괴 (진행 순서)", "0: 가드 실습, 1: 패링 실습, 2: 자유 실습. 이 목록의 요괴를 모두 처치해야 합니다. 다른 요괴의 처치는 세지 않습니다.");
            Draw("_talismanTarget", "확대할 공중 요괴");
            Draw("_hideTalismanTargetUntilPrerequisites", "세 마리 처치 후 공중 요괴 등장", "연결된 새를 처음에는 숨겼다가 지상 요괴가 모두 처치되면 원래 위치에 표시합니다.");
            if (GUILayout.Button("현재 씬의 EP1 기본 요괴 연결"))
            {
                serializedObject.ApplyModifiedProperties();
                foreach (Object selected in targets)
                {
                    var manager = (Ep1TutorialManager)selected;
                    Undo.RecordObject(manager, "Connect EP1 talisman tutorial targets");
                    manager.ResolveTalismanTutorialTargets();
                    EditorUtility.SetDirty(manager);
                }
                serializedObject.Update();
            }
            Draw("_talismanKeySprite", "V키 이미지 (선택)");
            Draw("_talismanVisibleDelay", "화면에 들어온 후 대기");
            Draw("_talismanMinimumSouls", "부적 안내 시작에 필요한 영혼", "세 마리 처치 후 실제 영혼 게이지가 이 값 이상 차야 시네마틱을 시작합니다. 기본값 3은 한 장 발사와 두 장 충전 실습에 필요한 양입니다.");
            Draw("_talismanZoomFactor", "확대 후 카메라 크기 비율", "작을수록 더 크게 보입니다. 요괴가 화면에 잘리지 않도록 최소 크기도 계산합니다.");
            Draw("_talismanZoomDuration", "요괴로 이동 / 확대 시간");
            Draw("_talismanReturnDuration", "원래 카메라로 복귀 시간");
            Draw("_talismanLetterboxAmount", "시네마틱 위 / 아래 검은 띠");
            Draw("_talismanShotPauseDelay", "한 장 발사 후 설명 대기", "발사 연출이 끝난 뒤 충전 설명을 위해 멈춥니다.");
            Draw("_refillTalismanPracticeSouls", "부족한 실습 영혼 보충", "실습 중에만 한 장 + 충전 두 장에 필요한 영혼을 보충합니다. 완료 후에는 보충하지 않습니다.");
        }

        _showFocusSettings = EditorGUILayout.Foldout(_showFocusSettings, "화면 정지 안내의 글자 / 강조 설정", true);
        if (_showFocusSettings)
        {
            EditorGUILayout.HelpBox("폰트 파일은 공통 글자 설정을 사용합니다. 아래 값은 화면을 멈추고 띄우는 설명에 적용됩니다.", MessageType.None);
            Draw("_popupFontSize", "설명 글자 크기");
            Draw("_popupFontStyle", "설명 굵기 / 글자 스타일");
            Draw("_popupTextColor", "설명 글자 색");
            Draw("_popupOutlineColor", "설명 외곽선 색");
            Draw("_popupOutlineThickness", "설명 외곽선 두께");
            Draw("_dimOpacity", "검은 배경 투명도", "0이면 투명, 1이면 완전히 검게 가립니다.");
            Draw("_focusBorderColor", "강조 테두리 색");
            Draw("_focusPadding", "강조 영역 여백");
            Draw("_popupTextSize", "설명 문구 영역");
            Draw("_soulFocusDelay", "영혼 생성 후 강조 대기", "실제 처치 영혼이 잠깐 퍼진 뒤 그 자리에서 화면을 멈춥니다.");
            Draw("_soulFocusPadding", "영혼 강조 영역 여백");
            Draw("_soulFocusMinimumSize", "영혼 강조 네모 최소 크기", "게임 화면에 실제로 생성된 영혼들의 위치를 감쌉니다.");
            Draw("_enterDelay", "Enter 입력 대기 시간", "화면이 뜨자마자 실수로 넘기는 것을 방지합니다.");
        }

        _showFocusReferences = EditorGUILayout.Foldout(_showFocusReferences, "가드 / 영혼 UI와 키 이미지 연결", true);
        if (_showFocusReferences)
        {
            Draw("_guardGaugeFocus", "강조할 가드 게이지");
            Draw("_soulGaugeFocus", "강조할 영혼 줄");
            Draw("_guardKeySprite", "아래 방향키 이미지");
            Draw("_continueKeySprite", "Enter 이미지");
        }

        EditorGUILayout.Space();
        _showLayout = EditorGUILayout.Foldout(_showLayout, "머리 위 위치 / 문구 영역", true);
        if (_showLayout)
        {
            Draw("_headOffset", "머리 위 위치", "Y가 클수록 머리에서 더 위에 표시합니다.");
            Draw("_jumpTextSize", "점프 문구 영역", "글자가 크거나 문구가 길어지면 영역도 넓혀 주세요.");
            Draw("_infoTextSize", "가드 / 패링 / 부적 문구 영역");
        }

        _showTiming = EditorGUILayout.Foldout(_showTiming, "표시 거리 / 시간", true);
        if (_showTiming)
        {
            Draw("_triggerDistance", "안내 시작 거리");
            Draw("_displayDuration", "패링 표시 시간");
            Draw("_holdDuration", "점프 길게 누르기 시간");
        }

        _showPoints = EditorGUILayout.Foldout(_showPoints, "Hint 위치 오브젝트 연결", true);
        if (_showPoints)
        {
            Draw("_followHintTargets", "가드 안내가 요괴 위치를 따라감", "첫 번째 요괴를 옮겨도 실제 요괴 위치에서 안내합니다. 끄면 기존 가드 Hint 위치를 사용합니다.");
            EditorGUILayout.HelpBox("장승은 '장승 시네마틱 / 시작 타이밍'에서 지정하고, 가드 안내는 처치할 지상 요괴 목록의 0번 대상을 사용합니다. 장승과 요괴를 옮겨도 Hint를 함께 옮길 필요가 없습니다.", MessageType.None);
            if (GUILayout.Button("현재 씬의 장승 / 요괴 연결"))
            {
                serializedObject.ApplyModifiedProperties();
                foreach (Object selected in targets)
                {
                    var manager = (Ep1TutorialManager)selected;
                    Undo.RecordObject(manager, "Connect EP1 hint targets");
                    manager.ResolveHintTargets();
                    EditorUtility.SetDirty(manager);
                }
                serializedObject.Update();
            }
            Draw("_highJumpHintPoint", "높이 점프 Hint");
            Draw("_respawnHintPoint", "장승 자동 연결 기준 Hint", "장승 자동 선택에만 사용합니다. 머리 위 장승 문구는 생성하지 않습니다.");
            Draw("_parryHintPoint", "패링 Hint");
            Draw("_guardHintPoint", "가드 Hint (패링 이전)");
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void Draw(string propertyName, string label, string tooltip = null)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), new GUIContent(label, tooltip), true);
    }
}
