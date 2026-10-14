using UnityEngine;

/// <summary>
/// 독 방울 (소환 패턴이 맵 아래에 생성).
/// 처음엔 빠르게 솟아오르고, 이후 천천히 계속 위로 올라가다 끝 높이에 닿으면 그대로 사라짐.
/// 체력이 0이 되면 터지면서 올챙이(BossBaby)가 나옴.
/// 레이어는 Enemy (플레이어 공격에 맞도록), Rigidbody2D + 콜라이더 필요 (Rigidbody2D는 자동으로 Kinematic).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BossPoisonBubble : MonoBehaviour, IDamageable, IHitFeedbackSource
{
    [Header("체력")]
    [SerializeField, Min(1f)]
    private float _maxHp = 40f;

    [Header("상승")]
    [Tooltip("처음 맵 아래에서 솟아오르는 거리")]
    [SerializeField, Min(0f)]
    private float _emergeHeight = 2f;

    [Tooltip("처음 솟아오를 때 속도")]
    [SerializeField, Min(0f)]
    private float _emergeSpeed = 5f;

    [Tooltip("이후 천천히 올라가는 속도")]
    [SerializeField, Min(0f)]
    private float _riseSpeed = 0.8f;

    [Tooltip("끝 높이가 지정되지 않았을 때 이 시간 뒤 사라짐 (초)")]
    [SerializeField, Min(0.1f)]
    private float _fallbackLifeTime = 20f;

    [Header("피격 밀림")]
    [SerializeField, Min(0f)] private float _hitPushSpeed = 2f;
    [SerializeField, Min(0.1f)] private float _hitPushDeceleration = 12f;

    [Header("흡입 반짝임")]
    [SerializeField] private EnemyHitFeedbackSettings _suctionFlash = new EnemyHitFeedbackSettings(0.12f, 0.85f);

    [Tooltip("흡수 축소가 이 비율에 도달하면 보스 뒤로 그려 입 안으로 들어가는 모습 표현")]
    [SerializeField, Range(0f, 1f)] private float _absorbHideRatio = 0.75f;

    [Header("터짐")]
    [Tooltip("터지면 나오는 올챙이")]
    [SerializeField]
    private BossBaby _tadpolePrefab;

    [Tooltip("터질 때 생성할 이펙트 (선택)")]
    [SerializeField]
    private GameObject _popEffect;

    [Header("상태 (확인용)")]
    [SerializeField, ReadOnly]
    private float _currentHp;

    private Rigidbody2D _body;
    private HitFlash _hitFlash;
    private Boss _boss;
    private float _startY;
    private float _topY = float.PositiveInfinity;
    private float _lifeTimer;
    private bool _isPopped;
    private float _hitPushVelocityX;
    private Transform _suctionTarget;
    private float _suctionDistance;
    private float _suctionDuration;
    private float _suctionElapsed;
    private float _absorbRadius;
    private float _absorbDuration;
    private float _absorbElapsed;
    private Vector3 _originalScale;
    private bool _isAbsorbing;
    private Collider2D _collider;
    private EnemyHitFeedback _suctionFeedback;
    private SpriteRenderer _sourceRenderer;
    private SpriteRenderer _suctionBossRenderer;

    public event System.Action OnAbsorbed;

    public void StartSuctionPulse(Transform target, float distance, float duration, float absorbRadius, float absorbDuration)
    {
        if (_isPopped || _isAbsorbing || target == null) return;
        _suctionBossRenderer = target.GetComponentInParent<SpriteRenderer>();
        SetSuctionSorting(true);
        if (_suctionFeedback != null) _suctionFeedback.StartFlash(_suctionFlash);
        _suctionTarget = target;
        _suctionDistance = distance;
        _suctionDuration = Mathf.Max(0.01f, duration);
        _suctionElapsed = 0f;
        _absorbRadius = absorbRadius;
        _absorbDuration = Mathf.Max(0.01f, absorbDuration);
    }

    /// <summary>맞았을 때 (EnemyHitFeedback 피격 연출용)</summary>
    public event System.Action<HitData> OnHitFeedback;

    /// <summary>생성 직후 호출. topY에 닿으면 사라짐 (무한대면 수명 기준)</summary>
    public void Initialize(Boss boss, float topY)
    {
        _boss = boss;
        _topY = topY;
        if (_boss != null)
        {
            _boss.RegisterMinion(gameObject);
        }
    }

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.linearVelocity = Vector2.zero;
        _hitFlash = GetComponent<HitFlash>();
        _currentHp = _maxHp;
        _startY = transform.position.y;
        _originalScale = transform.localScale;
        _collider = GetComponent<Collider2D>();
        _suctionFeedback = GetComponent<EnemyHitFeedback>();
        _sourceRenderer = GetComponent<SpriteRenderer>();
    }

    private void FixedUpdate()
    {
        if (_isPopped)
        {
            return;
        }

        if (_isAbsorbing)
        {
            if (_suctionTarget == null)
            {
                Destroy(gameObject);
                return;
            }
            _absorbElapsed += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(_absorbElapsed / _absorbDuration);
            SetSuctionSorting(progress < _absorbHideRatio);
            float remaining = Mathf.Max(Time.fixedDeltaTime, _absorbDuration - _absorbElapsed + Time.fixedDeltaTime);
            _body.MovePosition(Vector2.Lerp(_body.position, _suctionTarget.position, Time.fixedDeltaTime / remaining));
            transform.localScale = _originalScale * (1f - Mathf.SmoothStep(0f, 1f, progress));
            if (progress >= 1f)
            {
                _isPopped = true;
                OnAbsorbed?.Invoke();
                Destroy(gameObject);
            }
            return;
        }

        float speed = _body.position.y - _startY < _emergeHeight ? _emergeSpeed : _riseSpeed;
        Vector2 next = _body.position + new Vector2(_hitPushVelocityX, speed) * Time.fixedDeltaTime;
        if (_suctionTarget != null && _suctionElapsed < _suctionDuration)
        {
            float previous = Mathf.SmoothStep(0f, 1f, _suctionElapsed / _suctionDuration);
            _suctionElapsed = Mathf.Min(_suctionDuration, _suctionElapsed + Time.fixedDeltaTime);
            float current = Mathf.SmoothStep(0f, 1f, _suctionElapsed / _suctionDuration);
            next = Vector2.MoveTowards(next, _suctionTarget.position, _suctionDistance * (current - previous));
            if (Vector2.Distance(next, _suctionTarget.position) <= _absorbRadius)
            {
                _isAbsorbing = true;
                if (_collider != null) _collider.enabled = false;
                _hitPushVelocityX = 0f;
            }
        }
        _hitPushVelocityX = Mathf.MoveTowards(_hitPushVelocityX, 0f, _hitPushDeceleration * Time.fixedDeltaTime);
        _body.MovePosition(next);

        _lifeTimer += Time.fixedDeltaTime;
        if (!_isAbsorbing && (next.y >= _topY || (float.IsPositiveInfinity(_topY) && _lifeTimer >= _fallbackLifeTime)))
        {
            // 끝까지 올라간 방울은 터지지 않고 그대로 사라짐
            Destroy(gameObject);
        }
    }

    public bool TakeDamage(HitData hitData)
    {
        if (_isPopped || _isAbsorbing || hitData.Damage <= 0f)
        {
            return false;
        }

        _currentHp = Mathf.Max(0f, _currentHp - hitData.Damage);
        OnHitFeedback?.Invoke(hitData);

        if (_hitFlash != null)
        {
            _hitFlash.Flash();
        }

        if (_currentHp <= 0f)
        {
            Pop();
        }
        else
        {
            float directionX = hitData.AttackDirection.x;
            _hitPushVelocityX = Mathf.Abs(directionX) > 0.01f ? Mathf.Sign(directionX) * _hitPushSpeed : 0f;
        }

        return true;
    }

    private void SetSuctionSorting(bool inFront)
    {
        if (_sourceRenderer == null || _suctionBossRenderer == null) return;
        _sourceRenderer.sortingLayerID = _suctionBossRenderer.sortingLayerID;
        _sourceRenderer.sortingOrder = _suctionBossRenderer.sortingOrder + (inFront ? 1 : -1);
    }

    private void Pop()
    {
        _isPopped = true;
        AudioManager.Instance?.PlaySceneEvent("Boss.BubblePop", this);

        if (_popEffect != null)
        {
            Instantiate(_popEffect, transform.position, Quaternion.identity);
        }

        if (_tadpolePrefab != null)
        {
            BossBaby tadpole = Instantiate(_tadpolePrefab, transform.position, Quaternion.identity);
            tadpole.Initialize(_boss);
            AudioManager.Instance?.PlaySceneEvent("Boss.BabySpawn", tadpole);
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_boss != null)
        {
            _boss.UnregisterMinion(gameObject);
        }
    }
}
