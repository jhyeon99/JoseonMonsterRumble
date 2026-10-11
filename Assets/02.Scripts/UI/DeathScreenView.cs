using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public sealed class DeathScreenView : MonoBehaviour
{
    private const float MinimumScrollDuration = 0.01f;
    private const string DeathEventSystemObjectName = "DeathUIEventSystem";
    private static readonly int RevealProgressId = Shader.PropertyToID("_RevealProgress");
    private static readonly int BackgroundRectId = Shader.PropertyToID("_BackgroundRect");
    [Header("페이드 설정")]
    [Tooltip("게임 화면의 암전 시간")]
    [SerializeField, Min(0f)] private float _fadeInDuration = 1.5f;
    [SerializeField, Min(0f)] private float _fadeOutDuration = 0.5f;
    [FormerlySerializedAs("_backgroundFadeInDuration")]
    [SerializeField, Min(0f)] private float _backgroundSpreadDuration = 2.6f;

    [Header("배경")]
    [SerializeField] private Image _blackoutImage;
    [SerializeField] private CanvasGroup _bgOverlayCanvasGroup;
    [SerializeField] private Image _bgOverlayImage;
    [SerializeField] private Material _backgroundInkMaterial;
    [Tooltip("고정된 브러시 결을 따라 그림이 짙어지는 진행률. 시작부터 완만하게 감속한다.")]
    [SerializeField]
    private AnimationCurve _backgroundSpreadCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 1.5f, 1.5f), new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private Image _scrollShadowImage;
    [SerializeField, Range(0f, 1f)] private float _scrollShadowOpacity = 0.32f;
    [SerializeField] private Vector2 _scrollShadowPadding = new Vector2(160f, 140f);
    [SerializeField] private Vector2 _scrollShadowOffset = new Vector2(10f, -14f);

    [Header("스크롤 연출")]
    [SerializeField] private Image _deadScrollImage;
    [SerializeField] private Sprite[] _deadScrollSprites;
    [Tooltip("암전 시작 후 두루마리를 보여주기까지의 시간")]
    [SerializeField, Min(0f)] private float _scrollStartDelay = 0.6f;
    [SerializeField, Min(0.1f)] private float _scrollDuration = 1.8f;
    [SerializeField] private RectTransform _scrollMaskRect;
    [SerializeField, Min(0f)] private float _targetScrollHeight = 400f;
    [SerializeField, Min(0f)] private float _maxScrollWidth = 600f;
    [Tooltip("좌우 말린 부분의 합계 폭 / 스프라이트 높이. 펼쳐진 종이만 글씨 노출에 사용한다.")]
    [SerializeField, Min(0f)] private float _scrollRollWidthRatio = 0.27f;
    [SerializeField] private AnimationCurve _scrollUnfoldCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("펼침 초반의 세로 이동. 정착 이후와 마지막 값은 0으로 유지한다.")]
    [SerializeField]
    private AnimationCurve _scrollArrivalCurve = new AnimationCurve(
        new Keyframe(0f, 16f), new Keyframe(0.45f, -2f),
        new Keyframe(0.65f, 0f), new Keyframe(1f, 0f));

    [Header("사망 원인")]
    [SerializeField] private TextMeshProUGUI _causeOfDeathText;

    [Header("버튼")]
    [SerializeField] private Button _deadRunButton;
    [SerializeField] private DeathRunButtonMotion _deadRunButtonMotion;
    [SerializeField] private AudioClip _runButtonClickClip;
    [SerializeField, Range(0f, 1f)] private float _runButtonClickVolume = 1f;

    [Header("로딩 화면")]
    [SerializeField] private CanvasGroup _loadingCanvasGroup;
    [SerializeField] private Image _loadingImage;
    [SerializeField] private Sprite[] _loadingSprites;
    [SerializeField, Min(0.01f)] private float _loadingFrameInterval = 1.0f;

    public float MinLoadingDuration => _loadingSprites != null && _loadingSprites.Length > 0
        ? _loadingSprites.Length * _loadingFrameInterval
        : 3f;

    private CanvasGroup _canvasGroup;
    private bool _restartTriggered;
    private Coroutine _scrollAnimCoroutine;
    private Vector2 _scrollRestPosition;
    private Vector2 _maskRestPosition;
    private GameObject _ownedEventSystem;
    private Material _backgroundMaterialInstance;
    private Material _originalBackgroundMaterial;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        InitializeBackgroundInkMaterial();
        if (_deadScrollImage != null)
        {
            _scrollRestPosition = _deadScrollImage.rectTransform.anchoredPosition;
        }
        if (_scrollMaskRect != null)
        {
            _maskRestPosition = _scrollMaskRect.anchoredPosition;
        }
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
        {
            canvas.worldCamera = Camera.main;
        }

        if (_deadRunButton != null)
        {
            _deadRunButton.onClick.AddListener(HandleRunButtonClick);
        }
        if (_deadRunButtonMotion != null)
        {
            _deadRunButtonMotion.Pressed += PlayRunButtonClickSound;
        }

        HideImmediate();
    }

    private void OnDestroy()
    {
        if (_backgroundMaterialInstance != null)
        {
            if (_bgOverlayImage != null)
            {
                _bgOverlayImage.material = _originalBackgroundMaterial;
            }
            Destroy(_backgroundMaterialInstance);
        }
        DestroyOwnedEventSystem();
        GameCursorController.SetDeathScreenVisible(false);
        if (_deadRunButton != null)
        {
            _deadRunButton.onClick.RemoveListener(HandleRunButtonClick);
        }
        if (_deadRunButtonMotion != null)
        {
            _deadRunButtonMotion.Pressed -= PlayRunButtonClickSound;
        }
    }

    private void Update()
    {
        if (_canvasGroup != null && _canvasGroup.interactable && !_restartTriggered)
        {
            if (Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Escape))
            {
                TriggerRestart();
            }
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
        if (!_restartTriggered && _canvasGroup != null && _canvasGroup.interactable)
        {
            AudioManager.Instance.PlayUiEvent("UI.Click", _runButtonClickClip, _runButtonClickVolume);
        }
    }

    private void TriggerRestart(bool playClickSound)
    {
        if (_restartTriggered || _canvasGroup == null || !_canvasGroup.interactable)
        {
            return;
        }
        if (_deadRunButtonMotion != null)
        {
            _deadRunButtonMotion.ApplyPressedVisual();
        }
        if (playClickSound)
        {
            PlayRunButtonClickSound();
        }
        _restartTriggered = true;
    }

    public void SetCauseOfDeath(string text)
    {
        if (_causeOfDeathText != null)
        {
            _causeOfDeathText.text = GetCauseOfDeathMessage(text);
            // Prepare dynamic glyphs and the text mesh while the death UI is hidden.
            // The scroll mask still controls when the already-prepared text appears.
            _causeOfDeathText.ForceMeshUpdate(ignoreActiveState: true);
        }
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
            _ => causeOfDeath
        };
    }

    public IEnumerator FadeIn()
    {
        AudioManager.Instance.PlayMusicEvent("Music.Death", gameObject.scene);
        InitializeDeathEventSystem();
        GameCursorController.SetDeathScreenVisible(true);
        _restartTriggered = false;

        float baseHeight = _targetScrollHeight > 0f ? _targetScrollHeight : 400f;

        if (_deadScrollImage != null)
        {
            _deadScrollImage.preserveAspect = true;
            if (_deadScrollSprites != null && _deadScrollSprites.Length > 0)
            {
                Sprite firstSprite = _deadScrollSprites[0];
                _deadScrollImage.sprite = firstSprite;
                if (firstSprite != null && firstSprite.rect.height > 0f)
                {
                    float aspect = firstSprite.rect.width / firstSprite.rect.height;
                    _deadScrollImage.rectTransform.sizeDelta = new Vector2(baseHeight * aspect, baseHeight);
                }
            }
            _deadScrollImage.gameObject.SetActive(false);
        }

        if (_scrollMaskRect != null)
        {
            _scrollMaskRect.sizeDelta = new Vector2(0f, baseHeight);
            _scrollMaskRect.gameObject.SetActive(false);
        }
        if (_causeOfDeathText != null)
        {
            _causeOfDeathText.enabled = false;
        }

        if (_deadRunButton != null)
        {
            _deadRunButton.gameObject.SetActive(false);
        }

        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = false;

        if (_bgOverlayCanvasGroup != null)
        {
            _bgOverlayCanvasGroup.alpha = 0f;
        }
        else if (_bgOverlayImage != null)
        {
            Color backgroundColor = _bgOverlayImage.color;
            backgroundColor.a = 0f;
            _bgOverlayImage.color = backgroundColor;
        }

        if (_scrollAnimCoroutine != null)
        {
            StopCoroutine(_scrollAnimCoroutine);
        }
        _scrollAnimCoroutine = StartCoroutine(PlayDelayedScrollAnimationRoutine());
        yield return FadeImageAlpha(_blackoutImage, 1f, _fadeInDuration);
        yield return _scrollAnimCoroutine;
        _scrollAnimCoroutine = null;

        yield return PlayBackgroundRevealRoutine();

        _canvasGroup.interactable = true;
    }

    private IEnumerator PlayDelayedScrollAnimationRoutine()
    {
        if (_scrollStartDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(_scrollStartDelay);
        }
        yield return PlayScrollAnimationRoutine();
    }

    private IEnumerator PlayScrollAnimationRoutine()
    {
        if (_deadScrollImage == null || _deadScrollSprites == null || _deadScrollSprites.Length == 0)
        {
            yield break;
        }

        _deadScrollImage.preserveAspect = true;
        _deadScrollImage.gameObject.SetActive(true);
        if (_scrollMaskRect != null)
        {
            _scrollMaskRect.gameObject.SetActive(true);
        }

        float baseHeight = _targetScrollHeight > 0f ? _targetScrollHeight : 400f;
        float duration = Mathf.Max(MinimumScrollDuration, _scrollDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float unfoldProgress = _scrollUnfoldCurve != null
                ? Mathf.Clamp01(_scrollUnfoldCurve.Evaluate(t))
                : t;

            int spriteIndex = Mathf.Clamp(
                (int)(unfoldProgress * _deadScrollSprites.Length),
                0,
                _deadScrollSprites.Length - 1
            );
            Sprite currentSprite = _deadScrollSprites[spriteIndex];
            _deadScrollImage.sprite = currentSprite;

            if (currentSprite != null && currentSprite.rect.height > 0f)
            {
                float aspect = currentSprite.rect.width / currentSprite.rect.height;
                _deadScrollImage.rectTransform.sizeDelta = new Vector2(baseHeight * aspect, baseHeight);
            }

            ApplyScrollTextMask(currentSprite, baseHeight);
            float offset = _scrollArrivalCurve != null ? _scrollArrivalCurve.Evaluate(t) : 0f;
            SetScrollVerticalOffset(offset);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Sprite finalSprite = _deadScrollSprites[_deadScrollSprites.Length - 1];
        _deadScrollImage.sprite = finalSprite;
        if (finalSprite != null && finalSprite.rect.height > 0f)
        {
            float finalAspect = finalSprite.rect.width / finalSprite.rect.height;
            _deadScrollImage.rectTransform.sizeDelta = new Vector2(baseHeight * finalAspect, baseHeight);
        }

        ApplyScrollTextMask(finalSprite, baseHeight);
    }

    private void SetScrollVerticalOffset(float offset)
    {
        if (_deadScrollImage != null)
        {
            _deadScrollImage.rectTransform.anchoredPosition = _scrollRestPosition + Vector2.up * offset;
        }
        if (_scrollMaskRect != null)
        {
            _scrollMaskRect.anchoredPosition = _maskRestPosition + Vector2.up * offset;
        }
    }

    private IEnumerator PlayBackgroundRevealRoutine()
    {
        SetScrollVerticalOffset(0f);
        UpdateBackgroundInkCoordinates();
        ApplyBackgroundReveal(0f);
        if (_scrollShadowImage != null && _deadScrollImage != null)
        {
            _scrollShadowImage.rectTransform.anchoredPosition = _scrollRestPosition + _scrollShadowOffset;
            _scrollShadowImage.rectTransform.sizeDelta = _deadScrollImage.rectTransform.sizeDelta + _scrollShadowPadding;
            Color shadowColor = _scrollShadowImage.color;
            shadowColor.a = 0f;
            _scrollShadowImage.color = shadowColor;
            _scrollShadowImage.gameObject.SetActive(true);
        }
        float elapsed = 0f;
        float buttonRevealDuration = _deadRunButtonMotion != null ? _deadRunButtonMotion.RevealDuration : 0f;
        while (elapsed < _backgroundSpreadDuration)
        {
            float time = elapsed / _backgroundSpreadDuration;
            float progress = _backgroundSpreadCurve != null
                ? Mathf.Clamp01(_backgroundSpreadCurve.Evaluate(time)) : time;
            ApplyBackgroundReveal(progress);
            if (elapsed >= _backgroundSpreadDuration - buttonRevealDuration && _deadRunButton != null)
            {
                _deadRunButton.gameObject.SetActive(true);
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        ApplyBackgroundReveal(1f);
        if (_deadRunButton != null)
        {
            _deadRunButton.gameObject.SetActive(true);
        }
        if (_deadRunButtonMotion != null)
        {
            _deadRunButtonMotion.CompleteReveal();
        }
    }

    private void ApplyBackgroundReveal(float progress)
    {
        if (_backgroundMaterialInstance != null)
        {
            _backgroundMaterialInstance.SetFloat(RevealProgressId, progress);
        }
        if (_scrollShadowImage != null)
        {
            Color shadowColor = _scrollShadowImage.color;
            shadowColor.a = _scrollShadowOpacity * progress;
            _scrollShadowImage.color = shadowColor;
        }
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

    private void InitializeBackgroundInkMaterial()
    {
        if (_bgOverlayImage == null || _backgroundInkMaterial == null)
        {
            Debug.LogWarning("죽음 UI: 배경 Image 또는 먹 확산 Material 참조가 없습니다.", this);
            return;
        }
        _originalBackgroundMaterial = _bgOverlayImage.material;
        _backgroundMaterialInstance = new Material(_backgroundInkMaterial);
        _bgOverlayImage.material = _backgroundMaterialInstance;
    }

    private void UpdateBackgroundInkCoordinates()
    {
        if (_backgroundMaterialInstance == null || _bgOverlayImage == null)
        {
            return;
        }
        RectTransform backgroundRect = _bgOverlayImage.rectTransform;
        Rect rect = backgroundRect.rect;
        if (rect.width <= 0f || rect.height <= 0f)
        {
            Debug.LogWarning("죽음 UI: 먹 확산 배경의 크기가 0입니다.", this);
            return;
        }
        _backgroundMaterialInstance.SetVector(BackgroundRectId,
            new Vector4(rect.xMin, rect.yMin, rect.width, rect.height));
    }

    private void ApplyScrollTextMask(Sprite sprite, float displayHeight)
    {
        float paperWidth = CalculateScrollTextWidth(sprite, displayHeight);
        if (_scrollMaskRect != null)
        {
            _scrollMaskRect.sizeDelta = new Vector2(paperWidth, displayHeight);
        }
        if (_causeOfDeathText != null)
        {
            _causeOfDeathText.enabled = _scrollMaskRect != null && paperWidth > 0f;
        }
    }

    private float CalculateScrollTextWidth(Sprite sprite, float displayHeight)
    {
        if (sprite == null || sprite.rect.height <= 0f)
        {
            return 0f;
        }

        // The closed frame overlaps the two rolls, so its width is not their combined width.
        float rollWidth = sprite.rect.height * _scrollRollWidthRatio;
        float paperWidth = Mathf.Max(0f, sprite.rect.width - rollWidth) * displayHeight / sprite.rect.height;
        return Mathf.Min(_maxScrollWidth, paperWidth);
    }

    public IEnumerator WaitForRestartInput()
    {
        while (!_restartTriggered)
        {
            yield return null;
        }
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

            int frameIndex = 0;
            while (true)
            {
                if (_loadingSprites != null && _loadingSprites.Length > 0 && _loadingImage != null)
                {
                    _loadingImage.sprite = _loadingSprites[frameIndex % _loadingSprites.Length];
                    frameIndex++;
                }

                yield return new WaitForSecondsRealtime(_loadingFrameInterval);
            }
        }
    }

    public IEnumerator FadeOutLoading()
    {
        if (_loadingCanvasGroup != null)
        {
            yield return FadeCanvasGroup(_loadingCanvasGroup, 0f, _fadeOutDuration);
        }

        HideImmediate();
    }

    public void HideImmediate()
    {
        DestroyOwnedEventSystem();
        GameCursorController.SetDeathScreenVisible(false);
        if (_scrollShadowImage != null)
        {
            _scrollShadowImage.gameObject.SetActive(false);
        }
        SetScrollVerticalOffset(0f);
        ApplyBackgroundReveal(1f);
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        if (_bgOverlayCanvasGroup != null)
        {
            _bgOverlayCanvasGroup.alpha = 0f;
        }

        if (_blackoutImage != null)
        {
            Color blackoutColor = _blackoutImage.color;
            blackoutColor.a = 0f;
            _blackoutImage.color = blackoutColor;
        }

        if (_bgOverlayImage != null && _bgOverlayCanvasGroup == null)
        {
            Color c = _bgOverlayImage.color;
            c.a = 0f;
            _bgOverlayImage.color = c;
        }

        if (_loadingCanvasGroup != null)
        {
            _loadingCanvasGroup.alpha = 0f;
            _loadingCanvasGroup.interactable = false;
            _loadingCanvasGroup.blocksRaycasts = false;
        }

        if (_scrollAnimCoroutine != null)
        {
            StopCoroutine(_scrollAnimCoroutine);
            _scrollAnimCoroutine = null;
        }

        float baseHeight = _targetScrollHeight > 0f ? _targetScrollHeight : 400f;

        if (_deadScrollImage != null)
        {
            _deadScrollImage.gameObject.SetActive(false);
            _deadScrollImage.preserveAspect = true;
            if (_deadScrollSprites != null && _deadScrollSprites.Length > 0)
            {
                Sprite firstSprite = _deadScrollSprites[0];
                _deadScrollImage.sprite = firstSprite;
                if (firstSprite != null && firstSprite.rect.height > 0f)
                {
                    float aspect = firstSprite.rect.width / firstSprite.rect.height;
                    _deadScrollImage.rectTransform.sizeDelta = new Vector2(baseHeight * aspect, baseHeight);
                }
            }
        }

        if (_scrollMaskRect != null)
        {
            _scrollMaskRect.gameObject.SetActive(false);
            _scrollMaskRect.sizeDelta = new Vector2(0f, baseHeight);
        }

        if (_deadRunButton != null)
        {
            _deadRunButton.gameObject.SetActive(false);
        }

        if (_causeOfDeathText != null)
        {
            _causeOfDeathText.enabled = false;
            _causeOfDeathText.text = string.Empty;
        }

        _restartTriggered = false;
    }

    private void InitializeDeathEventSystem()
    {
        if (EventSystem.current != null && EventSystem.current.isActiveAndEnabled)
        {
            return;
        }

        if (_ownedEventSystem != null)
        {
            return;
        }

        _ownedEventSystem = new GameObject(DeathEventSystemObjectName,
            typeof(EventSystem), typeof(StandaloneInputModule));
        _ownedEventSystem.transform.SetParent(transform, false);
        Debug.LogWarning("죽음 UI: 활성 EventSystem이 없어 전용 입력 처리를 생성했습니다.", this);
    }

    private void DestroyOwnedEventSystem()
    {
        if (_ownedEventSystem == null)
        {
            return;
        }

        Destroy(_ownedEventSystem);
        _ownedEventSystem = null;
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float targetAlpha, float duration)
    {
        if (group == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            group.alpha = targetAlpha;
            yield break;
        }

        float startAlpha = group.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
            yield return null;
        }

        group.alpha = targetAlpha;
    }

    private static IEnumerator FadeImageAlpha(Image image, float targetAlpha, float duration)
    {
        if (image == null)
        {
            yield break;
        }

        Color startColor = image.color;
        if (duration <= 0f)
        {
            startColor.a = targetAlpha;
            image.color = startColor;
            yield break;
        }

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            Color c = startColor;
            c.a = Mathf.SmoothStep(startColor.a, targetAlpha, elapsedTime / duration);
            image.color = c;
            yield return null;
        }

        startColor.a = targetAlpha;
        image.color = startColor;
    }
}
