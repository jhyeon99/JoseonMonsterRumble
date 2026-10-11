using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Ep1TutorialManager))]
internal sealed class Ep1TutorialManagerEditor : Editor
{
    private bool _showLayout;
    private bool _showTiming;
    private bool _showPoints;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("세 가지 안내 문구를 이 매니저에서 수정합니다. Hint 오브젝트를 움직이면 안내가 시작되는 위치가 바뀝니다.", MessageType.Info);
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
        Draw("_respawnMessage", "장승 / 리스폰 안내");
        Draw("_parryMessage", "패링 안내");
        Draw("_keySeparator", "점프 키 사이 문구");

        EditorGUILayout.Space();
        _showLayout = EditorGUILayout.Foldout(_showLayout, "머리 위 위치 / 문구 영역", true);
        if (_showLayout)
        {
            Draw("_headOffset", "머리 위 위치", "Y가 클수록 머리에서 더 위에 표시합니다.");
            Draw("_jumpTextSize", "점프 문구 영역", "글자가 크거나 문구가 길어지면 영역도 넓혀 주세요.");
            Draw("_infoTextSize", "리스폰 / 패링 문구 영역");
        }

        _showTiming = EditorGUILayout.Foldout(_showTiming, "표시 거리 / 시간", true);
        if (_showTiming)
        {
            Draw("_triggerDistance", "안내 시작 거리");
            Draw("_displayDuration", "리스폰 / 패링 표시 시간");
            Draw("_holdDuration", "점프 길게 누르기 시간");
        }

        _showPoints = EditorGUILayout.Foldout(_showPoints, "Hint 위치 오브젝트 연결", true);
        if (_showPoints)
        {
            Draw("_highJumpHintPoint", "높이 점프 Hint");
            Draw("_respawnHintPoint", "리스폰 Hint");
            Draw("_parryHintPoint", "패링 Hint");
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void Draw(string propertyName, string label, string tooltip = null)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), new GUIContent(label, tooltip), true);
    }
}
