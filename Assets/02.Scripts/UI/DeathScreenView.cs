using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public sealed class DeathScreenView : MonoBehaviour
{
    private const string DeathEventSystemObjectName = "DeathUIEventSystem";
    private static readonly int RevealProgressId = Shader.PropertyToID("_RevealProgress");
    private static readonly int BackgroundRectId = Shader.PropertyToID("_BackgroundRect");

    [Header("화면 전환")]
    [SerializeField, Min(0f)] private float _fadeInDuration = 0.6f;
    [SerializeField, Min(0f)] private float _fadeOutDuration = 0.5f;
    [FormerlySerializedAs("_backgroundFadeInDuration")]
    [SerializeField, Min(0f)] private float _backgroundSpreadDuration = 1.2f;
    [SerializeField]
    private AnimationCurve _backgroundSpreadCurve =
        new AnimationCurve(new Keyframe(0f, 0f, 1.5f, 1.5f), new Keyframe(1f, 1f, 0f, 0f));

    [Header("배경")]
    [SerializeField] private Image _blackoutImage;
    [SerializeField] private CanvasGroup _bgOverlayCanvasGroup;
    [SerializeField] private Image _bgOverlayImage;
    [SerializeField] private Material _backgroundInkMaterial;

    [Header("그림 속 사망 원인")]
    [SerializeField] private CanvasGroup _causeOfDeathCanvasGroup;
    [SerializeField] private TextMeshProUGUI _causeOfDeathText;
    [SerializeField, Min(0f)] private float _causeOfDeathFadeDuration = 0.3f;
    [Tooltip("두루마리 글씨와 버튼 크기의 기준 배경 너비")]
    [SerializeField, Min(1f)] private float _artworkReferenceWidth = 1920f;
    [SerializeField, Min(1f)] private float _causeOfDeathFontSize = 30f;

    [Header("버튼")]
    [SerializeField] private Button _deadRunButton;
    [SerializeField] private DeathRunButtonMotion _deadRunButtonMotion;
    [SerializeField, Min(1f)] private float _runButtonWidth = 320f;
    [SerializeField] private AudioClip _runButtonClickClip;
    [SerializeField, Range(0f, 1f)] private float _runButtonClickVolume = 1f;

    [Header("로딩 화면")]
    [SerializeField] private CanvasGroup _loadingCanvasGroup;
    [SerializeField] private Image _loadingImage;
    [SerializeField] private Sprite[] _loadingSprites;
    [SerializeField, Min(0.01f)] private float _loadingFrameInterval = 1f;

    public float MinLoadingDuration => _loadingSprites != null && _loadingSprites.Length > 0
        ? _loadingSprites.Length * _loadingFrameInterval : 3f;

    private CanvasGroup _canvasGroup;
    private bool _restartTriggered;
    private Vector2 _lastArtworkSize;
    private GameObject _ownedEventSystem;
    private Material _backgroundMaterialInstance;
    private Material _originalBackgroundMaterial;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_bgOverlayImage != null && _backgroundInkMaterial != null)
        {
            _originalBackgroundMaterial = _bgOverlayImage.material;
            _backgroundMaterialInstance = new Material(_backgroundInkMaterial);
            _bgOverlayImage.material = _backgroundMaterialInstance;
        }
        if (_deadRunButton != null)
            _deadRunButton.onClick.AddListener(HandleRunButtonClick);
        if (_deadRunButtonMotion != null)
            _deadRunButtonMotion.Pressed += PlayRunButtonClickSound;

        UpdateArtworkLayout();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (_backgroundMaterialInstance != null)
        {
            if (_bgOverlayImage != null)
                _bgOverlayImage.material = _originalBackgroundMaterial;
            Destroy(_backgroundMaterialInstance);
        }
        DestroyOwnedEventSystem();
        GameCursorController.SetDeathScreenVisible(false);
        if (_deadRunButton != null)
            _deadRunButton.onClick.RemoveListener(HandleRunButtonClick);
        if (_deadRunButtonMotion != null)
            _deadRunButtonMotion.Pressed -= PlayRunButtonClickSound;
    }

    private void Update()
    {
        if (_canvasGroup != null && _canvasGroup.interactable && !_restartTriggered &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
             Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)))
        {
            TriggerRestart();
        }
    }

    private void LateUpdate()
    {
        UpdateArtworkLayout();
    }

    private void UpdateArtworkLayout()
    {
        if (_bgOverlayImage == null || _bgOverlayImage.sprite == null ||
            _bgOverlayImage.rectTransform.parent is not RectTransform parent)
        {
            return;
        }

        Vector2 viewport = parent.rect.size;
        Vector2 spriteSize = _bgOverlayImage.sprite.rect.size;
        if (viewport.x <= 0f || viewport.y <= 0f || spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;

        // Keep the paper and its text together when the window aspect ratio changes.
        Vector2 artworkSize = spriteSize * Mathf.Max(viewport.x / spriteSize.x, viewport.y / spriteSize.y);
        if (artworkSize == _lastArtworkSize)
            return;

        _lastArtworkSize = artworkSize;
        RectTransform artwork = _bgOverlayImage.rectTransform;
        artwork.sizeDelta = artworkSize;
        float scale = artworkSize.x / Mathf.Max(1f, _artworkReferenceWidth);
        if (_causeOfDeathText != null)
        {
            _causeOfDeathText.fontSize = _causeOfDeathFontSize * scale;
            _causeOfDeathText.fontSizeMax = _causeOfDeathFontSize * scale;
            _causeOfDeathText.fontSizeMin = _causeOfDeathFontSize * 0.7f * scale;
        }
        if (_deadRunButton != null && _deadRunButton.image != null && _deadRunButton.image.sprite != null)
        {
            Rect spriteRect = _deadRunButton.image.sprite.rect;
            float width = _runButtonWidth * scale;
            ((RectTransform)_deadRunButton.transform).sizeDelta =
                new Vector2(width, width * spriteRect.height / spriteRect.width);
        }
        if (_backgroundMaterialInstance != null)
        {
            Rect rect = artwork.rect;
            _backgroundMaterialInstance.SetVector(BackgroundRectId,
                new Vector4(rect.xMin, rect.yMin, rect.width, rect.height));
        }
    }

    public void SetCauseOfDeath(string text)
    {
        if (_causeOfDeathText == null)
            return;

        _causeOfDeathText.text = GetCauseOfDeathMessage(text);
        _causeOfDeathText.ForceMeshUpdate(ignoreActiveState: true);
    }

    private static string GetCauseOfDeathMessage(string causeOfDeath)
    {
        return causeOfDeath switch
        {
            "방망이 공격" => "두억시니의 방망이에\n두들겨 맞아 사망.",
            "돌진 공격" => "인면조의 박치기에\n정신을 놓고 사망.",
            "돌멩이 공격" => "목객이 던진 돌에\n정통으로 맞아 사망.",
            "화살 공격" => "도전복의 화살에 맞아 사망.\n피하기엔 한발 늦었다.",
            "폭발 공격" => "도깨비불과 함께 장렬히 폭발.",
            "접촉 피해" => "너무 가까이 다가갔다가 사망.\n거리 두기에 실패했다.",
            "추락" => "발을 헛디뎌 추락사.",
            "앞발 때리기" or "근접" => "절불가시의 앞발 후려치기에 맞아 사망.\n보고도 못 피했다.",
            "꼬리" => "절불가시의 꼬리에 후려 맞아 사망.\n뒤를 조심하자.",
            "가시 접촉 피해" => "가시에 찔려 사망.",
            "혓바닥 공격" or "Tongue Low Collider" or "Tongue High Collider" => "절불가시의 혓바닥에 휘감겨 사망.\n온몸에서 냄새가 난다.",
            "거대 돌진" or "Charge Collider" => "절불가시의 육탄 돌진에 치여 사망.\n두꺼비에게 뺑소니를 당했다.",
            "내리찍기" or "Stomp Collider" => "절불가시의 가시 찍기에 깔려 사망.",
            "바닥 가시" => "절불가시의 바닥 가시에 찔려 사망.",
            "산성 침" or "보스 침뱉기" => "절불가시의 산성 침에 녹아 사망.",
            "독 분사" => "절불가시의 독물 줄기에 휩쓸려 사망.",
            "독" => "절불가시의 독이 온몸에 퍼져 사망.",
            "뜨거운 도깨비 발판" => "달아오른 도깨비 발판에 데여 사망.\n앗 뜨거.",
            "궁극기 (가시 즉사)" or "궁극기" => "절불가시의 가시 폭발에 꿰여 사망.\n두꺼비를 화나게 해선 안 돼.",
            "몸통 박치기" => "절불가시의 새끼들에게 물려 사망.\n이런 거에 죽다니..",
            _ => causeOfDeath
        };
    }

    public IEnumerator FadeIn()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMusicEvent("Music.Death", gameObject.scene);
        }
        InitializeDeathEventSystem();
        GameCursorController.SetDeathScreenVisible(true);
        _restartTriggered = false;
        UpdateArtworkLayout();
        if (_causeOfDeathCanvasGroup != null)
            _causeOfDeathCanvasGroup.alpha = 0f;
        if (_causeOfDeathText != null)
            _causeOfDeathText.enabled = _causeOfDeathCanvasGroup != null;
        if (_deadRunButton != null)
            _deadRunButton.gameObject.SetActive(false);

        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = false;
        ApplyBackgroundReveal(0f);

        yield return FadeImageAlpha(_blackoutImage, 1f, _fadeInDuration);
        yield return PlayBackgroundRevealRoutine();
        if (_causeOfDeathText != null)
            _causeOfDeathText.enabled = true;
        yield return FadeCanvasGroup(_causeOfDeathCanvasGroup, 1f, _causeOfDeathFadeDuration);

        if (_deadRunButton != null)
            _deadRunButton.gameObject.SetActive(true);
        if (_deadRunButtonMotion != null)
        {
            if (_deadRunButtonMotion.RevealDuration > 0f)
                yield return new WaitForSecondsRealtime(_deadRunButtonMotion.RevealDuration);
            _deadRunButtonMotion.CompleteReveal();
        }
        _canvasGroup.interactable = true;
    }

    private IEnumerator PlayBackgroundRevealRoutine()
    {
        float elapsed = 0f;
        while (elapsed < _backgroundSpreadDuration)
        {
            float time = elapsed / _backgroundSpreadDuration;
            ApplyBackgroundReveal(_backgroundSpreadCurve != null
                ? Mathf.Clamp01(_backgroundSpreadCurve.Evaluate(time)) : time);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        ApplyBackgroundReveal(1f);
    }

    private void ApplyBackgroundReveal(float progress)
    {
        if (_backgroundMaterialInstance != null)
            _backgroundMaterialInstance.SetFloat(RevealProgressId, progress);
        if (_bgOverlayCanvasGroup != null)
        {
            _bgOverlayCanvasGroup.alpha = _backgroundMaterialInstance != null ? 1f : progress;
        }
        else if (_bgOverlayImage != null)
        {
            Color color = _bgOverlayImage.color;
            color.a = _backgroundMaterialInstance != null ? 1f : progress;
            _bgOverlayImage.color = color;
        }
    }

    public void TriggerRestart()
    {
        TriggerRestart(true);
    }

    private void HandleRunButtonClick()
    {
        TriggerRestart(false);
    }

    private void PlayRunButtonClickSound()
    {
        if (!_restartTriggered && _canvasGroup != null && _canvasGroup.interactable && AudioManager.Instance != null)
            AudioManager.Instance.PlayUiEvent("UI.Click", _runButtonClickClip, _runButtonClickVolume);
    }

    private void TriggerRestart(bool playClickSound)
    {
        if (_restartTriggered || _canvasGroup == null || !_canvasGroup.interactable)
            return;

        if (_deadRunButtonMotion != null)
            _deadRunButtonMotion.ApplyPressedVisual();
        if (playClickSound)
            PlayRunButtonClickSound();
        _restartTriggered = true;
        // Stop repeated clicks while StageManager begins its existing loading routine.
        _canvasGroup.interactable = false;
    }

    public IEnumerator WaitForRestartInput()
    {
        while (!_restartTriggered)
            yield return null;
    }

    public IEnumerator PlayLoadingRoutine()
    {
        DestroyOwnedEventSystem();
        GameCursorController.SetDeathScreenVisible(false);
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 0f;
        if (_loadingCanvasGroup != null)
        {
            _loadingCanvasGroup.alpha = 1f;
            _loadingCanvasGroup.blocksRaycasts = true;
            // 한 번만 재생하고, 씬 준비와 페이드아웃이 끝날 때까지 마지막 프레임을 유지한다.
            for (int frameIndex = 0; _loadingSprites != null && frameIndex < _loadingSprites.Length; frameIndex++)
            {
                if (_loadingImage != null)
                {
                    _loadingImage.sprite = _loadingSprites[frameIndex];
                }
                yield return new WaitForSecondsRealtime(_loadingFrameInterval);
            }
        }
    }

    public IEnumerator FadeOutLoading()
    {
        yield return FadeCanvasGroup(_loadingCanvasGroup, 0f, _fadeOutDuration);
        HideImmediate();
    }

    public void HideImmediate()
    {
        DestroyOwnedEventSystem();
        GameCursorController.SetDeathScreenVisible(false);
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
        if (_bgOverlayCanvasGroup != null)
            _bgOverlayCanvasGroup.alpha = 0f;
        if (_backgroundMaterialInstance != null)
            _backgroundMaterialInstance.SetFloat(RevealProgressId, 0f);
        if (_blackoutImage != null)
        {
            Color color = _blackoutImage.color;
            color.a = 0f;
            _blackoutImage.color = color;
        }
        if (_bgOverlayImage != null && _bgOverlayCanvasGroup == null)
        {
            Color color = _bgOverlayImage.color;
            color.a = 0f;
            _bgOverlayImage.color = color;
        }
        if (_loadingCanvasGroup != null)
        {
            _loadingCanvasGroup.alpha = 0f;
            _loadingCanvasGroup.interactable = false;
            _loadingCanvasGroup.blocksRaycasts = false;
        }
        if (_causeOfDeathCanvasGroup != null)
            _causeOfDeathCanvasGroup.alpha = 0f;
        if (_causeOfDeathText != null)
        {
            _causeOfDeathText.enabled = false;
            _causeOfDeathText.text = string.Empty;
        }
        if (_deadRunButton != null)
            _deadRunButton.gameObject.SetActive(false);
        _restartTriggered = false;
    }

    private void InitializeDeathEventSystem()
    {
        if ((EventSystem.current != null && EventSystem.current.isActiveAndEnabled) || _ownedEventSystem != null)
            return;

        _ownedEventSystem = new GameObject(DeathEventSystemObjectName,
            typeof(EventSystem), typeof(StandaloneInputModule));
        _ownedEventSystem.transform.SetParent(transform, false);
    }

    private void DestroyOwnedEventSystem()
    {
        if (_ownedEventSystem == null)
            return;
        Destroy(_ownedEventSystem);
        _ownedEventSystem = null;
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float targetAlpha, float duration)
    {
        if (group == null)
            yield break;
        float startAlpha = group.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }
        group.alpha = targetAlpha;
    }

    private static IEnumerator FadeImageAlpha(Image image, float targetAlpha, float duration)
    {
        if (image == null)
            yield break;
        Color startColor = image.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            Color color = startColor;
            color.a = Mathf.SmoothStep(startColor.a, targetAlpha, elapsed / duration);
            image.color = color;
            yield return null;
        }
        startColor.a = targetAlpha;
        image.color = startColor;
    }
}
