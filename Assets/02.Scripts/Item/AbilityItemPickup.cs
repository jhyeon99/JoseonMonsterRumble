using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class AbilityItemPickup : MonoBehaviour
{
    private const string MissingDataWarning = "능력 아이템: 데이터 또는 Trigger Collider 설정이 없습니다.";
    private const string MissingViewWarning = "능력 아이템: 활성 획득 설명 UI가 없습니다. 아이템을 유지합니다.";
    private enum PickupPhase { WaitingForClear, Dropping, Available, Collecting, Collected }

    [SerializeField] private AbilityItemData _item;
    [SerializeField, ReadOnly] private PickupPhase _phase;

    [Header("스테이지 클리어 드랍 연동")]
    [Tooltip("체크 시 도깨비 발판 전투 클리어 전까지 숨겨지고 상호작용할 수 없습니다.")]
    [SerializeField] private bool _waitForStageClear;
    [SerializeField, Min(0.1f)] private float _dropHeight = 3.5f;
    [SerializeField, Min(0.1f)] private float _dropDuration = 0.6f;

    [Header("획득 연출 및 장승 워프")]
    [Tooltip("획득 UI를 닫으면 현재 저장된 장승으로 이동합니다.")]
    [SerializeField] private bool _warpToCheckpointOnClose = true;
    [SerializeField] private GameObject _flameBurstPrefab;

    private Collider2D _trigger;
    private SpriteRenderer _spriteRenderer;
    private Vector3 _targetPosition;
    private bool _hasWarnedMissingView;
    private bool _hasDropped;
    private bool _isWaitingForCheckpointWarp;

    private void Awake()
    {
        _trigger = GetComponent<Collider2D>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _targetPosition = transform.position;

        if (_item == null || !_item.IsValid || !_trigger.isTrigger)
        {
            Debug.LogWarning(MissingDataWarning, this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_item != null && AbilityItemProgress.HasItem(_item.ItemId))
        {
            _phase = PickupPhase.Collected;
            gameObject.SetActive(false);
            return;
        }

        if (_waitForStageClear && !_hasDropped)
        {
            _phase = PickupPhase.WaitingForClear;
            _trigger.enabled = false;
            if (_spriteRenderer != null) _spriteRenderer.enabled = false;
            DokkaebiPlatformWavePreview.OnDokkaebiPlatformCleared += HandleStageCleared;
            return;
        }

        _phase = PickupPhase.Available;
        _trigger.enabled = true;
        if (_spriteRenderer != null) _spriteRenderer.enabled = true;
        _hasWarnedMissingView = false;
    }

    private void OnDisable()
    {
        DokkaebiPlatformWavePreview.OnDokkaebiPlatformCleared -= HandleStageCleared;
        ItemAcquisitionView.OnItemAcquisitionClosed -= HandleItemAcquisitionClosed;
        _isWaitingForCheckpointWarp = false;
    }

    private void HandleStageCleared()
    {
        DokkaebiPlatformWavePreview.OnDokkaebiPlatformCleared -= HandleStageCleared;
        if (_hasDropped || (_item != null && AbilityItemProgress.HasItem(_item.ItemId))) return;
        _hasDropped = true;
        StartCoroutine(DropRoutine());
    }

    private IEnumerator DropRoutine()
    {
        _phase = PickupPhase.Dropping;
        if (_spriteRenderer != null) _spriteRenderer.enabled = true;
        _trigger.enabled = false;

        Vector3 startPos = _targetPosition + Vector3.up * _dropHeight;
        transform.position = startPos;

        float elapsed = 0f;
        while (elapsed < _dropDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _dropDuration);
            float curve = t * t;
            transform.position = Vector3.Lerp(startPos, _targetPosition, curve);
            yield return null;
        }

        transform.position = _targetPosition;
        _phase = PickupPhase.Available;
        _trigger.enabled = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleContact(other);
    private void OnTriggerStay2D(Collider2D other) => HandleContact(other);

    private void HandleContact(Collider2D other)
    {
        if (_phase != PickupPhase.Available || PauseController.IsGameplayInputBlocked)
        {
            return;
        }
        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.Health == null || player.Health.IsDead)
        {
            return;
        }
        ItemAcquisitionView view = ItemAcquisitionView.Instance;
        if (view == null)
        {
            if (!_hasWarnedMissingView)
            {
                Debug.LogWarning(MissingViewWarning, this);
                _hasWarnedMissingView = true;
            }
            return;
        }
        _phase = PickupPhase.Collecting;
        if (!view.TryAcquireItem(_item))
        {
            _phase = PickupPhase.Available;
            return;
        }
        _phase = PickupPhase.Collected;
        _trigger.enabled = false;
        if (_spriteRenderer != null) _spriteRenderer.enabled = false;
        PlayPickupBurst();

        if (_warpToCheckpointOnClose)
        {
            _isWaitingForCheckpointWarp = true;
            ItemAcquisitionView.OnItemAcquisitionClosed += HandleItemAcquisitionClosed;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void HandleItemAcquisitionClosed()
    {
        ItemAcquisitionView.OnItemAcquisitionClosed -= HandleItemAcquisitionClosed;
        if (!_isWaitingForCheckpointWarp) return;
        _isWaitingForCheckpointWarp = false;
        if (StageManager.Instance != null)
        {
            StageManager.Instance.TryWarpToActiveCheckpoint();
        }
        gameObject.SetActive(false);
    }

    private void PlayPickupBurst()
    {
        if (_flameBurstPrefab == null) return;
        Vector3 origin = transform.position;
        for (int i = 0; i < 4; i++)
        {
            float angle = (45f + i * 90f) * Mathf.Deg2Rad;
            GameObject flame = Instantiate(_flameBurstPrefab, origin, Quaternion.identity);
            flame.name = "GamtuPickupFlame_" + i;
            SpriteRenderer renderer = flame.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sortingLayerID = -1826688615;
                renderer.sortingOrder = 20;
            }

            flame.AddComponent<BlueFlameScatter>().Initialize(
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), 1.7f, 0.85f, 0.65f);
        }
    }

    [ContextMenu("개발용: 이 아이템 획득 기록 초기화")]
    private void ResetThisItemProgress()
    {
        if (_item != null)
        {
            AbilityItemProgress.ResetProgress(new[] { _item });
            Debug.Log($"AbilityItemPickup: '{_item.DisplayName}' 획득 기록이 초기화되었습니다.", this);
        }
    }
}
