using UnityEngine;

public class SelfDestructState : EnemyState
{
    private enum Phase
    {
        Retreat,
        Approach,
        Exploded
    }

    private const float RetreatDuration = 0.6f;
    private const float DefaultDetonationDelay = 3f;
    private const float ChargeSpeed = 8.5f;
    private const float ApproachSmoothTime = 0.2f;

    private const float DesiredRetreatDistance = 1.5f;
    private const float BodyRadius = 0.35f;
    private const float MaxScaleMultiplier = 1.25f;
    private const float InitialFlashFrequency = 1f;
    private const float FinalFlashFrequency = 6f;
    private const float FallbackCenterOffsetY = 0.75f;

    private readonly Enemy _enemy;
    private readonly BuffEnemy _buffEnemy;

    private SpriteRenderer _spriteRenderer;
    private Color _originalColor = Color.white;
    private Vector3 _originalScale = Vector3.one;
    private Collider2D _targetCollider;

    private Vector2 _startPosition;
    private Vector2 _targetPosition;
    private Vector2 _approachVelocity;
    private float _timer;
    private Phase _currentPhase;
    private float _detonationDelay;

    public SelfDestructState(Enemy enemy)
    {
        _enemy = enemy;
        _buffEnemy = enemy as BuffEnemy;
    }

    public override void Enter()
    {
        if (_buffEnemy != null && _buffEnemy.VisualLink != null)
        {
            _buffEnemy.VisualLink.StopLink();
        }

        _enemy.DirectionLock.UnlockDirection();

        _spriteRenderer = _enemy.GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
            _originalScale = _spriteRenderer.transform.localScale;
        }

        if (_enemy.Target != null)
        {
            _targetCollider = _enemy.Target.GetComponent<Collider2D>();
        }

        _startPosition = _enemy.transform.position;
        Vector2 awayDirection = CalculateAwayDirection();
        float retreatDistance = CalculateSafeRetreatDistance(awayDirection);
        _targetPosition = _startPosition + (awayDirection * retreatDistance);

        _timer = 0f;
        _approachVelocity = Vector2.zero;
        _currentPhase = Phase.Retreat;
        _detonationDelay = _buffEnemy != null ? _buffEnemy.SelfDestructDelay : DefaultDetonationDelay;
    }

    private Vector2 GetTargetCenterPosition()
    {
        if (_targetCollider != null)
        {
            return _targetCollider.bounds.center;
        }

        if (_enemy.Target != null)
        {
            return (Vector2)_enemy.Target.position + (Vector2.up * FallbackCenterOffsetY);
        }

        return _enemy.transform.position;
    }

    private Vector2 CalculateAwayDirection()
    {
        Vector2 targetCenter = GetTargetCenterPosition();
        Vector2 diff = _startPosition - targetCenter;
        float dirX = diff.x;

        if (Mathf.Abs(dirX) < 0.01f)
        {
            dirX = _enemy.transform.localScale.x > 0f ? -1f : 1f;
        }
        else
        {
            dirX = Mathf.Sign(dirX);
        }

        var awayDir = new Vector2(dirX, 0.45f);
        return awayDir.normalized;
    }

    private float CalculateSafeRetreatDistance(Vector2 direction)
    {
        int groundMask = Constants.Layers.GroundMask;
        RaycastHit2D hit = Physics2D.CircleCast(_startPosition, BodyRadius, direction, DesiredRetreatDistance, groundMask);

        if (hit.collider != null)
        {
            return Mathf.Max(0.2f, hit.distance - 0.15f);
        }

        return DesiredRetreatDistance;
    }

    public override void Update()
    {
        if (_currentPhase == Phase.Exploded)
        {
            return;
        }

        _timer += Time.deltaTime;
        UpdateWarningTelegraph();

        if (_timer >= _detonationDelay)
        {
            _currentPhase = Phase.Exploded;
            _approachVelocity = Vector2.zero;
            _enemy.EnemyRb.linearVelocity = Vector2.zero;
            _enemy.Combat.Attack();
            _enemy.Health.Kill();
            return;
        }

        if (_currentPhase == Phase.Retreat && _timer >= RetreatDuration)
        {
            _currentPhase = Phase.Approach;
        }
    }

    private void UpdateWarningTelegraph()
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        float elapsed = Mathf.Min(_timer, _detonationDelay);
        float warningProgress = Mathf.Clamp01(elapsed / _detonationDelay);
        // 주파수의 적분으로 위상을 누적해 점멸 속도만 연속적으로 높인다.
        float flashCycles = InitialFlashFrequency * elapsed +
            (FinalFlashFrequency - InitialFlashFrequency) * elapsed * warningProgress * 0.5f;
        bool isFlash = Mathf.Sin(flashCycles * Mathf.PI * 2f) >= 0f;

        // 사령관 지시: 자폭 사인을 붉은색(Color.red)으로 점멸
        _spriteRenderer.color = isFlash ? Color.red : _originalColor;

        float baseScaleMultiplier = Mathf.Lerp(1f, MaxScaleMultiplier, warningProgress);
        float pulseBoost = isFlash ? 1.08f : 1f;
        _spriteRenderer.transform.localScale = _originalScale * (baseScaleMultiplier * pulseBoost);
    }

    public override void FixedUpdate()
    {
        if (_currentPhase == Phase.Retreat)
        {
            float progress = Mathf.Clamp01(_timer / RetreatDuration);
            float smoothProgress = Mathf.Sin(progress * Mathf.PI * 0.5f);
            var nextPos = Vector2.Lerp(_startPosition, _targetPosition, smoothProgress);

            _enemy.EnemyRb.MovePosition(nextPos);
            _enemy.EnemyRb.linearVelocity = Vector2.zero;

            float moveDirX = _targetPosition.x - _startPosition.x;
            Flip(moveDirX);
        }
        else if (_currentPhase == Phase.Approach)
        {
            Vector2 currentPosition = _enemy.EnemyRb.position;
            Vector2 offset = GetTargetCenterPosition() - currentPosition;
            float distance = offset.magnitude;
            Vector2 direction = offset.normalized;
            Vector2 targetPosition = currentPosition + direction * Mathf.Max(0f, distance - BodyRadius);
            var nextPosition = Vector2.SmoothDamp(currentPosition, targetPosition, ref _approachVelocity,
                ApproachSmoothTime, ChargeSpeed, Time.fixedDeltaTime);
            _enemy.EnemyRb.linearVelocity = (nextPosition - currentPosition) / Time.fixedDeltaTime;
            Flip(_enemy.EnemyRb.linearVelocity.x);
        }
    }

    private void Flip(float directionX)
    {
        if (Mathf.Abs(directionX) < 0.01f)
        {
            return;
        }

        Vector3 scale = _enemy.transform.localScale;
        scale.x = directionX > 0f ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
        _enemy.transform.localScale = scale;
    }

    public override void Exit()
    {
        _approachVelocity = Vector2.zero;
        _enemy.EnemyRb.linearVelocity = Vector2.zero;
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _originalColor;
            _spriteRenderer.transform.localScale = _originalScale;
        }
    }
}
