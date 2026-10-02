using System;
using UnityEngine;
using System.Collections;

public class HitFlash : MonoBehaviour
{
    [SerializeField] private Color _flashColor = Color.red;
    [SerializeField] private float _flashDuration = 0.1f;

    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void Flash()
    {
        if (_spriteRenderer == null) return;
        StartCoroutine(FlashCoroutine());
    }

    private IEnumerator FlashCoroutine()
    {
        if (_spriteRenderer == null) yield break;
        Color originalColor = _spriteRenderer.color;
        Color flashColor = _flashColor;
        flashColor.a = originalColor.a;
        _spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(_flashDuration);
        Color doneFlashColor = _spriteRenderer.color;
        originalColor.a = doneFlashColor.a;
        _spriteRenderer.color = originalColor;
    }
}