using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfigureTitleMenu
{
    [MenuItem("Tools/Configure Title Menu")]
    private static void Configure()
    {
        Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/01.Scenes/Test_TitleScene.unity")
        {
            Debug.LogError("Test_TitleScene을 연 뒤 실행하세요.");
            return;
        }

        GameObject canvas = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Title_Canvas");
        if (canvas == null)
        {
            Debug.LogError("Title_Canvas를 찾지 못했습니다.");
            return;
        }

        Transform menu = canvas.transform.Find("Menu_Container");
        if (menu == null)
        {
            Debug.LogError("Menu_Container를 찾지 못했습니다.");
            return;
        }

        Sprite[] normal = AssetDatabase.LoadAllAssetsAtPath("Assets/05.Art/UI/Title/기본.png").OfType<Sprite>().ToArray();
        Sprite[] selected = AssetDatabase.LoadAllAssetsAtPath("Assets/05.Art/UI/Title/선택.png").OfType<Sprite>()
            .ToArray();
        string[] names = new[] { "Start", "Settings", "Quit" };
        if (normal.Length < 3 || selected.Length < 3)
        {
            Debug.LogError("기본/선택 이미지에서 버튼 스프라이트 3개씩을 찾지 못했습니다.");
            return;
        }

        TitleMenuActions actions = canvas.GetComponent<TitleMenuActions>();
        if (actions == null)
        {
            actions = Undo.AddComponent<TitleMenuActions>(canvas);
        }

        for (int i = 0; i < names.Length; i++)
        {
            Transform buttonTransform = menu.Find("Menu_Button_" + names[i]);
            if (buttonTransform == null)
            {
                Debug.LogError("Menu_Button_" + names[i] + "을(를) 찾지 못했습니다.");
                return;
            }

            Sprite normalSprite = normal.FirstOrDefault(s => s.name == "기본_" + i);
            Sprite selectedSprite = selected.FirstOrDefault(s => s.name == "선택_" + i);
            if (normalSprite == null || selectedSprite == null)
            {
                Debug.LogError(names[i] + "의 스프라이트가 없습니다.");
                return;
            }

            Image image = buttonTransform.GetComponent<Image>();
            if (image == null) image = Undo.AddComponent<Image>(buttonTransform.gameObject);
            Undo.RecordObject(image, "Configure title button image");
            image.sprite = normalSprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = true;

            var rect = (RectTransform)buttonTransform;
            Undo.RecordObject(rect, "Resize title button");
            rect.sizeDelta = new Vector2(380, 110);

            Button button = buttonTransform.GetComponent<Button>();
            Undo.RecordObject(button, "Configure title button");
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;

            TitleMenuButton titleButton = buttonTransform.GetComponent<TitleMenuButton>();
            var serialized = new SerializedObject(titleButton);
            serialized.FindProperty("_buttonImage").objectReferenceValue = image;
            serialized.FindProperty("_normalSprite").objectReferenceValue = normalSprite;
            serialized.FindProperty("_selectedSprite").objectReferenceValue = selectedSprite;
            serialized.FindProperty("_isDefaultSelected").boolValue = (i == 0);

            var text = serialized.FindProperty("_buttonText").objectReferenceValue as Component;
            var frame = serialized.FindProperty("_selectFrame").objectReferenceValue as GameObject;
            if (text != null)
            {
                Undo.RecordObject(text.gameObject, "Hide old title text");
                text.gameObject.SetActive(false);
            }

            if (frame != null)
            {
                Undo.RecordObject(frame, "Hide old selection frame");
                frame.SetActive(false);
            }

            serialized.FindProperty("_buttonText").objectReferenceValue = null;
            serialized.FindProperty("_selectFrame").objectReferenceValue = null;
            serialized.ApplyModifiedProperties();

            if (i == 0 && titleButton.OnClickAction.GetPersistentEventCount() == 0)
                UnityEventTools.AddPersistentListener(titleButton.OnClickAction, actions.StartGame);
            if (i == 2 && titleButton.OnClickAction.GetPersistentEventCount() == 0)
                UnityEventTools.AddPersistentListener(titleButton.OnClickAction, actions.QuitGame);
        }

        EditorUtility.SetDirty(actions);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Test_TitleScene 메뉴: 기본/선택 이미지와 두 번 클릭 동작을 연결했습니다.");
    }
}