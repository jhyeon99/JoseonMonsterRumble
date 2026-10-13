using UnityEngine;

public class EnemyRotation : MonoBehaviour
{
    [SerializeField] private float _rangedMaxPitchAngle = 30f;
    private Enemy _enemy;
    private bool? _wasRight = null; // 이전 프레임의 플레이어 X축 방향 추적
    private FlyingMeleeAttackController _flyingMeleeAttack;


    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _flyingMeleeAttack = GetComponent<FlyingMeleeAttackController>();
    }

    private void Update()
    {
        Rotation();
    }

    private void Rotation()
    {
        if (_enemy == null || _enemy.Target == null) return;

        if (_flyingMeleeAttack != null)
        {
            _flyingMeleeAttack.FaceDirection(_enemy.GetDirectionToPlayer());
            return;
        }

        Vector2 relPos = transform.position - _enemy.Target.position;
        if (relPos.sqrMagnitude < 0.0001f) return;

        Vector2 direction = _enemy.GetDirectionToPlayer();
        bool isRight = direction.x >= 0f;

        if (!_wasRight.HasValue)
        {
            _wasRight = isRight;
        }

        float currentAngle = Mathf.Atan2(relPos.y, relPos.x) * Mathf.Rad2Deg;

        bool isAllowed = (currentAngle >= -_enemy.RightAngle && currentAngle <= _enemy.RightAngle) ||
                         (currentAngle >= _enemy.LeftAngle || currentAngle <= -_enemy.LeftAngle);

        if (isAllowed)
        {
            float angle = Vector2.SignedAngle(Vector2.right, direction);

            if (_enemy.Combat is EnemyRangedCombat)
            {
                if (isRight)
                {
                    float delta = Mathf.DeltaAngle(0f, angle);
                    float clampedDelta = Mathf.Clamp(delta, -_rangedMaxPitchAngle, _rangedMaxPitchAngle);
                    transform.rotation = Quaternion.Euler(0f, 0f, clampedDelta - 164f);
                }
                else
                {
                    float delta = Mathf.DeltaAngle(180f, angle);
                    float clampedDelta = Mathf.Clamp(delta, -_rangedMaxPitchAngle, _rangedMaxPitchAngle);
                    transform.rotation = Quaternion.Euler(0f, 0f, clampedDelta - 16f);
                }
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, 0f, angle - 180f);
            }

            _wasRight = isRight;
        }
        else
        {
            if (_wasRight.Value != isRight)
            {
                float currentZ = transform.eulerAngles.z;
                float targetZ = 180f - currentZ;

                transform.rotation = Quaternion.Euler(0f, 0f, targetZ);
                _wasRight = isRight;
            }
        }
    }

    public void ForceRotateToPlayer()
    {
        if (_enemy == null || _enemy.Target == null) return;

        if (_flyingMeleeAttack != null)
        {
            _flyingMeleeAttack.FaceDirection(_enemy.GetDirectionToPlayer());
            return;
        }

        Vector2 direction = _enemy.GetDirectionToPlayer();
        if (direction.sqrMagnitude < 0.0001f) return;

        bool isRight = direction.x >= 0f;
        float angle = Vector2.SignedAngle(Vector2.right, direction);

        transform.rotation = Quaternion.Euler(0f, 0f, angle - 180f);

        _wasRight = isRight;
    }
}
