using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이펙트 프리팹(Sprite Renderer + Animator)을 풀링해서 한 번씩 재생.
/// 직접 호출하지 말고 EffectManager를 통해 사용 (같은 오브젝트에 붙임).
/// </summary>
public class EffectPool : MonoBehaviour
{
    [Serializable]
    private struct EffectEntry
    {
        public EffectType Type;

        [Tooltip("Animator가 붙은 이펙트 프리팹. 클립의 Loop Time은 꺼둘 것")]
        public GameObject Prefab;

        [Tooltip("Play 호출 시 좌우 반전 요청을 받아들일지 (방향성 있는 이펙트만 켜기)")]
        public bool AllowFlip;
    }

    // 종류별 런타임 정보
    private class EffectSlot
    {
        public GameObject Prefab;
        public bool AllowFlip;
        public float Duration;
        public readonly Queue<GameObject> Pool = new Queue<GameObject>();
    }

    [Header("이펙트 목록")]
    [SerializeField] private EffectEntry[] _effects;

    [Header("풀 사이즈 (이펙트 종류별)")]
    [SerializeField, Min(1)] private int _poolSize = 5;

    [Tooltip("Animator나 클립이 없는 프리팹의 재생 시간")]
    [SerializeField, Min(0.01f)] private float _fallbackDuration = 1f;

    private readonly Dictionary<EffectType, EffectSlot> _slots = new Dictionary<EffectType, EffectSlot>();
    private readonly List<(GameObject Instance, EffectSlot Slot)> _playing = new List<(GameObject, EffectSlot)>();

    private void Awake()
    {
        InitSlots();
    }

    private void OnDisable()
    {
        // 재생 중에 꺼지면 코루틴이 멈춰서 이펙트가 화면에 남으므로 정리
        StopAllCoroutines();
        foreach ((GameObject instance, EffectSlot slot) in _playing)
        {
            if (instance == null) continue;
            instance.SetActive(false);
            slot.Pool.Enqueue(instance);
        }

        _playing.Clear();
    }

    private void InitSlots()
    {
        if (_effects == null) return;

        foreach (EffectEntry entry in _effects)
        {
            if (entry.Prefab == null)
            {
                Debug.LogWarning($"[EffectPool] {entry.Type}의 프리팹이 비어 있습니다.");
                continue;
            }

            if (_slots.ContainsKey(entry.Type))
            {
                Debug.LogWarning($"[EffectPool] {entry.Type}가 중복 등록되어 첫 번째 것만 사용합니다.");
                continue;
            }

            var slot = new EffectSlot
            {
                Prefab = entry.Prefab,
                AllowFlip = entry.AllowFlip,
                Duration = CalculateDuration(entry.Prefab),
            };

            for (int i = 0; i < _poolSize; i++)
            {
                slot.Pool.Enqueue(CreateInstance(slot.Prefab));
            }

            _slots.Add(entry.Type, slot);
        }
    }

    /// <summary>
    /// position 위치에서 type 이펙트를 한 번 재생.
    /// flipX는 등록할 때 AllowFlip을 켠 이펙트에만 적용.
    /// </summary>
    public void Play(EffectType type, Vector2 position, bool flipX = false, float scaleMultiplier = 1f)
    {
        if (!_slots.TryGetValue(type, out EffectSlot slot))
        {
            Debug.LogWarning($"[EffectPool] {type} 이펙트가 등록되지 않았습니다.");
            return;
        }

        GameObject instance = slot.Pool.Count > 0 ? slot.Pool.Dequeue() : CreateInstance(slot.Prefab);

        Transform effectTransform = instance.transform;
        effectTransform.position = new Vector3(position.x, position.y, slot.Prefab.transform.position.z);
        effectTransform.rotation = slot.Prefab.transform.rotation;

        // 프리팹 원래 스케일 기준으로 x만 반전
        Vector3 scale = slot.Prefab.transform.localScale;
        if (flipX && slot.AllowFlip)
        {
            scale.x = -scale.x;
        }
        effectTransform.localScale = scale * Mathf.Max(0f, scaleMultiplier);

        // 다시 켜면 Animator가 기본 상태(처음 프레임)부터 재생
        instance.SetActive(true);
        _playing.Add((instance, slot));
        StartCoroutine(ReturnAfter(instance, slot));
    }

    private IEnumerator ReturnAfter(GameObject instance, EffectSlot slot)
    {
        yield return new WaitForSeconds(slot.Duration);

        if (instance == null) yield break;

        instance.SetActive(false);
        _playing.Remove((instance, slot));
        slot.Pool.Enqueue(instance);
    }

    private GameObject CreateInstance(GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, transform);
        instance.SetActive(false);
        return instance;
    }

    // Animator에 들어 있는 클립 중 가장 긴 길이 / Animator 속도
    private float CalculateDuration(GameObject prefab)
    {
        Animator animator = prefab.GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return _fallbackDuration;
        }

        float maxLength = 0f;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.length > maxLength)
            {
                maxLength = clip.length;
            }
        }

        if (maxLength <= 0f) return _fallbackDuration;

        float speed = animator.speed > 0f ? animator.speed : 1f;
        return maxLength / speed;
    }
}
