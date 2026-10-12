using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 보스전 연출 감독. 씬에 빈 오브젝트로 하나 두면 보스 이벤트에 맞춰 자동 재생 (보스 자식에 두지 않기: 처치 후 보스가 꺼짐).
/// - 등장: 플레이어 조작 잠금 + 무적, 레터박스, 카메라가 보스 쪽으로 이동 + 줌, 보스 이름 타이틀 → 원래대로
///   (첫 조우 때만. 리스폰으로 씬을 다시 불러오면 생략하고 바로 전투)
/// - 2페이즈(하울링): 조작 잠금 + 무적, 화면 플래시, 보스 분노 색조 + 화면 색조, 음악 전환
/// - 처치(마무리 공격 적중 후): 조작 잠금 + 무적, 흰 화면 플래시 + 슬로모션, 레터박스, 음악 페이드 아웃
///   → 처치 연출이 끝나면 클리어 문구 표시 후 해제 (On Defeat Sequence Finished 이벤트로 스테이지 클리어 연결)
/// 레터박스 / 타이틀 / 플래시 / 화면 색조 UI는 실행 시 자동으로 만듦 (따로 만들 필요 없음).
/// </summary>
public class BossCinematicDirector : MonoBehaviour
{
    [Header("참조 (비우면 자동으로 찾음)")]
    [SerializeField]
    private Boss _boss;

    [SerializeField]
    private CameraFollow _cameraFollow;

    [Tooltip("타이틀 글꼴 (한글 지원 폰트). 비우면 보스 체력바 글꼴 사용")]
    [SerializeField]
    private TMP_FontAsset _titleFont;

    [Header("등장")]
    [SerializeField]
    private bool _playIntro = true;

    [Tooltip("켜면 첫 조우 때만 등장 연출(카메라 이동 / 레터박스 / 이름)을 보여주고, 리스폰(씬 다시 불러오기) 후에는 생략. 게임을 다시 실행하면 초기화")]
    [SerializeField]
    private bool _playIntroOnlyOnce = true;

    [Tooltip("켜면 리스폰으로 등장 연출을 생략할 때 보스의 등장 하울링도 건너뛰고 바로 전투 시작")]
    [SerializeField]
    private bool _skipIntroHowlOnRespawn = true;

    // 이번 실행에서 등장 연출을 이미 본 보스 (씬 이름 + 보스 이름). 씬을 다시 불러와도 유지
    private static readonly HashSet<string> SeenIntros = new HashSet<string>();

    /// <summary>이번 실행에서 이 씬의 보스 등장 연출을 이미 봤는지 (리스폰 로딩 생략 판단용)</summary>
    public static bool HasSeenIntroInScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return false;
        }

        string prefix = sceneName + "/";
        foreach (string key in SeenIntros)
        {
            if (key.StartsWith(prefix))
            {
                return true;
            }
        }

        return false;
    }

    // 에디터에서 도메인 리로드를 끄고 플레이해도 플레이할 때마다 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSeenIntros()
    {
        SeenIntros.Clear();
    }

    [Tooltip("카메라가 보스 쪽으로 이동하는 부드러움 (작을수록 빠름)")]
    [SerializeField, Min(0f)]
    private float _introPanSmoothTime = 0.35f;

    [Tooltip("카메라가 보스를 비추는 시간 (초). 이 동안 타이틀이 나왔다 사라짐")]
    [SerializeField, Min(0f)]
    private float _introFocusDuration = 2.6f;

    [Tooltip("플레이어 쪽으로 돌아가는 부드러움")]
    [SerializeField, Min(0f)]
    private float _introReturnSmoothTime = 0.3f;

    [Tooltip("카메라가 돌아간 뒤 조작을 풀기까지 대기 (초)")]
    [SerializeField, Min(0f)]
    private float _introReturnDuration = 0.6f;

    [Tooltip("보스를 비출 때 줌 (Hold Time은 자동으로 맞춤)")]
    [SerializeField]
    private CameraZoomSettings _introZoom = new CameraZoomSettings(0.15f, 0.6f, 0f, 0.5f);

    [Header("타이틀")]
    [Tooltip("비우면 보스 이름 사용")]
    [SerializeField]
    private string _title = "";

    [Tooltip("이름 위에 작게 표시 (비우면 표시 안 함)")]
    [SerializeField]
    private string _subtitle = "";

    [Tooltip("카메라 이동 시작 후 타이틀이 나오기까지 (초)")]
    [SerializeField, Min(0f)]
    private float _titleDelay = 0.5f;

    [SerializeField, Min(0.01f)]
    private float _titleFadeIn = 0.35f;

    [SerializeField, Min(0f)]
    private float _titleHold = 1.3f;

    [SerializeField, Min(0.01f)]
    private float _titleFadeOut = 0.4f;

    [SerializeField, Min(1f)]
    private float _titleFontSize = 84f;

    [SerializeField, Min(1f)]
    private float _subtitleFontSize = 34f;

    [Tooltip("타이틀 세로 위치 (화면 아래 0 ~ 위 1)")]
    [SerializeField, Range(0f, 1f)]
    private float _titleHeight = 0.3f;

    [SerializeField]
    private Color _titleColor = Color.white;

    [Header("레터박스")]
    [Tooltip("위 / 아래 검은 띠 두께 (화면 높이 대비)")]
    [SerializeField, Range(0f, 0.3f)]
    private float _letterboxHeight = 0.11f;

    [SerializeField, Min(0.01f)]
    private float _letterboxTime = 0.35f;

    [SerializeField]
    private bool _letterboxOnHowl = true;

    [SerializeField]
    private bool _letterboxOnDefeat = true;

    [Header("2페이즈 (하울링)")]
    [SerializeField]
    private Color _flashColor = Color.white;

    [SerializeField, Range(0f, 1f)]
    private float _flashPeakAlpha = 0.85f;

    [SerializeField, Min(0.01f)]
    private float _flashDuration = 0.45f;

    [Tooltip("보스 스프라이트에 곱할 분노 색 (흰색 = 변화 없음)")]
    [SerializeField]
    private Color _bossAngerTint = new Color(1f, 0.72f, 0.72f, 1f);

    [Tooltip("색조가 바뀌는 시간 (초)")]
    [SerializeField, Min(0.01f)]
    private float _tintTransitionTime = 1.2f;

    [Tooltip("색조를 입힐 보스 스프라이트들. 비우면 시작할 때 켜져 있는 보스의 모든 SpriteRenderer")]
    [SerializeField]
    private SpriteRenderer[] _bossTintTargets;

    [Header("음악 (비우면 바꾸지 않음)")]
    [Tooltip("등장 연출 시작 시 재생")]
    [SerializeField]
    private AudioClip _phase1Bgm;

    [Tooltip("2페이즈 진입 시 재생")]
    [SerializeField]
    private AudioClip _phase2Bgm;

    [SerializeField, Min(0f)]
    private float _bgmFadeTime = 1f;

    [Tooltip("처치 시 음악을 페이드 아웃")]
    [SerializeField]
    private bool _stopBgmOnDefeat = true;

    [SerializeField, Min(0f)]
    private float _defeatBgmFadeTime = 2f;

    [Header("처치")]
    [Tooltip("처치 연출 종료 신호가 오지 않을 때 최대 잠금 시간 (초)")]
    [SerializeField, Min(0.1f)]
    private float _defeatLockTimeout = 6f;

    [Tooltip("마무리 공격이 맞는 순간 화면 플래시")]
    [SerializeField]
    private Color _defeatFlashColor = Color.white;

    [SerializeField, Range(0f, 1f)]
    private float _defeatFlashPeakAlpha = 1f;

    [SerializeField, Min(0.01f)]
    private float _defeatFlashDuration = 0.6f;

    [Tooltip("슬로모션 시간 배율 (1이면 사용 안 함). 마무리 공격 히트스톱이 끝난 뒤 시작")]
    [SerializeField, Range(0.05f, 1f)]
    private float _slowMotionScale = 0.25f;

    [Tooltip("슬로모션 유지 시간 (실제 초)")]
    [SerializeField, Min(0f)]
    private float _slowMotionHold = 0.6f;

    [Tooltip("평소 속도로 돌아오는 시간 (실제 초)")]
    [SerializeField, Min(0.01f)]
    private float _slowMotionRecover = 0.5f;

    [Tooltip("처치 순간 효과음 (선택)")]
    [SerializeField]
    private AudioClip _defeatSfx;

    [Header("클리어 문구")]
    [Tooltip("처치 연출이 끝난 뒤 표시 (비우면 표시 안 함)")]
    [SerializeField]
    private string _clearText = "격파";

    [Tooltip("클리어 문구 아래 작게 표시 (비우면 표시 안 함)")]
    [SerializeField]
    private string _clearSubtext = "";

    [SerializeField, Min(1f)]
    private float _clearFontSize = 110f;

    [SerializeField]
    private Color _clearColor = new Color(1f, 0.86f, 0.45f, 1f);

    [Tooltip("클리어 문구 세로 위치 (화면 아래 0 ~ 위 1)")]
    [SerializeField, Range(0f, 1f)]
    private float _clearHeight = 0.55f;

    [SerializeField, Min(0.01f)]
    private float _clearFadeIn = 0.5f;

    [SerializeField, Min(0f)]
    private float _clearHold = 2f;

    [SerializeField, Min(0.01f)]
    private float _clearFadeOut = 0.6f;

    [Tooltip("클리어 문구가 나타날 때 효과음 (선택)")]
    [SerializeField]
    private AudioClip _clearSfx;

    [Tooltip("처치 연출이 모두 끝나고 조작이 풀릴 때 (스테이지 클리어 / 다음 씬 연결용)")]
    [SerializeField]
    private UnityEvent _onDefeatSequenceFinished;

    [Header("UI")]
    [SerializeField]
    private int _canvasSortingOrder = 50;

    private readonly List<Color> _tintBaseColors = new List<Color>();

    private PlayerInputHandler _playerInput;
    private PlayerHealth _playerHealth;
    private int _lockCount;

    private RectTransform _topBar;
    private RectTransform _bottomBar;
    private CanvasGroup _titleGroup;
    private Image _flashImage;
    private float _letterboxAmount;
    private Coroutine _letterboxRoutine;
    private Coroutine _defeatRoutine;
    private bool _isDefeatFinished;
    private CanvasGroup _clearGroup;
    private bool _isSlowMotion;

    private void Awake()
    {
        if (_boss == null)
        {
            _boss = FindFirstObjectByType<Boss>(FindObjectsInactive.Include);
        }

        if (_cameraFollow == null && Camera.main != null)
        {
            _cameraFollow = Camera.main.GetComponent<CameraFollow>();
        }

        if (_titleFont == null)
        {
            UI_BossHealthBar bar = FindFirstObjectByType<UI_BossHealthBar>(FindObjectsInactive.Include);
            TMP_Text text = bar != null ? bar.GetComponentInChildren<TMP_Text>(true) : null;
            _titleFont = text != null ? text.font : TMP_Settings.defaultFontAsset;
        }

        if (_boss == null)
        {
            Debug.LogWarning($"{name}: 씬에 Boss가 없어 보스 연출을 사용하지 않습니다.", this);
            enabled = false;
            return;
        }

        CacheTintTargets();
        BuildUI();
    }

    private void OnEnable()
    {
        Boss.OnFightStarted += HandleFightStarted;
        Boss.OnDefeated += HandleDefeated;

        if (_boss != null)
        {
            _boss.OnPhaseChanged += HandlePhaseChanged;
            _boss.OnDefeatFinished += HandleDefeatFinished;
        }
    }

    private void OnDisable()
    {
        Boss.OnFightStarted -= HandleFightStarted;
        Boss.OnDefeated -= HandleDefeated;

        if (_boss != null)
        {
            _boss.OnPhaseChanged -= HandlePhaseChanged;
            _boss.OnDefeatFinished -= HandleDefeatFinished;
            _boss.HoldIntro = false;
        }

        // 연출 도중 꺼져도 플레이어가 잠긴 채 남지 않도록
        _lockCount = 1;
        UnlockPlayer();

        // 슬로모션 도중 꺼지면 시간이 느린 채로 남지 않도록
        if (_isSlowMotion)
        {
            _isSlowMotion = false;
            Time.timeScale = 1f;
        }

        if (_cameraFollow != null)
        {
            _cameraFollow.ClearFocus();
        }
    }

    #region 이벤트

    private void HandleFightStarted(Boss boss)
    {
        if (boss != _boss || !_playIntro)
        {
            return;
        }

        if (_playIntroOnlyOnce)
        {
            string key = $"{gameObject.scene.name}/{_boss.BossName}";

            // 리스폰 등으로 이미 본 등장 연출은 생략하고 음악만 시작
            if (!SeenIntros.Add(key))
            {
                // Boss.StartFight가 이 이벤트 직후 확인해서 하울링 없이 바로 대기 → 패턴으로 넘어감
                _boss.SkipIntro = _skipIntroHowlOnRespawn;
                PlayBgm(_phase1Bgm);
                return;
            }
        }

        // 연출이 끝날 때까지 보스가 패턴을 시작하지 않도록 등장 상태를 붙잡아 둠
        _boss.HoldIntro = true;
        StartCoroutine(IntroRoutine());
    }

    private void HandlePhaseChanged(int phase)
    {
        if (phase >= 2)
        {
            StartCoroutine(HowlRoutine());
        }
    }

    private void HandleDefeated(Boss boss)
    {
        if (boss != _boss)
        {
            return;
        }

        if (_defeatRoutine != null)
        {
            StopCoroutine(_defeatRoutine);
        }

        _defeatRoutine = StartCoroutine(DefeatRoutine());
    }

    private void HandleDefeatFinished()
    {
        _isDefeatFinished = true;
    }

    #endregion

    #region 연출

    private IEnumerator IntroRoutine()
    {
        LockPlayer();
        SetLetterbox(true);
        PlayBgm(_phase1Bgm);

        // 보스 쪽으로 카메라 이동 + 줌
        if (_cameraFollow != null)
        {
            _cameraFollow.SetFocus(_boss.CenterPosition, _introPanSmoothTime);
        }

        CameraZoomSettings zoom = _introZoom;
        zoom.HoldTime = Mathf.Max(0f, _introFocusDuration - zoom.InTime);
        CameraShake.Zoom(zoom);

        StartCoroutine(TitleRoutine());
        yield return Wait(_introFocusDuration);

        // 플레이어 쪽으로 복귀
        if (_cameraFollow != null)
        {
            _cameraFollow.ClearFocus(_introReturnSmoothTime);
        }

        SetLetterbox(false);
        yield return Wait(_introReturnDuration);

        UnlockPlayer();
        _boss.HoldIntro = false;
    }

    private IEnumerator TitleRoutine()
    {
        yield return Wait(_titleDelay);
        yield return FadeCanvasGroup(_titleGroup, 0f, 1f, _titleFadeIn);
        yield return Wait(_titleHold);
        yield return FadeCanvasGroup(_titleGroup, 1f, 0f, _titleFadeOut);
    }

    private IEnumerator HowlRoutine()
    {
        LockPlayer();
        if (_letterboxOnHowl)
        {
            SetLetterbox(true);
        }

        PlayBgm(_phase2Bgm);
        StartCoroutine(FlashRoutine(_flashColor, _flashPeakAlpha, _flashDuration));
        StartCoroutine(TintRoutine(true));

        yield return Wait(_boss.HowlDuration);

        if (_letterboxOnHowl)
        {
            SetLetterbox(false);
        }

        UnlockPlayer();
    }

    private IEnumerator DefeatRoutine()
    {
        _isDefeatFinished = false;
        LockPlayer();

        // 마무리 공격이 맞는 순간: 흰 화면 플래시 + 슬로모션
        StartCoroutine(FlashRoutine(_defeatFlashColor, _defeatFlashPeakAlpha, _defeatFlashDuration));
        StartCoroutine(SlowMotionRoutine());
        PlaySfx(_defeatSfx);

        if (_letterboxOnDefeat)
        {
            SetLetterbox(true);
        }

        if (_stopBgmOnDefeat && AudioManager.Instance != null)
        {
            AudioManager.Instance.StopBgm(_defeatBgmFadeTime);
        }

        StartCoroutine(TintRoutine(false));

        float timeout = _defeatLockTimeout;
        while (!_isDefeatFinished && timeout > 0f)
        {
            timeout -= DeltaTime();
            yield return null;
        }

        // 클리어 문구
        if (_clearGroup != null)
        {
            PlaySfx(_clearSfx);
            yield return FadeCanvasGroup(_clearGroup, 0f, 1f, _clearFadeIn);
            yield return Wait(_clearHold);
            yield return FadeCanvasGroup(_clearGroup, 1f, 0f, _clearFadeOut);
        }

        if (_letterboxOnDefeat)
        {
            SetLetterbox(false);
        }

        UnlockPlayer();
        _defeatRoutine = null;
        _onDefeatSequenceFinished?.Invoke();
    }

    private IEnumerator FlashRoutine(Color color, float peakAlpha, float duration)
    {
        if (_flashImage == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += DeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);

            // 순간 번쩍 → 서서히 사라짐
            float alpha = t < 0.1f ? Mathf.Lerp(0f, 1f, t / 0.1f) : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.1f) / 0.9f);
            SetImageAlpha(_flashImage, color, peakAlpha * alpha);
            yield return null;
        }

        SetImageAlpha(_flashImage, color, 0f);
    }

    /// <summary>마무리 공격 히트스톱이 끝나면 느려졌다가 서서히 평소 속도로</summary>
    private IEnumerator SlowMotionRoutine()
    {
        if (_slowMotionScale >= 1f)
        {
            yield break;
        }

        // 히트스톱과 겹치면 서로 timeScale을 덮어쓰므로 끝날 때까지 대기
        while (HitStop.IsActive)
        {
            yield return null;
        }

        _isSlowMotion = true;
        float elapsed = 0f;
        float total = _slowMotionHold + _slowMotionRecover;

        while (elapsed < total)
        {
            if (!PauseController.IsPaused)
            {
                elapsed += Time.unscaledDeltaTime;
                float recover = Mathf.Clamp01((elapsed - _slowMotionHold) / _slowMotionRecover);
                Time.timeScale = Mathf.Lerp(_slowMotionScale, 1f, Mathf.SmoothStep(0f, 1f, recover));
            }

            yield return null;
        }

        if (!PauseController.IsPaused)
        {
            Time.timeScale = 1f;
        }

        _isSlowMotion = false;
    }

    /// <summary>분노 색조 켜기 / 끄기 (보스 스프라이트만. 화면 전체 붉은 색조는 사용 안 함)</summary>
    private IEnumerator TintRoutine(bool angry)
    {
        float elapsed = 0f;

        while (elapsed < _tintTransitionTime)
        {
            elapsed += DeltaTime();
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _tintTransitionTime));

            ApplyBossTint(angry ? t : 1f - t);
            yield return null;
        }
    }

    #endregion

    #region 플레이어 잠금

    private void LockPlayer()
    {
        _lockCount++;
        if (_lockCount > 1)
        {
            return;
        }

        FindPlayer();

        if (_playerInput != null)
        {
            _playerInput.ClearInputState();
            _playerInput.enabled = false;
        }

        if (_playerHealth != null)
        {
            _playerHealth.SetCinematicInvincible(true);
        }
    }

    private void UnlockPlayer()
    {
        if (_lockCount <= 0)
        {
            return;
        }

        _lockCount--;
        if (_lockCount > 0)
        {
            return;
        }

        if (_playerInput != null)
        {
            _playerInput.ClearInputState();
            _playerInput.enabled = true;
        }

        if (_playerHealth != null)
        {
            _playerHealth.SetCinematicInvincible(false);
        }
    }

    private void FindPlayer()
    {
        if (_playerInput != null && _playerHealth != null)
        {
            return;
        }

        var player = GameObject.FindWithTag(Constants.Tags.Player);
        if (player == null)
        {
            return;
        }

        _playerInput = player.GetComponent<PlayerInputHandler>();
        _playerHealth = player.GetComponent<PlayerHealth>();
    }

    #endregion

    #region 색조 / 음악

    private void CacheTintTargets()
    {
        if (_bossTintTargets == null || _bossTintTargets.Length == 0)
        {
            var targets = new List<SpriteRenderer>();
            foreach (SpriteRenderer renderer in _boss.GetComponentsInChildren<SpriteRenderer>(true))
            {
                // 피격 / 반짝임용 실루엣 렌더러는 시작할 때 꺼져 있으므로 제외됨
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    targets.Add(renderer);
                }
            }

            _bossTintTargets = targets.ToArray();
        }

        _tintBaseColors.Clear();
        foreach (SpriteRenderer renderer in _bossTintTargets)
        {
            _tintBaseColors.Add(renderer != null ? renderer.color : Color.white);
        }
    }

    private void ApplyBossTint(float amount)
    {
        for (int i = 0; i < _bossTintTargets.Length; i++)
        {
            SpriteRenderer renderer = _bossTintTargets[i];
            if (renderer == null)
            {
                continue;
            }

            // 알파는 처치 페이드 등 다른 연출이 쓰므로 유지
            Color tinted = _tintBaseColors[i] * Color.Lerp(Color.white, _bossAngerTint, amount);
            tinted.a = renderer.color.a;
            renderer.color = tinted;
        }
    }

    private static void PlaySfx(AudioClip clip)
    {
        if (clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(clip);
        }
    }

    private void PlayBgm(AudioClip clip)
    {
        if (clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBgm(clip, _bgmFadeTime);
        }
    }

    #endregion

    #region UI

    private void BuildUI()
    {
        var canvasObject =
            new GameObject("BossCinematicUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = _canvasSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 플래시 → 레터박스 → 타이틀 순서로 위에 그려짐
        _flashImage = CreateImage("Flash", canvasObject.transform, _flashColor);
        Stretch(_flashImage.rectTransform, Vector2.zero, Vector2.one);
        SetImageAlpha(_flashImage, _flashColor, 0f);

        _topBar = CreateImage("LetterboxTop", canvasObject.transform, Color.black).rectTransform;
        _bottomBar = CreateImage("LetterboxBottom", canvasObject.transform, Color.black).rectTransform;
        ApplyLetterbox(0f);

        var titleObject = new GameObject("Title", typeof(RectTransform), typeof(CanvasGroup));
        titleObject.transform.SetParent(canvasObject.transform, false);
        var titleRect = (RectTransform)titleObject.transform;
        titleRect.anchorMin = new Vector2(0f, _titleHeight);
        titleRect.anchorMax = new Vector2(1f, _titleHeight);
        titleRect.sizeDelta = new Vector2(0f, 200f);
        _titleGroup = titleObject.GetComponent<CanvasGroup>();
        _titleGroup.alpha = 0f;
        _titleGroup.blocksRaycasts = false;
        _titleGroup.interactable = false;

        string title = string.IsNullOrEmpty(_title) ? _boss.BossName : _title;
        CreateText("Name", titleRect, title, _titleFontSize, new Vector2(0f, -20f));

        if (!string.IsNullOrEmpty(_subtitle))
        {
            CreateText("Subtitle", titleRect, _subtitle, _subtitleFontSize, new Vector2(0f, 50f));
        }

        // 클리어 문구
        if (!string.IsNullOrEmpty(_clearText))
        {
            var clearObject = new GameObject("ClearTitle", typeof(RectTransform), typeof(CanvasGroup));
            clearObject.transform.SetParent(canvasObject.transform, false);
            var clearRect = (RectTransform)clearObject.transform;
            clearRect.anchorMin = new Vector2(0f, _clearHeight);
            clearRect.anchorMax = new Vector2(1f, _clearHeight);
            clearRect.sizeDelta = new Vector2(0f, 240f);
            _clearGroup = clearObject.GetComponent<CanvasGroup>();
            _clearGroup.alpha = 0f;
            _clearGroup.blocksRaycasts = false;
            _clearGroup.interactable = false;

            CreateText("Clear", clearRect, _clearText, _clearFontSize, new Vector2(0f, 20f)).color = _clearColor;

            if (!string.IsNullOrEmpty(_clearSubtext))
            {
                CreateText("ClearSub", clearRect, _clearSubtext, _subtitleFontSize, new Vector2(0f, -70f));
            }
        }
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

    private TextMeshProUGUI CreateText(string objectName, RectTransform parent, string text, float size, Vector2 offset)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        var rect = (RectTransform)textObject.transform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(0f, size * 1.4f);
        rect.anchoredPosition = offset;

        TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
        if (_titleFont != null)
        {
            tmp.font = _titleFont;
        }

        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = _titleColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetImageAlpha(Image image, Color color, float alpha)
    {
        color.a = alpha;
        image.color = color;
    }

    private void SetLetterbox(bool show)
    {
        if (_letterboxRoutine != null)
        {
            StopCoroutine(_letterboxRoutine);
        }

        _letterboxRoutine = StartCoroutine(LetterboxRoutine(show ? 1f : 0f));
    }

    private IEnumerator LetterboxRoutine(float target)
    {
        float start = _letterboxAmount;
        float elapsed = 0f;

        while (elapsed < _letterboxTime)
        {
            elapsed += DeltaTime();
            ApplyLetterbox(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / _letterboxTime)));
            yield return null;
        }

        ApplyLetterbox(target);
        _letterboxRoutine = null;
    }

    private void ApplyLetterbox(float amount)
    {
        _letterboxAmount = amount;
        float height = _letterboxHeight * amount;
        Stretch(_topBar, new Vector2(0f, 1f - height), Vector2.one);
        Stretch(_bottomBar, Vector2.zero, new Vector2(1f, height));
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += DeltaTime();
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        group.alpha = to;
    }

    #endregion

    #region 시간

    /// <summary>히트스톱 영향 없이 진행, 일시정지 중에는 멈춤</summary>
    private static float DeltaTime()
    {
        return PauseController.IsPaused ? 0f : Time.unscaledDeltaTime;
    }

    private static IEnumerator Wait(float seconds)
    {
        while (seconds > 0f)
        {
            seconds -= DeltaTime();
            yield return null;
        }
    }

    #endregion
}