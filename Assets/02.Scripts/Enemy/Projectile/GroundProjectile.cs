using UnityEngine;


public class GroundProjectile : Projectile

{
    protected override void Fire()

    {
        if (!_isLaunching) return;

        _t += Time.deltaTime * Speed;

        _t = Mathf.Clamp01(_t);

        Vector2 currentPosition = CalculationBezierPoint(MuzzlePosition, ControlPoint, PlayerPosition, _t);

        Vector2 direction = currentPosition - PlayerPosition;

        if (direction != Vector2.zero)

        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        transform.position = currentPosition;

        if (_t >= 1f)

        {
            _isLaunching = false;

            ReachedTarget();
        }
    }


    void ReachedTarget()

    {
        gameObject.SetActive(false);
    }


    private Vector3 CalculationBezierPoint(Vector2 p0, Vector2 p1, Vector2 p2, float t)

    {
        float u = 1 - t;

        float uu = u * u;

        float tt = t * t;

        Vector2 point = uu * p0;

        point += 2 * u * t * p1;

        point += tt * p2;

        return point;
    }
}
