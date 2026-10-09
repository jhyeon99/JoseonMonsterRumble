using UnityEngine;

/// <summary>
/// 독 방울 (소환 패턴이 맵 아래에 생성).
/// 처음엔 빠르게 솟아오르고, 이후 천천히 계속 위로 올라가다 끝 높이에 닿으면 그대로 사라짐.
/// 체력이 0이 되면 터지면서 올챙이(BossBaby)가 나옴.
/// 레이어는 Enemy (플레이어 공격에 맞도록), Rigidbody2D + 콜라이더 필요 (Rigidbody2D는 자동으로 Kinematic).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BossPoisonBubble : MonoBehaviour, IDamageable
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
    }

    private void FixedUpdate()
    {
        if (_isPopped)
        {
            return;
        }

        float speed = _body.position.y - _startY < _emergeHeight ? _emergeSpeed : _riseSpeed;
        Vector2 next = _body.position + (Vector2.up * (speed * Time.fixedDeltaTime));
        _body.MovePosition(next);

        _lifeTimer += Time.fixedDeltaTime;
        if (next.y >= _topY || (float.IsPositiveInfinity(_topY) && _lifeTimer >= _fallbackLifeTime))
        {
            // 끝까지 올라간 방울은 터지지 않고 그대로 사라짐
            Destroy(gameObject);
        }
    }

    public bool TakeDamage(HitData hitData)
    {
        if (_isPopped || hitData.Damage <= 0f)
        {
            return false;
        }

        _currentHp = Mathf.Max(0f, _currentHp - hitData.Damage);
        if (_hitFlash != null)
        {
            _hitFlash.Flash();
        }

        if (_currentHp <= 0f)
        {
            Pop();
        }

        return true;
    }

    private void Pop()
    {
        _isPopped = true;

        if (_popEffect != null)
        {
            Instantiate(_popEffect, transform.position, Quaternion.identity);
        }

        if (_tadpolePrefab != null)
        {
            BossBaby tadpole = Instantiate(_tadpolePrefab, transform.position, Quaternion.identity);
            tadpole.Initialize(_boss);
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
