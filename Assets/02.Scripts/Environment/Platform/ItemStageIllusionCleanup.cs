using UnityEngine;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class ItemStageIllusionCleanup : MonoBehaviour
{
    [Tooltip("획득 후에도 남겨둘 무덤입니다. 이 스테이지 루트의 직접 자식이어야 합니다.")]
    [SerializeField] private GameObject _grave;
    private bool _isHidden;

    private void Awake()
    {
        if (_grave == null || _grave.transform.parent != transform)
        {
            Debug.LogWarning($"{name}: 환영 정리에 필요한 무덤 참조 또는 부모 연결이 올바르지 않습니다.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        if (AbilityItemProgress.HasItem(AbilityItemProgress.DokkaebiGamtuId))
            HideIllusions();
    }

    public void HideIllusions()
    {
        if (!isActiveAndEnabled || _isHidden || _grave == null ||
            !AbilityItemProgress.HasItem(AbilityItemProgress.DokkaebiGamtuId))
        {
            return;
        }
        _isHidden = true;
        foreach (Transform child in transform)
        {
            if (child.gameObject != _grave) child.gameObject.SetActive(false);
        }
    }
}
