using System.Collections;
using UnityEngine;

/// <summary>
/// 테스트용 공격 오브젝트. 씬에 놓으면 일정 간격으로 범위 안의 플레이어를 공격한다.
/// 가드 / 패링 / 가드 불가 공격을 실제 적 패턴 없이 확인하기 위한 용도 (실제 게임에서는 사용하지 않음).
/// 공격 직전에 색이 바뀜: 노랑 = 패링 가능, 빨강 = 가드 불가, 하양 = 가드만 가능
/// 패링: 예고 색을 보고 맞기 직전에 아래 방향키로 가드를 시작 (패링 판정 시간은 PlayerGuard / AttackDefense)
/// </summary>
public class ParryTestAttacker : MonoBehaviour, IParryable
{
    [Header("공격")]
    [SerializeField, Min(0.1f)] private float _interval = 2f;

    [Tooltip("공격 예고 시간 (색이 바뀐 뒤 실제로 맞을 때까지)")]
    [SerializeField, Min(0f)]
    private float _windup = 0.6f;

    [SerializeField, Min(0f)] private float _range = 2.5f;
    [SerializeField, Min(0f)] private float _damage = 10f;
    [SerializeField, Min(0f)] private float _knockbackForce = 4f;

    [Header("가드 / 패링")]
    [SerializeField]
    private AttackDefense _defense = new AttackDefense { Parryable = true };

    [Header("표시")]
    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField] private Color _idleColor = Color.gray;
    [SerializeField] private Color _parryableColor = Color.yellow;
    [SerializeField] private Color _unblockableColor = Color.red;
    [SerializeField] private Color _blockOnlyColor = Color.white;
    [SerializeField] private Color _parriedColor = Color.cyan;

    private Player _player;
    private Coroutine _loop;

    private void Awake()
    {
        if (_renderer == null)
        {
            _renderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void OnEnable()
    {
        _loop = StartCoroutine(AttackLoop());
    }

    private void OnDisable()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
    }

    private IEnumerator AttackLoop()
    {
        while (true)
        {
            SetColor(_idleColor);
            yield return new WaitForSeconds(_interval);

            SetColor(GetWindupColor());
            yield return new WaitForSeconds(_windup);

            Attack();
            yield return new WaitForSeconds(0.15f);
        }
    }

    private void Attack()
    {
        if (_player == null)
        {
            _player = FindFirstObjectByType<Player>();
        }

        if (_player == null)
        {
            return;
        }

        Vector2 origin = transform.position;
        Vector2 target = _player.transform.position;
        if (Vector2.Distance(origin, target) > _range)
        {
            return;
        }

        var hitData = new HitData(
            _damage,
            _knockbackForce,
            target,
            (target - origin).normalized,
            _defense,
            this
        );

        bool damaged = _player.Health.TakeDamage(hitData);
        Debug.Log($"[ParryTestAttacker] 공격 → {(damaged ? "피격" : "막힘 / 무적")}", this);
    }

    public void OnParried(HitData hitData, Transform parrier)
    {
        SetColor(_parriedColor);
        Debug.Log("[ParryTestAttacker] 패링당함", this);
    }

    private Color GetWindupColor()
    {
        if (_defense.Unblockable)
        {
            return _unblockableColor;
        }

        return _defense.Parryable ? _parryableColor : _blockOnlyColor;
    }

    private void SetColor(Color color)
    {
        if (_renderer != null)
        {
            _renderer.color = color;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _range);
    }
}
