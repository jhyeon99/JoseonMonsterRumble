using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 찍기 → 좌우 바늘. (1층 공격) 애니메이션 하나에 점프 / 찍기 / 바늘이 모두 들어 있는 기준.
/// - 0 ~ 판정 시작: 점프해서 플레이어 위로 이동 (최고점 비율까지 올라가고, 판정 시작에 착지)
/// - 판정 시작 ~ 끝: 찍기 판정 (패링 시 그로기를 켜 두면 타이밍 가드로 보스 그로기)
/// - 바늘 비율: 좌우로 바늘 발사
/// </summary>
public class BossStompPattern : BossPattern
{
    [Header("점프 (비율은 애니메이션 길이 대비)")]
    [SerializeField, Min(0f)] private float _jumpHeight = 6f;

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
    [Tooltip("찍기 판정 (보스 발밑). 패링 시 그로기를 켜 두기")]
    [SerializeField]
    private BossHitbox _stompHitbox;

    [Tooltip("착지 위치 예고 표시 (선택)")]
    [SerializeField]
    private GameObject _landingMarker;

    [SerializeField] private CameraShakeSettings _landingShake = new CameraShakeSettings(0.25f, 0.25f);

    [Header("좌우 바늘")]
    [SerializeField] private BossProjectile _needlePrefab;

    [Tooltip("바늘 발사 위치 (비우면 보스 발밑)")]
    [SerializeField]
    private Transform _needleSpawnPoint;

    [Tooltip("바늘 발사 지점 (애니메이션 길이 대비 비율)")]
    [SerializeField, Range(0f, 1f)]
    private float _needleRatio = 0.75f;

    [Tooltip("한쪽에 쏘는 바늘 수")]
    [SerializeField, Min(0)]
    private int _needleCountPerSide = 1;

    [Tooltip("바늘이 여러 개일 때 간격 (초)")]
    [SerializeField, Min(0f)]
    private float _needleInterval = 0.2f;

    private void Reset()
    {
        SetDefaults("찍기 → 바늘", BossPhaseMask.Both, BossFloorMask.Floor1);
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
    }

    protected override IEnumerator Execute()
    {
        float startX = Boss.Position.x;
        float groundY = Boss.GroundY;
        float landRatio = Mathf.Max(HitStartRatio, 0.01f);
        float apexRatio = Mathf.Min(_apexRatio, landRatio * 0.95f);
        float targetX = GetTargetX();

        // 점프 → 착지 (판정 시작 지점에 착지)
        while (Progress < landRatio)
        {
            float progress = Progress;

            if (progress < _trackUntilRatio)
            {
                targetX = GetTargetX();
            }

            float x = Mathf.Lerp(startX, targetX, Mathf.SmoothStep(0f, 1f, progress / landRatio));
            float y = GetJumpY(progress, apexRatio, landRatio, groundY);
            Boss.MoveTo(new Vector2(x, y));

            ShowLandingMarker(progress >= apexRatio, targetX, groundY);
            yield return new WaitForFixedUpdate();
        }

        // 착지 + 찍기 판정
        Boss.MoveTo(new Vector2(targetX, groundY));
        Boss.SnapToGround();
        ShowLandingMarker(false, targetX, groundY);
        CameraShake.Shake(_landingShake);

        _stompHitbox.Activate();
        yield return WaitUntilProgress(HitEndRatio);
        _stompHitbox.Deactivate();

        // 좌우 바늘 (패링당했으면 그로기로 넘어가 여기까지 오지 않음)
        if (_needlePrefab == null || _needleCountPerSide <= 0)
        {
            yield break;
        }

        yield return WaitUntilProgress(_needleRatio);

        for (int i = 0; i < _needleCountPerSide; i++)
        {
            FireNeedle(Vector2.left);
            FireNeedle(Vector2.right);

            if (i < _needleCountPerSide - 1)
            {
                yield return new WaitForSeconds(_needleInterval);
            }
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

        // 공중에서 끊겼으면 땅으로
        if (Boss != null)
        {
            Boss.SnapToGround();
        }
    }

    private float GetJumpY(float progress, float apexRatio, float landRatio, float groundY)
    {
        float apexY = groundY + _jumpHeight;

        if (progress < apexRatio)
        {
            // 올라갈 때는 점점 느려짐
            float t = apexRatio > 0f ? progress / apexRatio : 1f;
            return Mathf.Lerp(groundY, apexY, 1f - ((1f - t) * (1f - t)));
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

    private void FireNeedle(Vector2 direction)
    {
        Vector3 position = _needleSpawnPoint != null ? _needleSpawnPoint.position : (Vector3)Boss.Position;
        BossProjectile needle = Instantiate(_needlePrefab, position, Quaternion.identity);
        needle.Launch(direction);
    }
}
