using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>격파 표시 이후 암전, 음악 목록, 스크롤 크레딧과 타이틀 복귀를 재생한다.</summary>
[DisallowMultipleComponent]
public class EndingCreditsController : MonoBehaviour
{
    [Serializable]
    private sealed class MusicCue
    {
        public AudioClip Clip;
        [Min(0f), Tooltip("곡의 재생 시작 위치 (초)")] public float StartOffset;
        [Min(0f), Tooltip("페이드 포함 재생 시간 (초). 0이면 시작 위치부터 곡 끝까지")]
        public float Duration;
        [Range(0f, 1f)] public float Volume = 1f;
        [Tooltip("재생 시간 동안 곡이 끝나면 반복")] public bool Loop;
        [Min(0f)] public float FadeIn = 1f;
        [Min(0f)] public float FadeOut = 1f;
    }

    [Header("엔딩 연결")]
    [SerializeField] private bool _playEnding = true;
    [SerializeField, Min(0f)] private float _blackoutDuration = 2f;
    [SerializeField, Min(0f)] private float _creditsDelay = 0.5f;
    [SerializeField] private int _canvasSortingOrder = 200;

    [Header("크레딧 이미지 (위에서 아래 순서)")]
    [Tooltip("등록된 이미지를 원본 비율로 나란히 이어서 스크롤합니다. 비우면 아래 문구를 사용합니다.")]
    [SerializeField] private Texture2D[] _creditImages = Array.Empty<Texture2D>();
    [SerializeField, Min(1f)] private float _imageWidth = 1152f;
    [SerializeField, Min(0f)] private float _imageSpacing = 160f;
    [SerializeField, Min(0f)] private float _imageSidePadding = 60f;

    [Header("대체 크레딧 문구 (이미지가 없을 때)")]
    [SerializeField, TextArea(10, 30)]
    private string _creditsText = "프로젝트 총괄 · 게임 기획\n하지호\n아트 리소스 제작 · 레벨 디자인\n프롤로그 · 스테이지 제작\n튜토리얼 · 사운드 제작\n\n\n플레이어 · 보스 시스템\n송재현\n플레이어 조작 · 콤보 · 패링 · 마무리 공격\n보스 전체 패턴 · 저장 및 리스폰\n\n\nUI · 애니메이션 · 전투 연출\n박욱영\nUI · 캐릭터, 몬스터 애니메이션\nVFX · 피격 피드백 · 환경 기믹\n\n\n몬스터 · 전투 시스템\n김상곤\n몬스터 AI · 공통구조 · 공격 및 피격\n오디오 시스템 · 레벨 배치\n\n\n플레이해 주셔서 감사합니다";
    [Tooltip("비우면 보스 연출의 글꼴 사용")]
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField, Min(1f)] private float _fontSize = 42f;
    [SerializeField] private Color _textColor = Color.white;
    [SerializeField, Min(1f)] private float _textWidth = 1300f;
    [SerializeField, Min(1f), Tooltip("1920 × 1080 기준 초당 이동 거리")]
    private float _scrollSpeed = 65f;
    [SerializeField, Min(0f)] private float _offscreenPadding = 80f;
    [SerializeField, Min(0f)] private float _buttonDelay = 0.5f;

    [Header("크레딧 배경 (선택)")]
    [SerializeField] private Sprite _backgroundSprite;
    [SerializeField, Range(0f, 1f)] private float _backgroundOpacity = 0.25f;

    [Header("음악 목록 (위에서 아래 순서)")]
    [SerializeField] private List<MusicCue> _musicPlaylist = new List<MusicCue>();
    [Tooltip("마지막 곡이 끝나면 첫 곡부터 반복. 버튼이 표시된 뒤에도 재생")]
    [SerializeField] private bool _repeatMusicPlaylist = true;
    [SerializeField, Min(0f)] private float _previousMusicFadeOut = 1f;

    [Header("타이틀 복귀")]
    [SerializeField] private string _returnButtonText = "엔터를 눌러 타이틀로 돌아가기";
    [SerializeField] private string _titleSceneName = "Test_TitleScene";
    [SerializeField] private TMP_FontAsset _returnButtonFont;
    [SerializeField] private Vector2 _returnButtonSize = new Vector2(1200f, 180f);
    [SerializeField, Min(1f)] private float _returnButtonFontSize = 44f;
    [SerializeField, Min(0f)] private float _returnButtonFadeIn = 0.6f;

    private GameObject _canvasObject;
    private RectTransform _viewport;
    private RectTransform _creditsContent;
    private TMP_FontAsset _resolvedFont;
    private Image _blackout;
    private Image _background;
    private GameObject _returnPrompt;
    private CanvasGroup _returnPromptGroup;
    private AudioSource _musicSource;
    private bool _isPlaying;
    private bool _creditsFinished;
    private bool _isReturning;
    private bool _previousCursorVisible;
    private CursorLockMode _previousCursorLock;

    public bool CanPlay => _playEnding && isActiveAndEnabled;

    private void Update()
    {
        if (!_creditsFinished || _isReturning) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ReturnToTitle();
        }
    }

    public IEnumerator PlayEnding(TMP_FontAsset fallbackFont)
    {
        if (!CanPlay || _isPlaying) yield break;
        if (!PauseController.TryPauseForEnding(this)) yield break;

        _isPlaying = true;
        _previousCursorVisible = Cursor.visible;
        _previousCursorLock = Cursor.lockState;
        BuildUI(_font != null ? _font : fallbackFont);
        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.spatialBlend = 0f;
        _musicSource.ignoreListenerPause = true;
        if (AudioManager.Instance != null) AudioManager.Instance.StopBgm(_previousMusicFadeOut);
        StartCoroutine(PlayMusicPlaylist());

        float elapsed = 0f;
        while (elapsed < _blackoutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _blackout.color = new Color(0f, 0f, 0f, Mathf.Clamp01(elapsed / _blackoutDuration));
            yield return null;
        }
        _blackout.color = Color.black;
        if (_background != null) _background.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(_creditsDelay);

        Canvas.ForceUpdateCanvases();
        float height = BuildCreditsContent();
        float position = -_offscreenPadding;
        _creditsContent.anchoredPosition = new Vector2(0f, position);
        _creditsContent.gameObject.SetActive(true);
        while (position < _viewport.rect.height + height + _offscreenPadding)
        {
            position += Mathf.Max(1f, _scrollSpeed) * Time.unscaledDeltaTime;
            _creditsContent.anchoredPosition = new Vector2(0f, position);
            yield return null;
        }

        _creditsContent.gameObject.SetActive(false);
        yield return new WaitForSecondsRealtime(_buttonDelay);
        _returnPrompt.SetActive(true);
        yield return FadeInReturnPrompt();
        _creditsFinished = true;
    }

    private IEnumerator PlayMusicPlaylist()
    {
        do
        {
            bool playedAny = false;
            foreach (MusicCue cue in _musicPlaylist)
            {
                if (cue == null || cue.Clip == null) continue;
                playedAny = true;
                float offset = Mathf.Clamp(cue.StartOffset, 0f, Mathf.Max(0f, cue.Clip.length - 0.01f));
                float duration = cue.Duration > 0f ? cue.Duration : cue.Clip.length - offset;
                float fadeIn = Mathf.Min(cue.FadeIn, duration * 0.5f);
                float fadeOut = Mathf.Min(cue.FadeOut, duration * 0.5f);
                _musicSource.clip = cue.Clip;
                _musicSource.loop = cue.Loop;
                _musicSource.time = offset;
                _musicSource.volume = 0f;
                _musicSource.Play();
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    float envelopeIn = fadeIn > 0f ? Mathf.Clamp01(elapsed / fadeIn) : 1f;
                    float envelopeOut = fadeOut > 0f ? Mathf.Clamp01((duration - elapsed) / fadeOut) : 1f;
                    AudioManager manager = AudioManager.Instance;
                    float volume = manager != null ? (manager.IsBgmMuted() ? 0f : manager.GetBgmVolume()) : 1f;
                    _musicSource.volume = cue.Volume * volume * Mathf.Min(envelopeIn, envelopeOut);
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                _musicSource.Stop();
            }
            if (!playedAny) break;
        }
        while (_repeatMusicPlaylist);
    }

    public void ReturnToTitle()
    {
        if (!_creditsFinished || _isReturning) return;
        if (string.IsNullOrWhiteSpace(_titleSceneName) || !Application.CanStreamedLevelBeLoaded(_titleSceneName))
        {
            Debug.LogWarning($"타이틀 씬 '{_titleSceneName}'이(가) 빌드 설정에 없습니다.", this);
            return;
        }
        _isReturning = true;
        _returnPrompt.SetActive(false);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        PauseController.ResumeFromEnding(this);
        GameCursorController.SetEndingVisible(false);
        Time.timeScale = 1f;
        SceneManager.LoadScene(_titleSceneName);
    }

    private void BuildUI(TMP_FontAsset font)
    {
        _canvasObject = new GameObject("EndingCreditsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvasObject.transform.SetParent(transform, false);
        Canvas canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = _canvasSortingOrder;
        CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _blackout = CreateImage("Blackout", _canvasObject.transform, new Color(0f, 0f, 0f, 0f));
        _blackout.raycastTarget = true;
        Stretch(_blackout.rectTransform);
        if (_backgroundSprite != null)
        {
            _background = CreateImage("Background", _canvasObject.transform, new Color(1f, 1f, 1f, _backgroundOpacity));
            _background.sprite = _backgroundSprite;
            _background.preserveAspect = true;
            Stretch(_background.rectTransform);
            _background.gameObject.SetActive(false);
        }

        var viewportObject = new GameObject("CreditsViewport", typeof(RectTransform), typeof(RectMask2D));
        viewportObject.transform.SetParent(_canvasObject.transform, false);
        _viewport = (RectTransform)viewportObject.transform;
        Stretch(_viewport);
        _resolvedFont = font;
        var contentObject = new GameObject("CreditsContent", typeof(RectTransform));
        contentObject.transform.SetParent(_viewport, false);
        _creditsContent = (RectTransform)contentObject.transform;
        _creditsContent.anchorMin = _creditsContent.anchorMax = new Vector2(0.5f, 0f);
        _creditsContent.pivot = new Vector2(0.5f, 1f);
        _creditsContent.gameObject.SetActive(false);

        _returnPrompt = new GameObject("ReturnToTitlePrompt", typeof(RectTransform), typeof(CanvasGroup));
        _returnPrompt.transform.SetParent(_canvasObject.transform, false);
        var promptRect = (RectTransform)_returnPrompt.transform;
        promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.sizeDelta = _returnButtonSize;
        _returnPromptGroup = _returnPrompt.GetComponent<CanvasGroup>();
        _returnPromptGroup.alpha = 0f;
        _returnPromptGroup.interactable = false;
        _returnPromptGroup.blocksRaycasts = false;
        TMP_Text label = CreateText("Label", promptRect, _returnButtonFont != null ? _returnButtonFont : font,
            _returnButtonText, _returnButtonFontSize);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(50f, 25f);
        label.rectTransform.offsetMax = new Vector2(-50f, -25f);
        label.color = Color.white;
        label.enableAutoSizing = true;
        label.fontSizeMin = _returnButtonFontSize * 0.65f;
        label.fontSizeMax = _returnButtonFontSize * 1.06f;
        _returnPrompt.SetActive(false);
    }

    private IEnumerator FadeInReturnPrompt()
    {
        float elapsed = 0f;
        while (elapsed < _returnButtonFadeIn)
        {
            elapsed += Time.unscaledDeltaTime;
            _returnPromptGroup.alpha = Mathf.SmoothStep(0f, 1f, elapsed / _returnButtonFadeIn);
            yield return null;
        }
        _returnPromptGroup.alpha = 1f;
    }

    private float BuildCreditsContent()
    {
        float availableWidth = Mathf.Max(1f, _viewport.rect.width - _imageSidePadding * 2f);
        float totalHeight = 0f;
        float contentWidth = 0f;
        int imageCount = 0;
        if (_creditImages != null)
        {
            foreach (Texture2D texture in _creditImages)
            {
                if (texture == null) continue;

                if (imageCount > 0) totalHeight += _imageSpacing;
                float aspect = (float)texture.width / Mathf.Max(1, texture.height);
                // 작은 화면에서도 카드 전체가 보이도록 높이도 화면의 90% 이내로 제한한다.
                float width = Mathf.Min(_imageWidth, availableWidth, _viewport.rect.height * 0.9f * aspect);
                float height = width / aspect;
                var imageObject = new GameObject($"CreditImage_{imageCount + 1}", typeof(RectTransform), typeof(RawImage));
                imageObject.transform.SetParent(_creditsContent, false);
                RawImage image = imageObject.GetComponent<RawImage>();
                image.texture = texture;
                image.color = Color.white;
                image.raycastTarget = false;
                RectTransform rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(width, height);
                rect.anchoredPosition = new Vector2(0f, -totalHeight);
                totalHeight += height;
                contentWidth = Mathf.Max(contentWidth, width);
                imageCount++;
            }
        }

        if (imageCount == 0)
        {
            contentWidth = Mathf.Max(1f, Mathf.Min(_textWidth, _viewport.rect.width - 80f));
            TMP_Text credits = CreateText("CreditsText", _creditsContent, _resolvedFont, _creditsText, _fontSize);
            credits.alignment = TextAlignmentOptions.Top;
            totalHeight = Mathf.Max(_fontSize, credits.GetPreferredValues(_creditsText, contentWidth, Mathf.Infinity).y);
            Stretch(credits.rectTransform);
        }

        _creditsContent.sizeDelta = new Vector2(contentWidth, totalHeight);
        return totalHeight;
    }

    private TMP_Text CreateText(string objectName, Transform parent, TMP_FontAsset font, string text, float size)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text label = textObject.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = _textColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void OnDisable()
    {
        if (!_isPlaying) return;
        StopAllCoroutines();
        if (_musicSource != null)
        {
            _musicSource.Stop();
            Destroy(_musicSource);
        }
        if (_canvasObject != null) Destroy(_canvasObject);
        PauseController.ResumeFromEnding(this);
        GameCursorController.SetEndingVisible(false);
        if (!_isReturning)
        {
            Cursor.visible = _previousCursorVisible;
            Cursor.lockState = _previousCursorLock;
        }
        _isPlaying = false;
        _creditsFinished = false;
        _isReturning = false;
    }
}
