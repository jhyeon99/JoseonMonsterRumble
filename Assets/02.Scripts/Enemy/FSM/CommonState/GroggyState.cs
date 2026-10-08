using UnityEngine;

/// <summary>
/// 패링당해 무너진 상태. 일정 시간 동안 움직이지 않고 공격하지 않음 (깜빡임으로 표시).
/// 이 상태의 적 앞에서 공격키를 누르면 플레이어 마무리 공격 (PlayerFinisher).
/// </summary>
public class GroggyState : EnemyState
{
    private const float BlinkInterval = 0.08f;
    private static readonly Color BlinkColor = new Color(1f, 0.95f, 0.4f, 1f);

    private readonly Enemy _enemy;
    private readonly float _duration;

    private float _timer;
    private bool _isHeld;

    private SpriteRenderer _renderer;
    private Color _originalColor;
    private float _blinkTimer;
    private bool _isBlinkOn;

    public GroggyState(Enemy enemy, float duration)
    {
        _enemy = enemy;
        _duration = duration;
    }

    /// <summary>마무리 공격 준비 중에는 시간이 지나도 그로기가 풀리지 않게 고정</summary>
    public void Hold() => _isHeld = true;

    public void Release() => _isHeld = false;

    public override void Enter()
    {
        Debug.Log("적 그로기!");
        _timer = _duration;
        _isHeld = false;

        _enemy.MoveType.StopMove();
        _enemy.DirectionLock?.LockDirection();
        _enemy.Animation.SetRunning(false);
        _enemy.Animation.SetGroggy(true);

        _renderer = _enemy.GetComponentInChildren<SpriteRenderer>();
        if (_renderer != null)
        {
            _originalColor = _renderer.color;
        }

        _blinkTimer = 0f;
        _isBlinkOn = false;
    }

    public override void FixedUpdate()
    {
        _enemy.MoveType.StopMove();
    }

    public override void Update()
    {
        UpdateBlink();

        if (_isHeld)
        {
            return;
        }

        _timer -= Time.deltaTime;
        if (_timer > 0f)
        {
            return;
        }

        if (_enemy.Type == EnemyType.Buffer)
        {
            _enemy.StateMachine.ChangeState(new FindEnemyState(_enemy));
            return;
        }

        _enemy.StateMachine.ChangeState(new ChaseState(_enemy));
    }

    public override void Exit()
    {
        Debug.Log("적 그로기 종료");
        RestoreColor();
        _enemy.Animation.SetGroggy(false);
    }

    private void UpdateBlink()
    {
        if (_renderer == null)
        {
            return;
        }

        _blinkTimer -= Time.deltaTime;
        if (_blinkTimer > 0f)
        {
            return;
        }

        _blinkTimer = BlinkInterval;
        _isBlinkOn = !_isBlinkOn;

        Color color = _isBlinkOn ? BlinkColor : _originalColor;
        color.a = _renderer.color.a;
        _renderer.color = color;
    }

    private void RestoreColor()
    {
        if (_renderer == null)
        {
            return;
        }

        Color color = _originalColor;
        color.a = _renderer.color.a;
        _renderer.color = color;
    }
}
