using UnityEngine;

[RequireComponent(typeof(Player))]
public class PlayerMovementEffects : MonoBehaviour
{
    private enum GroundState
    {
        Uninitialized,
        Grounded,
        Airborne,
    }

    [Tooltip("걷기 먼지 기준점. 뒤꿈치에 배치합니다.")]
    [SerializeField] private Transform _feet;
    [Tooltip("착지 먼지 기준점. 두 발 사이의 바닥에 배치합니다.")]
    [SerializeField] private Transform _landingDustPoint;
    [Tooltip("달릴 때 먼지를 한 번 재생할 이동 거리")]
    [SerializeField, Min(0.01f)] private float _walkStepDistance = 1.2f;
    [Tooltip("짧은 접지 흔들림을 착지로 처리하지 않을 최소 공중 시간")]
    [SerializeField, Min(0f)] private float _minimumAirTime = 0.06f;

    private Player _player;
    private EffectManager _effects;
    private GroundState _groundState;
    private float _previousPositionX;
    private float _walkDistance;
    private float _airTime;

    private void Awake()
    {
        _player = GetComponent<Player>();
        if (_feet == null || _landingDustPoint == null)
        {
            Debug.LogWarning("[PlayerMovementEffects] Feet 또는 Landing Dust Point 참조가 없습니다.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        _effects = EffectManager.Instance;
        if (_effects == null)
        {
            Debug.LogWarning("[PlayerMovementEffects] EffectManager가 없어 이동 먼지를 재생할 수 없습니다.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        _groundState = GroundState.Uninitialized;
        _previousPositionX = transform.position.x;
        _walkDistance = 0f;
        _airTime = 0f;
    }

    private void LateUpdate()
    {
        float currentPositionX = transform.position.x;
        float distance = Mathf.Abs(currentPositionX - _previousPositionX);
        _previousPositionX = currentPositionX;

        if (_player.Health.IsDead || Time.deltaTime <= 0f)
        {
            _groundState = GroundState.Uninitialized;
            _walkDistance = 0f;
            _airTime = 0f;
            return;
        }

        bool isGrounded = _player.IsGrounded;
        UpdateLandingEffect(isGrounded);

        if (!isGrounded || _player.StateMachine.CurrentState != _player.MoveState)
        {
            _walkDistance = 0f;
            return;
        }

        _walkDistance += distance;
        if (_walkDistance < _walkStepDistance)
        {
            return;
        }

        _walkDistance %= _walkStepDistance;
        _effects.PlayWalkDust(_feet.position, transform.lossyScale.x < 0f);
    }

    private void UpdateLandingEffect(bool isGrounded)
    {
        if (_groundState == GroundState.Uninitialized)
        {
            _groundState = isGrounded ? GroundState.Grounded : GroundState.Airborne;
            return;
        }

        if (!isGrounded)
        {
            _groundState = GroundState.Airborne;
            _airTime += Time.deltaTime;
            return;
        }

        if (_groundState == GroundState.Airborne && _airTime >= _minimumAirTime)
        {
            _effects.PlayLandingDust(_landingDustPoint.position);
            _walkDistance = 0f;
        }

        _groundState = GroundState.Grounded;
        _airTime = 0f;
    }
}
