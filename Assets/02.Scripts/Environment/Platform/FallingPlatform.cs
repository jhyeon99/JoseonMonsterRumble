using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FallingPlatform : MonoBehaviour
{
    private static readonly int BreakStateHash = Animator.StringToHash("Break");

    [SerializeField] private Rigidbody2D _platformBody;
    [SerializeField] private Collider2D _solidCollider;
    [SerializeField] private Transform _visual;
    [SerializeField] private Animator _animator;
    [SerializeField, Min(0f)] private float _fallDelay = 0.8f;
    [SerializeField, Min(0f)] private float _fallGravityScale = 2f;
    [SerializeField, Min(0f)] private float _respawnDelay = 3f;
    [SerializeField, ReadOnly] private PlatformState _state;

    private Vector2 _startPosition;
    private float _startRotation;
    private Vector3 _visualStartLocalPosition;
    private Coroutine _fallRoutine;
    private GameAudio.EventAudio _audio;

    private enum PlatformState
    {
        Idle,
        Warning,
        Falling,
    }

    private void Awake()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        if (_animator == null)
        {
            Debug.LogWarning($"{name}: Animator 참조가 없어 파괴 애니메이션 없이 동작합니다.", this);
        }

        _startPosition = _platformBody.position;
        _startRotation = _platformBody.rotation;
        _visualStartLocalPosition = _visual.localPosition;

        RestorePlatform();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        Rigidbody2D playerBody = collision.rigidbody;

        if (!enabled
            || _state != PlatformState.Idle
            || !collision.enabled
            || playerBody == null
            || playerBody.gameObject.layer != Constants.Layers.Player)
        {
            return;
        }

        _state = PlatformState.Warning;
        _fallRoutine = StartCoroutine(FallRoutine());
    }

    private bool HasRequiredReferences()
    {
        bool hasRequiredReferences = true;

        if (_platformBody == null)
        {
            Debug.LogWarning($"{name}: Platform Body 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        if (_solidCollider == null)
        {
            Debug.LogWarning($"{name}: Solid Collider 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        if (_visual == null)
        {
            Debug.LogWarning($"{name}: Visual 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        return hasRequiredReferences;
    }

    public void ResetPlatform()
    {
        if (!enabled)
        {
            return;
        }

        if (_fallRoutine != null)
        {
            StopCoroutine(_fallRoutine);
            _fallRoutine = null;
        }

        if (_audio != null) _audio.Cancel();
        RestorePlatform();
    }

    private IEnumerator FallRoutine()
    {
        _audio = GameAudio.EventAudio.For(this, "Environment");
        _audio.Begin();
        _audio.Play("Environment.FallingWarning");
        PlayBreakAnimation();

        yield return new WaitForSeconds(_fallDelay);

        _audio.Complete();
        AudioManager.Instance?.PlaySceneEvent("Environment.FallingBreak", this);
        StartFalling();

        yield return new WaitForSeconds(_respawnDelay);

        _fallRoutine = null;
        RestorePlatform();
    }

    private void OnDisable()
    {
        if (_audio != null) _audio.Cancel();
    }

    private void PlayBreakAnimation()
    {
        if (_animator == null)
        {
            return;
        }

        _animator.enabled = true;
        _animator.Play(BreakStateHash, 0, 0f);
    }

    private void StartFalling()
    {
        _state = PlatformState.Falling;
        _solidCollider.enabled = false;

        if (_animator != null)
        {
            _animator.enabled = false;
        }

        _visual.localPosition = _visualStartLocalPosition;
        _platformBody.bodyType = RigidbodyType2D.Dynamic;
        _platformBody.gravityScale = _fallGravityScale;
        _platformBody.linearVelocity = Vector2.zero;
        _platformBody.angularVelocity = 0f;
        _platformBody.WakeUp();
    }

    private void RestorePlatform()
    {
        _solidCollider.enabled = false;
        _platformBody.simulated = true;
        _platformBody.bodyType = RigidbodyType2D.Kinematic;
        _platformBody.linearVelocity = Vector2.zero;
        _platformBody.angularVelocity = 0f;
        _platformBody.gravityScale = 0f;
        _platformBody.position = _startPosition;
        _platformBody.rotation = _startRotation;

        if (_animator != null)
        {
            _animator.enabled = false;
        }

        _visual.localPosition = _visualStartLocalPosition;
        _solidCollider.enabled = true;
        _state = PlatformState.Idle;
    }
}
