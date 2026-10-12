using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 연속 찍기 → 마지막 찍기 후 바닥 전체에서 가시가 솟음. (1층 공격) 애니메이션 하나에 점프 / 찍기 / 가시가 모두 들어 있는 기준.
/// - 0 ~ 판정 시작: 점프해서 플레이어 위로 이동 (최고점 비율까지 올라가고, 판정 시작에 착지)
/// - 판정 시작 ~ 끝: 찍기 판정 (패링 시 그로기를 켜 두면 타이밍 가드로 보스 그로기)
/// - 마지막이 아닌 찍기: 다음 찍기 비율에서 애니메이션을 처음부터 다시 재생해 다시 점프
/// - 마지막 찍기: 바닥 전체에 빨간 경고 → 가시 비율에서 바닥 전체의 가시가 아래에서 위로 솟았다가 다시 내려감
/// </summary>
public class BossStompPattern : BossPattern
{
    [Header("점프 (비율은 애니메이션 길이 대비)")]
    [SerializeField, Min(0f)]
    private float _jumpHeight = 6f;

    [Tooltip("최고점에 도달하는 지점. 이후 판정 시작 지점까지 떨어짐")]
    [SerializeField, Range(0f, 1f)]
    private float _apexRatio = 0.25f;

    [Tooltip("이 지점까지 플레이어 위치를 따라감 (이후 착지 위치 고정)")]
    [SerializeField, Range(0f, 1f)]
    private float _trackUntilRatio = 0.3f;

    [Tooltip("맵 양 끝에서 이만큼 안쪽까지만 이동")]
    [SerializeField, Min(0f)]
    private float _edgeMargin = 2f;

    [Header("찍기")]
    [Tooltip(
        "찍는 횟수별 가중치. 원소 순서 = 1번, 2번, 3번... 찍을 확률 (예: 0.3, 0.3, 0.4면 30% 1번 / 30% 2번 / 40% 3번). 최대 횟수 = 배열 크기. 가시는 마지막 찍기 후에만 솟음")]
    [SerializeField]
    private float[] _stompCountWeights = { 0.3f, 0.3f, 0.4f };

    [Tooltip("마지막이 아닌 찍기에서 다음 점프를 시작하는 지점 (애니메이션 길이 대비 비율). 판정 끝보다 앞이면 판정 끝 사용")]
    [SerializeField, Range(0f, 1f)]
    private float _nextStompRatio = 0f;

    [Tooltip("찍기 사이 추가 대기 (초)")]
    [SerializeField, Min(0f)]
    private float _delayBetweenStomps = 0f;

    [Tooltip("찍기 판정 (보스 발밑). 패링 시 그로기를 켜 두기")]
    [SerializeField]
    private BossHitbox _stompHitbox;

    [Tooltip("착지 위치 예고 표시 (선택)")]
    [SerializeField]
    private GameObject _landingMarker;

    [SerializeField]
    private CameraShakeSettings _landingShake = new CameraShakeSettings(0.25f, 0.25f);

    [Header("착지 돌 튀는 이펙트 (PF_Boss_StompExplosion_VFX)")]
    [Tooltip("찍기 착지 때마다 발밑에서 돌이 튀는 이펙트 재생")]
    [SerializeField]
    private bool _playStompVfx = true;

    [Tooltip("이펙트 크기 배율")]
    [SerializeField, Min(0f)]
    private float _stompVfxScale = 1f;

    [Tooltip("이펙트 위치 보정 (발밑 기준. x는 보스가 보는 방향 기준)")]
    [SerializeField]
    private Vector2 _stompVfxOffset = Vector2.zero;

    [Tooltip("켜면 매번 좌우를 랜덤으로 뒤집어 같은 모양이 반복돼 보이지 않게 함. 끄면 보스가 보는 방향 기준")]
    [SerializeField]
    private bool _randomizeStompVfxFlip = true;

    [Tooltip("착지보다 이 시간(초)만큼 먼저 이펙트를 재생. 이펙트 앞부분이 늘어져 늦게 터져 보이면 올림 (0 = 착지 순간)")]
    [SerializeField, Min(0f)]
    private float _stompVfxLeadTime = 0.05f;

    [Header("바닥 가시 (마지막 찍기 후 바닥 전체에서 솟음)")]
    [Tooltip("솟아오를 가시 프리팹 (BossFloorSpike). 비우면 가시 없이 찍기만")]
    [SerializeField]
    private BossFloorSpike _floorSpikePrefab;

    [Tooltip("가시가 솟는 지점 (애니메이션 길이 대비 비율)")]
    [SerializeField, Range(0f, 1f)]
    private float _needleRatio = 0.75f;

    [Tooltip("가시가 솟는 바닥 높이 기준. 비우면 보스 바닥 높이(1층)")]
    [SerializeField]
    private Transform _spikeFloor;

    [Tooltip("가시 사이 간격 (월드)")]
    [SerializeField, Min(0.1f)]
    private float _spikeSpacing = 0.5f;

    [Tooltip("맵 양 끝에서 이만큼 안쪽부터 가시를 깔기")]
    [SerializeField, Min(0f)]
    private float _spikeEdgeMargin = 0f;

    [Tooltip("다 솟았을 때 가시 위치가 바닥보다 얼마나 위인지 (가시 프리팹의 기준점 기준)")]
    [SerializeField]
    private float _spikeUpOffset = 0.5f;

    [Tooltip("바닥 아래에서 솟아오르는 거리")]
    [SerializeField, Min(0f)]
    private float _spikeRiseHeight = 1.5f;

    [Tooltip("솟아오르는 시간 (초)")]
    [SerializeField, Min(0.01f)]
    private float _spikeRiseTime = 0.12f;

    [Tooltip("솟은 채로 유지하는 시간 (초)")]
    [SerializeField, Min(0f)]
    private float _spikeHoldTime = 0.5f;

    [Tooltip("다시 내려가는 시간 (초)")]
    [SerializeField, Min(0.01f)]
    private float _spikeSinkTime = 0.3f;

    [Header("가시 예고 (마지막 찍기, 바닥 전체가 빨갛게 빛남)")]
    [Tooltip("가시가 솟기 몇 초 전에 예고할지 (0이면 예고 없음)")]
    [SerializeField, Min(0f)]
    private float _needleWarningLead = 0.5f;

    [Tooltip("예고 줄 색 (알파 = 최대 밝기)")]
    [SerializeField]
    private Color _needleWarningColor = new Color(1f, 0.1f, 0.1f, 0.55f);

    [Tooltip("예고 띠 높이 (월드). 바닥에서 위로 이만큼. 가시가 솟는 높이 정도로")]
    [SerializeField, Min(0.01f)]
    private float _needleWarningThickness = 0.4f;

    [Tooltip("예고 / 가시 범위 (보스 위치에서 좌 / 우 각각, 월드). 0이면 맵 끝까지 (BossArena 기준)")]
    [SerializeField, Min(0f)]
    private float _needleWarningLength = 0f;

    [Tooltip("깜빡이는 속도 (클수록 빠름)")]
    [SerializeField, Min(0f)]
    private float _needleWarningBlinkSpeed = 18f;

    [Tooltip("예고 줄 정렬 순서 (보스 스프라이트와 같은 Sorting Layer 사용, 값이 클수록 앞)")]
    [SerializeField]
    private int _needleWarningSortingOffset = 5;

    private static Sprite _warningSprite;
    private SpriteRenderer _needleWarningLine;

    // 떨어지면서 몸통이 먼저 닿아 찍기 판정 전에 맞지 않도록 기본으로 몸통 접촉 데미지 끔 (기존 컴포넌트에도 적용)
    public BossStompPattern()
    {
        _disableContactDamage = true;
    }

    private void Reset()
    {
        SetDefaults("연속 찍기 → 바늘", BossPhaseMask.Both, BossFloorMask.Floor1);
    }

    protected override void OnInitialize()
    {
        if (_stompHitbox == null)
        {
            Debug.LogWarning($"{name}: {PatternName}에 찍기 판정(BossHitbox)이 연결되지 않아 사용하지 않습니다.", this);
            enabled = false;
        }

        if (_landingMarker != null)
        {
            _landingMarker.SetActive(false);
        }

        HideNeedleWarning();
    }

    protected override IEnumerator Execute()
    {
        int count = PickStompCount();
        Boss.Log($"{PatternName}: {count}번 찍기");
        float nextStompRatio = Mathf.Max(_nextStompRatio, HitEndRatio);

        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                if (_delayBetweenStomps > 0f)
                {
                    yield return new WaitForSeconds(_delayBetweenStomps);
                }

                // 같은 애니메이션을 처음부터 다시 재생 (진행도도 0부터)
                Boss.FacePlayer();
                ReplayAnimation();
            }

            yield return Stomp();

            if (i < count - 1)
            {
                yield return WaitUntilProgress(nextStompRatio);
            }
        }

        // 마지막 찍기 후에만 바닥 가시 (패링당했으면 그로기로 넘어가 여기까지 오지 않음)
        yield return EruptFloorSpikes();
    }

    /// <summary>가중치 배열로 이번에 찍을 횟수 결정 (1 ~ 배열 크기). 배열이 비었거나 합이 0이면 1번</summary>
    private int PickStompCount()
    {
        if (_stompCountWeights == null || _stompCountWeights.Length == 0)
        {
            return 1;
        }

        float total = 0f;
        foreach (float weight in _stompCountWeights)
        {
            total += Mathf.Max(0f, weight);
        }

        if (total <= 0f)
        {
            return 1;
        }

        float pick = Random.value * total;
        for (int i = 0; i < _stompCountWeights.Length; i++)
        {
            pick -= Mathf.Max(0f, _stompCountWeights[i]);
            if (pick <= 0f && _stompCountWeights[i] > 0f)
            {
                return i + 1;
            }
        }

        // 부동소수점 오차로 끝까지 온 경우: 가중치가 있는 마지막 횟수
        for (int i = _stompCountWeights.Length - 1; i >= 0; i--)
        {
            if (_stompCountWeights[i] > 0f)
            {
                return i + 1;
            }
        }

        return 1;
    }

    /// <summary>점프 → 착지 → 찍기 판정 (판정 끝까지). 높은 곳(소환 위치 등)에서 시작하면 그 높이에서 뛰어내림</summary>
    private IEnumerator Stomp()
    {
        float startX = Boss.Position.x;
        float startY = Boss.Position.y;
        float groundY = Boss.GroundY;
        float landRatio = Mathf.Max(HitStartRatio, 0.01f);
        float apexRatio = Mathf.Min(_apexRatio, landRatio * 0.95f);
        float targetX = GetTargetX();
        bool hasWarned = false;
        bool vfxPlayed = false;

        // 점프 → 착지 (판정 시작 지점에 착지)
        while (Progress < landRatio)
        {
            float progress = Progress;

            // 패링 그로기 찍기면 착지 직전에 반짝임
            hasWarned = UpdateParryWarning(_stompHitbox, landRatio, hasWarned);

            if (progress < _trackUntilRatio)
            {
                targetX = GetTargetX();
            }

            float x = Mathf.Lerp(startX, targetX, Mathf.SmoothStep(0f, 1f, progress / landRatio));
            float y = GetJumpY(progress, apexRatio, landRatio, startY, groundY);
            Boss.MoveTo(new Vector2(x, y));

            ShowLandingMarker(progress >= apexRatio, targetX, groundY);

            // 착지 직전에 미리 재생 (Stomp Vfx Lead Time)
            if (!vfxPlayed && (landRatio - progress) * Duration <= _stompVfxLeadTime)
            {
                PlayStompVfx(targetX, groundY);
                vfxPlayed = true;
            }

            yield return new WaitForFixedUpdate();
        }

        // 착지 + 찍기 판정
        Boss.MoveTo(new Vector2(targetX, groundY));
        Boss.SnapToGround();
        ShowLandingMarker(false, targetX, groundY);
        CameraShake.Shake(_landingShake);
        if (!vfxPlayed)
        {
            PlayStompVfx(targetX, groundY);
        }

        AudioResult("Boss.Stomp", Boss.transform.position);
        _stompHitbox.Activate();
        yield return WaitUntilProgress(HitEndRatio);
        _stompHitbox.Deactivate();
    }

    /// <summary>착지 지점 발밑에서 돌 튀는 이펙트</summary>
    private void PlayStompVfx(float x, float groundY)
    {
        if (!_playStompVfx || _stompVfxScale <= 0f)
        {
            return;
        }

        EffectManager effects = EffectManager.Instance;
        if (effects == null)
        {
            return;
        }

        float facing = Boss.FacingX;
        var position = new Vector2(x + (_stompVfxOffset.x * facing), groundY + _stompVfxOffset.y);
        bool flip = _randomizeStompVfxFlip ? Random.value < 0.5f : facing < 0f;
        effects.PlayBossStompExplosion(position, _stompVfxScale, flip);
    }

    private IEnumerator EruptFloorSpikes()
    {
        if (_floorSpikePrefab == null)
        {
            yield break;
        }

        // 가시 예고: 솟기 Lead초 전부터 바닥 전체가 빨갛게 깜빡임
        if (_needleWarningLead > 0f)
        {
            float leadRatio = Duration > 0f ? _needleWarningLead / Duration : 0f;
            float warnRatio = Mathf.Max(0f, _needleRatio - leadRatio);
            yield return WaitUntilProgress(warnRatio);

            ShowNeedleWarning();
            float warnStart = Time.time;
            while (Progress < _needleRatio)
            {
                UpdateNeedleWarning(Time.time - warnStart);
                yield return null;
            }

            HideNeedleWarning();
        }

        yield return WaitUntilProgress(_needleRatio);
        SpawnFloorSpikes();
    }

    /// <summary>바닥 범위 전체에 간격대로 가시를 깔고 동시에 솟게 함</summary>
    private void SpawnFloorSpikes()
    {
        GetSpikeRange(out float left, out float right);
        float upY = GetSpikeFloorY() + _spikeUpOffset;

        int count = Mathf.Max(1, Mathf.FloorToInt((right - left) / _spikeSpacing) + 1);
        float step = count > 1 ? (right - left) / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            var upPosition = new Vector2(count > 1 ? left + (step * i) : (left + right) * 0.5f, upY);
            BossFloorSpike spike = Instantiate(_floorSpikePrefab, upPosition, Quaternion.identity);
            spike.Begin(Boss, upPosition, _spikeRiseHeight, _spikeRiseTime, _spikeHoldTime, _spikeSinkTime);
        }

        AudioResult("Boss.NeedleFire", new Vector3((left + right) * 0.5f, upY, Boss.transform.position.z));
    }

    private float GetSpikeFloorY()
    {
        return _spikeFloor != null ? _spikeFloor.position.y : Boss.GroundY;
    }

    /// <summary>가시 / 예고 범위. Length가 0이면 맵 끝까지 (양 끝 여백 제외)</summary>
    private void GetSpikeRange(out float left, out float right)
    {
        float centerX = Boss.Position.x;

        if (_needleWarningLength > 0f)
        {
            left = centerX - _needleWarningLength;
            right = centerX + _needleWarningLength;
        }
        else
        {
            BossArena arena = Boss.Arena;
            bool hasBounds = arena != null && !float.IsInfinity(arena.LeftX) && !float.IsInfinity(arena.RightX);
            left = hasBounds ? arena.LeftX : centerX - 20f;
            right = hasBounds ? arena.RightX : centerX + 20f;
        }

        left += _spikeEdgeMargin;
        right -= _spikeEdgeMargin;
        if (left > right)
        {
            left = right = (left + right) * 0.5f;
        }
    }

    protected override void Cleanup()
    {
        if (_stompHitbox != null)
        {
            _stompHitbox.Deactivate();
            _stompHitbox.SetTelegraph(false);
        }

        if (_landingMarker != null)
        {
            _landingMarker.SetActive(false);
        }

        HideNeedleWarning();

        // 공중에서 끊겼으면 땅으로
        if (Boss != null)
        {
            Boss.SnapToGround();
        }
    }

    private float GetJumpY(float progress, float apexRatio, float landRatio, float startY, float groundY)
    {
        float apexY = Mathf.Max(startY, groundY) + _jumpHeight;

        if (progress < apexRatio)
        {
            // 올라갈 때는 점점 느려짐
            float t = apexRatio > 0f ? progress / apexRatio : 1f;
            return Mathf.Lerp(startY, apexY, 1f - ((1f - t) * (1f - t)));
        }

        // 떨어질 때는 점점 빨라짐
        float fall = (progress - apexRatio) / Mathf.Max(0.0001f, landRatio - apexRatio);
        return Mathf.Lerp(apexY, groundY, fall * fall);
    }

    private float GetTargetX()
    {
        float x = Boss.Player != null ? Boss.Player.position.x : Boss.Position.x;
        return Boss.Arena != null ? Boss.Arena.ClampX(x, _edgeMargin) : x;
    }

    private void ShowLandingMarker(bool show, float x, float groundY)
    {
        if (_landingMarker == null)
        {
            return;
        }

        _landingMarker.SetActive(show);
        if (show)
        {
            _landingMarker.transform.position = new Vector3(x, groundY, _landingMarker.transform.position.z);
        }
    }

    #region 가시 예고 줄

    private void ShowNeedleWarning()
    {
        if (_needleWarningLine == null)
        {
            CreateNeedleWarningLine();
        }

        GetSpikeRange(out float left, out float right);

        // 바닥에서 위로 Thickness만큼 덮는 띠 (가시 간격 반만큼 양옆을 더 덮음)
        float halfSpacing = _spikeSpacing * 0.5f;
        left -= halfSpacing;
        right += halfSpacing;
        float floorY = GetSpikeFloorY();

        Transform line = _needleWarningLine.transform;
        line.position = new Vector3((left + right) * 0.5f, floorY + (_needleWarningThickness * 0.5f), 0f);
        line.localScale = new Vector3(Mathf.Max(0.01f, right - left), _needleWarningThickness, 1f);

        // 보스 스프라이트와 같은 정렬 레이어에서 조금 앞에
        SpriteRenderer bossRenderer = Boss.GetComponentInChildren<SpriteRenderer>();
        if (bossRenderer != null)
        {
            _needleWarningLine.sortingLayerID = bossRenderer.sortingLayerID;
            _needleWarningLine.sortingOrder = bossRenderer.sortingOrder + _needleWarningSortingOffset;
        }

        _needleWarningLine.gameObject.SetActive(true);
        UpdateNeedleWarning(0f);
    }

    /// <summary>깜빡이면서 점점 진해지고 높아짐 (솟을 때가 가까울수록)</summary>
    private void UpdateNeedleWarning(float elapsed)
    {
        if (_needleWarningLine == null)
        {
            return;
        }

        float progress = _needleWarningLead > 0f ? Mathf.Clamp01(elapsed / _needleWarningLead) : 1f;
        float blink = 0.6f + (0.4f * Mathf.Sin(elapsed * _needleWarningBlinkSpeed));

        Color color = _needleWarningColor;
        color.a = _needleWarningColor.a * Mathf.Lerp(0.5f, 1f, progress) * blink;
        _needleWarningLine.color = color;

        // 바닥에 붙은 채로 높이만 커지도록 위치도 같이 맞춤
        float height = _needleWarningThickness * Mathf.Lerp(0.4f, 1f, progress);
        Transform line = _needleWarningLine.transform;
        Vector3 scale = line.localScale;
        scale.y = height;
        line.localScale = scale;

        Vector3 position = line.position;
        position.y = GetSpikeFloorY() + (height * 0.5f);
        line.position = position;
    }

    private void HideNeedleWarning()
    {
        if (_needleWarningLine != null)
        {
            _needleWarningLine.gameObject.SetActive(false);
        }
    }

    private void CreateNeedleWarningLine()
    {
        if (_warningSprite == null)
        {
            var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            _warningSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        // 보스 자식으로 두면 보스 좌우 반전 / 날려 보내기에 같이 움직이므로 따로 둠
        var lineObject = new GameObject($"{name}_NeedleWarning");
        _needleWarningLine = lineObject.AddComponent<SpriteRenderer>();
        _needleWarningLine.sprite = _warningSprite;
        lineObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_needleWarningLine != null)
        {
            Destroy(_needleWarningLine.gameObject);
        }
    }

    #endregion
}