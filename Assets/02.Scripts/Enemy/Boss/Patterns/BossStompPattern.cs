using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 찍기 → 좌우 바늘. (1층 공격)
/// 점프해서 플레이어 위로 이동 → 잠깐 조준 → 내려찍기(판정) → 착지 후 좌우로 바늘 발사.
/// 찍기 판정(BossHitbox)의 "패링 시 그로기"를 켜 두면, 타이밍 맞춘 가드로 보스가 그로기 (프리딜).
/// </summary>
public class BossStompPattern : BossPattern
{
    [Header("점프")]
    [SerializeField, Min(0.05f)] private float _jumpDuration = 0.8f;
    [SerializeField, Min(0f)] private float _jumpHeight = 6f;

    [Tooltip("공중에서 플레이어를 따라가며 조준하는 시간 (초)")]
    [SerializeField, Min(0f)]
    private float _aimTime = 0.4f;

    [Tooltip("조준 중 플레이어를 따라가는 속도")]
    [SerializeField, Min(0f)]
    private float _aimFollowSpeed = 6f;

    [Tooltip("맵 양 끝에서 이만큼 안쪽까지만 이동")]
    [SerializeField, Min(0f)]
    private float _edgeMargin = 2f;

    [Header("찍기")]
    [Tooltip("찍기 판정 (보스 발밑). 패링 시 그로기를 켜 두기")]
    [SerializeField]
    private BossHitbox _stompHitbox;

    [Tooltip("떨어지는 시간 (초)")]
    [SerializeField, Min(0.01f)]
    private float _slamDuration = 0.15f;

    [Tooltip("착지 후 찍기 판정 유지 시간 (초)")]
    [SerializeField, Min(0f)]
    private float _stompActiveTime = 0.15f;

    [Tooltip("착지 위치 예고 표시 (선택)")]
    [SerializeField]
    private GameObject _landingMarker;

    [SerializeField] private CameraShakeSettings _landingShake = new CameraShakeSettings(0.25f, 0.25f);

    [Header("좌우 바늘")]
    [SerializeField] private BossProjectile _needlePrefab;

    [Tooltip("바늘 발사 위치 (비우면 보스 발밑)")]
    [SerializeField]
    private Transform _needleSpawnPoint;

    [Tooltip("착지 후 바늘 발사까지 (초)")]
    [SerializeField, Min(0f)]
    private float _needleDelay = 0.25f;

    [Tooltip("한쪽에 쏘는 바늘 수")]
    [SerializeField, Min(0)]
    private int _needleCountPerSide = 1;

    [SerializeField, Min(0f)] private float _needleInterval = 0.2f;
    [SerializeField, Min(0f)] private float _recoverTime = 0.8f;

    [Header("애니메이션 (없으면 무시)")]
    [SerializeField] private string _jumpTrigger = "stompJump";
    [SerializeField] private string _slamTrigger = "stompSlam";
    [SerializeField] private string _needleTrigger = "needle";

    private void Reset()
    {
        SetDefaults("찍기 → 바늘", BossPhaseMask.Both, BossFloorMask.Floor1);
    }

    protected override IEnumerator Execute()
    {
        Boss.FacePlayer();
        Boss.SetTrigger(_jumpTrigger);

        Vector2 start = Boss.Position;
        float groundY = Boss.GroundY;
        float apexY = groundY + _jumpHeight;
        float targetX = GetTargetX();

        // 점프: 플레이어 위로 이동하며 올라감
        float elapsed = 0f;
        while (elapsed < _jumpDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / _jumpDuration);
            float x = Mathf.Lerp(start.x, targetX, Mathf.SmoothStep(0f, 1f, t));
            float y = Mathf.Lerp(start.y, apexY, 1f - ((1f - t) * (1f - t)));
            Boss.MoveTo(new Vector2(x, y));
            yield return new WaitForFixedUpdate();
        }

        // 조준: 플레이어를 따라가며 착지 위치 예고
        float currentX = targetX;
        ShowLandingMarker(true, currentX, groundY);

        elapsed = 0f;
        while (elapsed < _aimTime)
        {
            elapsed += Time.fixedDeltaTime;
            currentX = Mathf.MoveTowards(currentX, GetTargetX(), _aimFollowSpeed * Time.fixedDeltaTime);
            Boss.MoveTo(new Vector2(currentX, apexY));
            ShowLandingMarker(true, currentX, groundY);
            yield return new WaitForFixedUpdate();
        }

        // 내려찍기
        Boss.SetTrigger(_slamTrigger);
        _stompHitbox.Activate();

        elapsed = 0f;
        while (elapsed < _slamDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / _slamDuration);
            Boss.MoveTo(new Vector2(currentX, Mathf.Lerp(apexY, groundY, t * t)));
            yield return new WaitForFixedUpdate();
        }

        Boss.SnapToGround();
        ShowLandingMarker(false, currentX, groundY);
        CameraShake.Shake(_landingShake);

        yield return new WaitForSeconds(_stompActiveTime);

        _stompHitbox.Deactivate();

        // 좌우 바늘 (패링당했으면 그로기로 넘어가 여기까지 오지 않음)
        if (_needlePrefab != null && _needleCountPerSide > 0)
        {
            yield return new WaitForSeconds(_needleDelay);

            Boss.SetTrigger(_needleTrigger);
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

        yield return new WaitForSeconds(_recoverTime);
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
