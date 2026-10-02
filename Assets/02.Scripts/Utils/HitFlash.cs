using System;
using UnityEngine;
using System.Collections;

public class HitFlash : MonoBehaviour
{
    [SerializeField] private Color _flashColor = Color.red;
    [SerializeField] private float _flashDuration = 0.1f;

    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;
    private Coroutine _flashRoutine;

    private void Awake()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // 원래 색은 한 번만 저장 (맞을 때마다 저장하면 연속 피격 시 빨간색이 저장됨)
        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
        }
    }

    private void OnDisable()
    {
        // 플래시 도중 비활성화되면 코루틴이 멈춰서 빨간 채로 남으므로 복구
        _flashRoutine = null;
        RestoreColor();
    }

    public void Flash()
    {
        if (_spriteRenderer == null) return;
        if (!isActiveAndEnabled) return;

        // 이전 플래시가 진행 중이면 멈추고 새로 시작
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
        }

        _flashRoutine = StartCoroutine(FlashCoroutine());
    }

    private IEnumerator FlashCoroutine()
    {
        // 알파는 현재 값 유지
        Color flashColor = _flashColor;
        flashColor.a = _spriteRenderer.color.a;
        _spriteRenderer.color = flashColor;

        yield return new WaitForSeconds(_flashDuration);

        RestoreColor();
        _flashRoutine = null;
    }

    private void RestoreColor()
    {
        if (_spriteRenderer == null) return;

        // RGB는 원래 색으로, 알파는 현재 값 유지
        Color restored = _originalColor;
        restored.a = _spriteRenderer.color.a;
        _spriteRenderer.color = restored;
    }
}