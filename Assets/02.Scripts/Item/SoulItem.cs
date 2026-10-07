using System.Collections;
using UnityEngine;

public class SoulItem : Item
{
    [SerializeField] private float _soulAmount;

    [SerializeField] private float _waitTime;
    [SerializeField] private float _absorbDuration;
    [SerializeField] private float _arcHeight;
    [SerializeField] private float _randomRange;

    private Transform _playerTransform;
    private bool _isWaiting;

    private float _absorbTimer;
    private Vector2 _originPosition;
    private Vector2 _controlPoint;

    private void Update()
    {
        if (_playerTransform == null || _isWaiting) return;

        _absorbTimer += Time.deltaTime;
        float bezierTime = Mathf.Clamp(_absorbTimer / _absorbDuration, 0f, 1f);

        transform.position = Bezier(_originPosition, _controlPoint, _playerTransform.position, bezierTime);
    }

    private IEnumerator WaitRoutine(float duration)
    {
        _isWaiting = true;
        yield return new WaitForSeconds(duration);
        _isWaiting = false;
        _originPosition = transform.position;
        _controlPoint = (Vector2)transform.position + Vector2.up * _arcHeight +
                        (Vector2)Random.insideUnitSphere * _randomRange;
    }

    Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        var a = Vector3.Lerp(p0, p1, t);
        var b = Vector3.Lerp(p1, p2, t);
        return Vector3.Lerp(a, b, t);
    }

    public override void OnSpawn()
    {
        _playerTransform = FindAnyObjectByType<Player>().transform;
        _isWaiting = true;
        _absorbTimer = 0;
        StartCoroutine(WaitRoutine(_waitTime));
    }

    public override void OnCollect(Player player)
    {
        player.Soul.AddSoul(_soulAmount);
    }
}