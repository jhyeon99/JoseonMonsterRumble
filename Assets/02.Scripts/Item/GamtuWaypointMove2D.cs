using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GamtuWaypointMove2D : MonoBehaviour
{
    [Header("플레이어 및 접근 거리 설정")]

    [SerializeField] private Transform _playerTransform; // 인스펙터에서 플레이어 할당
    [SerializeField] private float _triggerDistance = 3f; // 이동을 시작할 플레이어 접근 거리

    [Header("이동 설정")]

    [SerializeField] private float _moveDuration = 0.8f; // 각 포인트 간 이동 시간(초)
    [SerializeField] private AnimationCurve _speedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("경로 설정")]

    [Tooltip("비워두면 자식 Empty 오브젝트들의 위치를 순서대로 자동 등록합니다.")]
    [SerializeField]
    private List<Transform> _waypoints = new List<Transform>();

    private readonly List<Vector2> _targetPositions = new List<Vector2>();
    private int _currentIndex = 0;
    private bool _isMoving = false;
    private bool _hasFinished = false;

    private void Start()
    {
        if (_waypoints == null || _waypoints.Count == 0)
        {
            _waypoints = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (child != transform)
                {
                    _waypoints.Add(child);
                }
            }
        }

        foreach (Transform wp in _waypoints)
        {
            if (wp != null)
            {
                _targetPositions.Add(wp.position);
            }
        }
    }

    private void Update()
    {
        if (_hasFinished || _isMoving || _playerTransform == null || _targetPositions.Count == 0) return;

        float distance = Vector2.Distance(transform.position, _playerTransform.position);

        if (distance <= _triggerDistance)
        {
            StartCoroutine(MoveToNextPointRoutine());
        }
    }

    private IEnumerator MoveToNextPointRoutine()
    {
        _isMoving = true;

        Vector2 startPos = transform.position;
        Vector2 targetPos = _targetPositions[_currentIndex];

        float elapsedTime = 0f;

        while (elapsedTime < _moveDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / _moveDuration);
            float curveProgress = _speedCurve.Evaluate(normalizedTime);

            transform.position = Vector2.Lerp(startPos, targetPos, curveProgress);

            yield return null;
        }

        transform.position = targetPos;

        _currentIndex++;

        if (_currentIndex >= _targetPositions.Count)
        {
            _hasFinished = true;
        }

        _isMoving = false;
    }
}
