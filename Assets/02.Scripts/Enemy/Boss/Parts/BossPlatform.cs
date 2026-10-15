using System.Collections;
using UnityEngine;

/// <summary>
/// 보스전에서 자라나고 사라지는 플랫폼 (하나 또는 여러 개를 묶은 세트).
/// - 자라나는 애니메이션(Animator의 기본 상태)을 코드가 직접 시간 조절해 정방향(생성) / 역방향(소멸)으로 재생
/// - 판정(Collider2D)은 애니메이션이 Solid Ratio 이상 자랐을 때만 켜짐 → 애니메이션과 판정이 맞음
/// - 자라나는 / 사라지는 동안에는 애니메이션(Animator) 쪽 그림만 보이고, 다 자라면 애니메이션 그림은 끄고
///   나머지 실제 플랫폼 그림(줄기 / 잎 등)을 켬. 다 사라지면 전부 끔
/// 플랫폼 루트(보이는 부분 + 콜라이더를 모두 자식으로 가진 오브젝트)에 붙일 것.
/// 여러 플랫폼의 부모에 하나만 붙이면 자식의 모든 Animator / 콜라이더 / 렌더러를 한꺼번에 함께 자라나게 / 사라지게 함.
/// 배치 전환은 BossPlatformDirector가 담당
/// </summary>
[DisallowMultipleComponent]
public class BossPlatform : MonoBehaviour
{
    [Header("애니메이션")]
    [Tooltip("자라나는 애니메이션이 있는 Animator들. 비우면 자식의 모든 Animator. 각 Animator의 기본 상태(Entry)가 자라나는 애니메이션이어야 함")]
    [SerializeField]
    private Animator[] _animators;

    [Tooltip("자라나는 / 사라지는 데 걸리는 시간 (초). 0이면 가장 긴 애니메이션 길이")]
    [SerializeField, Min(0f)]
    private float _durationOverride = 0f;

    [Header("판정")]
    [Tooltip("이 플랫폼의 판정들. 비우면 자식의 모든 Collider2D")]
    [SerializeField]
    private Collider2D[] _colliders;

    [Tooltip("애니메이션이 이만큼(0~1) 자랐을 때부터 판정이 켜짐. 사라질 때는 이 아래로 내려가는 순간 꺼짐")]
    [SerializeField, Range(0f, 1f)]
    private float _solidRatio = 0.85f;

    [Header("그림")]
    [Tooltip("다 자란 뒤 애니메이션 그림을 끄고 실제 플랫폼 그림(Animator가 없는 자식 렌더러)만 남김. " +
             "실제 플랫폼 그림이 하나도 없으면 애니메이션 그림을 계속 보여줌")]
    [SerializeField]
    private bool _swapToStaticWhenGrown = true;

    private Renderer[] _animatedRenderers;
    private Renderer[] _staticRenderers;
    private int[] _stateHashes;
    private float _clipLength = 1f;
    private float _progress = 1f;
    private bool _initialized;
    private Coroutine _routine;

    /// <summary>지금 자란 정도 (0 = 없음, 1 = 다 자람)</summary>
    public float Progress => _progress;

    /// <summary>다 자라 있는지</summary>
    public bool IsShown => _progress >= 1f && _routine == null;

    /// <summary>완전히 사라져 있는지</summary>
    public bool IsHidden => _progress <= 0f && _routine == null;

    public bool IsAnimating => _routine != null;

    private float Duration => _durationOverride > 0f ? _durationOverride : _clipLength;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        if (_animators == null || _animators.Length == 0)
        {
            _animators = GetComponentsInChildren<Animator>(true);
        }

        if (_colliders == null || _colliders.Length == 0)
        {
            _colliders = GetComponentsInChildren<Collider2D>(true);
        }


        // 시간을 코드가 직접 조절 (정방향 / 역방향 모두). 길이는 가장 긴 애니메이션 기준, 모두 같은 비율로 진행
        _stateHashes = new int[_animators.Length];
        float longest = 0f;
        for (int i = 0; i < _animators.Length; i++)
        {
            Animator animator = _animators[i];
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                continue;
            }

            animator.speed = 0f;

            // Awake 시점에는 자식 Animator가 아직 초기화되지 않아 상태 정보를 못 읽을 수 있음.
            // 그때는 해시 0(= 지금 상태의 시간만 바꿈)으로 재생하고, 길이는 컨트롤러의 클립에서 구함
            if (animator.isActiveAndEnabled && animator.isInitialized)
            {
                animator.Update(0f);
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                _stateHashes[i] = info.fullPathHash;
            }

            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null)
                {
                    longest = Mathf.Max(longest, clip.length);
                }
            }
        }

        _clipLength = Mathf.Max(0.05f, longest > 0f ? longest : 1f);

        // 애니메이션 그림(Animator 오브젝트와 그 자식) / 실제 플랫폼 그림(그 외)으로 나눔
        var animated = new System.Collections.Generic.List<Renderer>();
        var statics = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (IsUnderAnimator(renderer.transform))
            {
                animated.Add(renderer);
            }
            else
            {
                statics.Add(renderer);
            }
        }

        _animatedRenderers = animated.ToArray();
        _staticRenderers = statics.ToArray();
    }

    /// <summary>애니메이션 없이 바로 보이기 / 숨기기 (전투 시작 시 초기 배치용)</summary>
    public void SetShownImmediate(bool shown)
    {
        Initialize();
        StopRoutine();
        ApplyProgress(shown ? 1f : 0f);
    }

    /// <summary>자라나는 애니메이션 정방향 재생 → 다 자라면 판정 유지</summary>
    public void Grow(float speed)
    {
        Initialize();
        StopRoutine();
        _routine = StartCoroutine(Animate(1f, speed));
    }

    /// <summary>자라나는 애니메이션 역재생 → 다 줄어들면 사라짐</summary>
    public void Shrink(float speed)
    {
        Initialize();
        StopRoutine();
        _routine = StartCoroutine(Animate(0f, speed));
    }

    private IEnumerator Animate(float target, float speed)
    {
        float rate = Mathf.Max(0.01f, speed) / Mathf.Max(0.01f, Duration);
        while (!Mathf.Approximately(_progress, target))
        {
            ApplyProgress(Mathf.MoveTowards(_progress, target, rate * Time.deltaTime));
            yield return null;
        }

        ApplyProgress(target);
        _routine = null;
    }

    private void ApplyProgress(float progress)
    {
        _progress = Mathf.Clamp01(progress);

        // 자라는 / 사라지는 중: 애니메이션 그림만, 다 자람: 실제 플랫폼 그림만, 없음: 전부 끔
        bool grown = _progress >= 1f;
        bool swap = _swapToStaticWhenGrown && _staticRenderers != null && _staticRenderers.Length > 0;
        bool showAnimated = _progress > 0f && (!grown || !swap);
        bool showStatic = swap ? grown : _progress > 0f;
        SetRenderers(_animatedRenderers, showAnimated);
        SetRenderers(_staticRenderers, showStatic);

        bool solid = _progress >= _solidRatio && _progress > 0f;
        if (_colliders != null)
        {
            foreach (Collider2D collider in _colliders)
            {
                if (collider != null)
                {
                    collider.enabled = solid;
                }
            }
        }

        if (_animators != null && _stateHashes != null)
        {
            // 마지막 프레임을 정확히 보여주기 위해 1 직전까지만 (비반복 클립은 1에서 다음 상태로 넘어갈 수 있음)
            float time = Mathf.Min(_progress, 0.9999f);
            for (int i = 0; i < _animators.Length; i++)
            {
                Animator animator = _animators[i];
                if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled)
                {
                    continue;
                }

                // 속도는 항상 0 (다른 곳에서 바뀌었거나 다시 켜졌을 때 대비)
                animator.speed = 0f;

                // 해시 0이면 지금 상태(기본 상태 = 자라나는 애니메이션)의 시간만 바꿈
                animator.Play(_stateHashes[i], 0, time);
                animator.Update(0f);
            }
        }
    }

    private static void SetRenderers(Renderer[] renderers, bool enabled)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = enabled;
            }
        }
    }

    private bool IsUnderAnimator(Transform target)
    {
        if (_animators == null)
        {
            return false;
        }

        foreach (Animator animator in _animators)
        {
            if (animator != null && target.IsChildOf(animator.transform))
            {
                return true;
            }
        }

        return false;
    }

    private void StopRoutine()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private void OnDisable()
    {
        _routine = null;
    }
}
