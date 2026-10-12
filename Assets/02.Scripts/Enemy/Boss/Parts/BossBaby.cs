using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 올챙이 (독 방울을 터트리면 나옴). 공격 패턴 없이 좌우로 무작위로 돌아다니며 닿으면 약한 접촉 데미지.
/// 플레이어 콤보 3타에 맞으면 (그 공격으로 죽더라도) 보스에게 날아가 부딪혀 기믹 데미지.
/// 그 외 공격은 체력이 깎이고 뒤로 튕겨 나감 (일반 적보다 크게, 살짝 떠오름). 체력이 0이 되면 죽는 애니메이션 후 사라짐.
/// 레이어는 Enemy (플레이어 근접 공격에 맞도록), Rigidbody2D(Dynamic) + 콜라이더 필요.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BossBaby : MonoBehaviour, IDamageable, IHitFeedbackSource
{
    /// <summary>날려 보낸 올챙이가 보스에게 부딪힘 (소환 기믹 파훼 횟수 집계용)</summary>
    public static event System.Action<Boss> OnHitBoss;

    /// <summary>true면 보스에게 부딪혀도 데미지 없이 횟수만 셈 (궁극기 기믹 진행 중 소환 패턴이 켬)</summary>
    public static bool CountHitsOnly { get; set; }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnHitBoss = null;
        CountHitsOnly = false;
    }

    [Header("체력")]
    [SerializeField, Min(1f)]
    private float _maxHp = 60f;

    [Tooltip("생성 직후 공격을 받지 않는 시간 (초). 방울을 터트린 공격(3타 등)에 나오자마자 같이 맞지 않도록. 플레이어 공격 판정 시간보다 길게")]
    [SerializeField, Min(0f)]
    private float _spawnInvincibleTime = 0.4f;

    [Tooltip("죽을 때 생성할 이펙트 (선택)")]
    [SerializeField]
    private GameObject _deathEffect;

    [Header("이동 (좌우 무작위)")]
    [SerializeField, Min(0f)]
    private float _moveSpeed = 2f;

    [Tooltip("방향을 바꾸는 간격 (초, 최소 ~ 최대)")]
    [SerializeField]
    private Vector2 _directionChangeInterval = new Vector2(0.8f, 2f);

    [Tooltip("방향을 바꿀 때 잠깐 멈출 확률 (0 ~ 1)")]
    [SerializeField, Range(0f, 1f)]
    private float _stopChance = 0.2f;

    [Tooltip("맵 끝에서 이만큼 안쪽에서 방향을 돌림")]
    [SerializeField, Min(0f)]
    private float _edgeMargin = 0.5f;

    [Tooltip("이동 방향과 반대로 뒤집히면 체크를 바꿔 주기")]
    [SerializeField]
    private bool _spriteFacesRight = false;

    [Tooltip("0보다 크면 이 시간 뒤 사라짐 (초)")]
    [SerializeField, Min(0f)]
    private float _lifeTime = 0f;

    [Header("접촉 데미지")]
    [SerializeField, Min(0f)]
    private float _contactDamage = 1f;

    [SerializeField, Min(0f)]
    private float _contactKnockback = 3f;

    [SerializeField, Min(0.05f)]
    private float _contactInterval = 0.8f;

    [Tooltip("접촉 공격 이름 (사망 원인 표시용)")]
    [SerializeField]
    private string _contactAttackName = "몸통 박치기";

    [SerializeField]
    private AttackDefense _defense = new AttackDefense { Parryable = true, GuardGaugeCost = 5f };

    [Header("3타 기믹")]
    [Tooltip("이 콤보 단계에 맞으면 보스에게 날아감 (3 = 3타)")]
    [SerializeField, Range(1, 3)]
    private int _launchComboStep = 3;

    [SerializeField, Min(0.1f)]
    private float _launchSpeed = 16f;

    [Tooltip("보스에게 부딪혔을 때 보스가 받는 데미지")]
    [SerializeField, Min(0f)]
    private float _gimmickDamage = 40f;

    [Header("넉백 (3타가 아닌 공격)")]
    [Tooltip("맞았을 때 공격 방향으로 밀리는 가로 속도")]
    [SerializeField, Min(0f)]
    private float _nudgeSpeed = 3f;

    [Tooltip("맞았을 때 위로 튀어 오르는 속도 (0이면 바닥에서 밀리기만 함)")]
    [SerializeField, Min(0f)]
    private float _nudgeUpSpeed = 4f;

    [Tooltip("넉백 직후 최소 이 시간 동안은 스스로 움직이지 않음 (초)")]
    [SerializeField, Min(0f)]
    private float _nudgeDuration = 0.3f;

    [Tooltip("넉백 후 착지해서 미끄러질 때 가로 속도를 초당 얼마나 줄일지. 멈출 때까지 좌우 이동을 막음")]
    [SerializeField, Min(0.1f)]
    private float _knockbackGroundDeceleration = 20f;

    [Tooltip("공격의 넉백 세기(KnockbackForce)를 가로 속도에 더하는 배율 (0이면 공격과 상관없이 같은 세기)")]
    [SerializeField, Min(0f)]
    private float _attackKnockbackMultiplier = 0f;

    [SerializeField]
    private CameraShakeSettings _impactShake = new CameraShakeSettings(0.2f, 0.2f);

    [SerializeField]
    private HitStopSettings _impactHitStop = new HitStopSettings(0.06f, 0.05f);

    [Header("애니메이션 (애니메이터에 없으면 무시)")]
    [Tooltip("걷는 동안 켜 둘 Bool")]
    [SerializeField]
    private string _moveBool = "isMoving";

    [Tooltip("죽을 때 보낼 트리거")]
    [SerializeField]
    private string _dieTrigger = "die";

    [Tooltip("죽는 애니메이션 클립. 이 길이만큼 재생한 뒤 사라짐. 비우면 애니메이터에서 이름에 die가 들어간 클립을 찾음")]
    [SerializeField]
    private AnimationClip _deathClip;

    [Tooltip("죽는 클립을 찾지 못했을 때 사라지기까지 시간 (초)")]
    [SerializeField, Min(0f)]
    private float _deathFallbackDuration = 0.6f;

    [Header("상태 (확인용)")]
    [SerializeField, ReadOnly]
    private float _currentHp;

    private const float GroundNormalThreshold = 0.7f;

    private readonly List<Collider2D> _results = new List<Collider2D>();
    private readonly List<ContactPoint2D> _groundContacts = new List<ContactPoint2D>();
    private ContactFilter2D _groundFilter;
    private Rigidbody2D _body;
    private Collider2D _collider;
    private HitFlash _hitFlash;
    private ContactFilter2D _playerFilter;
    private Boss _boss;
    private float _contactTimer;
    private float _nudgeTimer;
    private bool _isKnockedBack;
    private float _directionTimer;
    private float _direction;
    private bool _isLaunched;
    private bool _isDead;
    private float _invincibleUntil;
    private Animator _animator;
    private readonly HashSet<int> _animatorParameters = new HashSet<int>();

    /// <summary>맞았을 때 (EnemyHitFeedback 피격 연출용)</summary>
    public event System.Action<HitData> OnHitFeedback;

    public void Initialize(Boss boss)
    {
        _boss = boss;
        if (_boss != null)
        {
            _boss.RegisterMinion(gameObject);
        }

        _invincibleUntil = Time.time + _spawnInvincibleTime;

        if (_lifeTime > 0f)
        {
            Destroy(gameObject, _lifeTime);
        }

        PickDirection();
    }

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _hitFlash = GetComponent<HitFlash>();
        _currentHp = _maxHp;
        _animator = GetComponentInChildren<Animator>();
        CacheAnimatorParameters();

        _playerFilter = new ContactFilter2D();
        _playerFilter.SetLayerMask(Constants.Layers.PlayerMask);
        _playerFilter.useTriggers = true;

        _groundFilter = new ContactFilter2D();
        _groundFilter.SetLayerMask(Constants.Layers.GroundAllMask);
        _groundFilter.useTriggers = false;
    }

    private void FixedUpdate()
    {
        // 죽는 애니메이션 중에는 제자리 (떠 있었으면 그대로 떨어짐)
        if (_isDead)
        {
            _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
            return;
        }

        if (_isLaunched)
        {
            FlyToBoss();
            return;
        }

        Wander();
        CheckContact();
    }

    private void PickDirection()
    {
        float min = Mathf.Max(0.1f, _directionChangeInterval.x);
        _directionTimer = Random.Range(min, Mathf.Max(min, _directionChangeInterval.y));
        _direction = Random.value < _stopChance ? 0f : (Random.value < 0.5f ? -1f : 1f);
    }

    private void Wander()
    {
        if (_nudgeTimer > 0f)
        {
            _nudgeTimer -= Time.fixedDeltaTime;
            SetMoving(false);
            return;
        }

        bool isGrounded = IsGrounded();

        // 넉백 중에는 좌우 이동 금지: 공중이면 밀리는 속도 유지, 착지하면 미끄러지다 멈출 때까지 대기
        if (_isKnockedBack)
        {
            SetMoving(false);
            if (!isGrounded)
            {
                return;
            }

            float slideX = Mathf.MoveTowards(_body.linearVelocity.x, 0f, _knockbackGroundDeceleration * Time.fixedDeltaTime);
            _body.linearVelocity = new Vector2(slideX, _body.linearVelocity.y);
            if (Mathf.Abs(slideX) > 0.05f)
            {
                return;
            }

            // 다 멈췄으면 새 방향을 골라 다시 돌아다님
            _isKnockedBack = false;
            PickDirection();
        }

        // 방울에서 나와 떨어지는 동안(공중)은 움직이지 않고 그대로 낙하
        if (!isGrounded)
        {
            _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
            SetMoving(false);
            return;
        }

        _directionTimer -= Time.fixedDeltaTime;
        if (_directionTimer <= 0f)
        {
            PickDirection();
        }

        // 맵 끝에 닿으면 반대로
        BossArena arena = _boss != null ? _boss.Arena : null;
        if (arena != null)
        {
            float x = _body.position.x;
            if (x <= arena.LeftX + _edgeMargin && _direction < 0f)
            {
                _direction = 1f;
            }
            else if (x >= arena.RightX - _edgeMargin && _direction > 0f)
            {
                _direction = -1f;
            }
        }

        _body.linearVelocity = new Vector2(_direction * _moveSpeed, _body.linearVelocity.y);
        SetMoving(_direction != 0f && _moveSpeed > 0f);

        if (_direction != 0f)
        {
            Vector3 scale = transform.localScale;
            float sign = _spriteFacesRight ? -_direction : _direction;
            scale.x = Mathf.Abs(scale.x) * sign;
            transform.localScale = scale;
        }
    }

    /// <summary>땅(Ground / OneWayPlatform)을 밟고 있는지</summary>
    private bool IsGrounded()
    {
        int count = _body.GetContacts(_groundFilter, _groundContacts);
        for (int i = 0; i < count; i++)
        {
            if (_groundContacts[i].normal.y > GroundNormalThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private void CheckContact()
    {
        if (_contactTimer > 0f)
        {
            _contactTimer -= Time.fixedDeltaTime;
            return;
        }

        if (_collider == null || _contactDamage <= 0f)
        {
            return;
        }

        _results.Clear();
        int count = Physics2D.OverlapCollider(_collider, _playerFilter, _results);

        for (int i = 0; i < count; i++)
        {
            Collider2D other = _results[i];
            if (other.GetComponentInParent<Player>() == null)
            {
                continue;
            }

            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null)
            {
                continue;
            }

            Vector2 direction = ((Vector2)other.bounds.center - (Vector2)transform.position).normalized;
            var hitData = new HitData(
                _contactDamage,
                _contactKnockback,
                other.ClosestPoint(transform.position),
                direction,
                _defense,
                attackName: _contactAttackName
            );
            hitData.Attacker = gameObject;

            damageable.TakeDamage(hitData);
            _contactTimer = _contactInterval;
            return;
        }
    }

    public bool TakeDamage(HitData hitData)
    {
        if (_isLaunched || _isDead)
        {
            return false;
        }

        // 방울을 터트린 그 공격에 같이 맞지 않도록 생성 직후 잠깐 무적
        if (Time.time < _invincibleUntil)
        {
            return false;
        }

        if (_hitFlash != null)
        {
            _hitFlash.Flash();
        }

        _currentHp = Mathf.Max(0f, _currentHp - hitData.Damage);
        OnHitFeedback?.Invoke(hitData);

        // 3타는 죽든 말든 보스에게 날아감
        if (hitData.ComboStep == _launchComboStep)
        {
            Launch();
            return true;
        }

        if (_currentHp <= 0f)
        {
            Die();
            return true;
        }

        // 그 외 공격은 뒤로 튕겨 나감 (위로 살짝 떠서 바닥 마찰에 덜 막힘)
        float pushX = hitData.AttackDirection.x >= 0f ? 1f : -1f;
        float speedX = _nudgeSpeed + (Mathf.Max(0f, hitData.KnockbackForce) * _attackKnockbackMultiplier);
        float speedY = Mathf.Max(_body.linearVelocity.y, _nudgeUpSpeed);
        _body.linearVelocity = new Vector2(pushX * speedX, speedY);
        _nudgeTimer = _nudgeDuration;
        _isKnockedBack = true;
        return true;
    }

    private void Die()
    {
        _isDead = true;

        if (_deathEffect != null)
        {
            Instantiate(_deathEffect, transform.position, Quaternion.identity);
        }

        // 죽는 애니메이션을 끝까지 보여준 뒤 사라짐 (그동안 데미지 / 이동 / 접촉 데미지 없음)
        _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
        SetMoving(false);
        SetTrigger(_dieTrigger);
        Destroy(gameObject, GetDeathDuration());
    }

    private float GetDeathDuration()
    {
        AnimationClip clip = _deathClip;
        if (clip == null && _animator != null && _animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip candidate in _animator.runtimeAnimatorController.animationClips)
            {
                if (candidate != null && candidate.name.ToLower().Contains("die"))
                {
                    clip = candidate;
                    break;
                }
            }
        }

        if (clip == null || _animator == null || !TryGetParameter(_dieTrigger, out _))
        {
            return _deathFallbackDuration;
        }

        float speed = _animator.speed > 0f ? _animator.speed : 1f;
        return clip.length / speed;
    }

    #region 애니메이터

    private void CacheAnimatorParameters()
    {
        _animatorParameters.Clear();
        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            _animatorParameters.Add(parameter.nameHash);
        }
    }

    private bool TryGetParameter(string parameter, out int hash)
    {
        hash = 0;
        if (_animator == null || string.IsNullOrEmpty(parameter))
        {
            return false;
        }

        hash = Animator.StringToHash(parameter);
        return _animatorParameters.Contains(hash);
    }

    private void SetMoving(bool moving)
    {
        if (TryGetParameter(_moveBool, out int hash))
        {
            _animator.SetBool(hash, moving);
        }
    }

    private void SetTrigger(string parameter)
    {
        if (TryGetParameter(parameter, out int hash))
        {
            _animator.SetTrigger(hash);
        }
    }

    #endregion

    private void Launch()
    {
        if (_boss == null || !_boss.isActiveAndEnabled)
        {
            Die();
            return;
        }

        _isLaunched = true;
        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.linearVelocity = Vector2.zero;

        if (_collider != null)
        {
            _collider.isTrigger = true;
        }
    }

    private void FlyToBoss()
    {
        if (_boss == null || !_boss.isActiveAndEnabled)
        {
            Destroy(gameObject);
            return;
        }

        Vector2 target = _boss.CenterPosition;
        var next = Vector2.MoveTowards(_body.position, target, _launchSpeed * Time.fixedDeltaTime);
        _body.MovePosition(next);

        // 회전하며 날아감
        transform.Rotate(0f, 0f, 720f * Time.fixedDeltaTime);

        if ((next - target).sqrMagnitude <= 0.04f)
        {
            HitBoss();
        }
    }

    private void HitBoss()
    {
        if (CountHitsOnly)
        {
            // 기믹 진행 중: 데미지 없이 맞춘 횟수만 (타격감은 그대로)
            CameraShake.Shake(_impactShake);
            HitStop.Play(_impactHitStop);
        }
        else if (_boss.Health.TakeGimmickDamage(_gimmickDamage))
        {
            CameraShake.Shake(_impactShake);
            HitStop.Play(_impactHitStop);
        }

        OnHitBoss?.Invoke(_boss);

        // 부딪힌 자리에서 회전을 멈추고 죽는 애니메이션 후 사라짐
        transform.rotation = Quaternion.identity;
        Die();
    }

    private void OnDestroy()
    {
        if (_boss != null)
        {
            _boss.UnregisterMinion(gameObject);
        }
    }
}
