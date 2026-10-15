using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button), typeof(CanvasGroup))]
public sealed class DeathRunButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{

    private enum VisualState { Normal, Highlighted, Pressed }

    [SerializeField, Min(0f)] private float _revealDuration = 0.24f;
    [SerializeField, Min(0.01f)] private float _responseDuration = 0.12f;
    [SerializeField, Range(0.5f, 1f)] private float _pressedScale = 0.96f;

    public float RevealDuration => _revealDuration;
    public event Action Pressed;

    private Button _button;
    private CanvasGroup _canvasGroup;
    private Vector3 _restScale;
    private VisualState _state;
    private bool _isPointerInside;
    private bool _isSelected;
    private Coroutine _revealCoroutine;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _restScale = transform.localScale;
    }

    private void OnEnable()
    {
        ResetVisual();
        _revealCoroutine = StartCoroutine(PlayRevealRoutine());
    }

    private void OnDisable()
    {
        if (_revealCoroutine != null)
        {
            StopCoroutine(_revealCoroutine);
            _revealCoroutine = null;
        }
        ResetVisual();
    }

    private void Update()
    {
        if (_state != VisualState.Pressed)
        {
            UpdateSelectionVisual();
        }
        float progress = Mathf.Clamp01(Time.unscaledDeltaTime / _responseDuration);
        float targetScale = _state == VisualState.Pressed ? _pressedScale : 1f;
        transform.localScale = Vector3.Lerp(transform.localScale, _restScale * targetScale, progress);
    }

    private IEnumerator PlayRevealRoutine()
    {
        float elapsed = 0f;
        while (elapsed < _revealDuration)
        {
            _canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, elapsed / _revealDuration);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        _canvasGroup.alpha = 1f;
        _revealCoroutine = null;
    }

    private void ResetVisual()
    {
        _state = VisualState.Normal;
        _isPointerInside = false;
        _isSelected = false;
        transform.localScale = _restScale;
        _canvasGroup.alpha = 0f;
    }

    public void ApplyPressedVisual()
    {
        _state = VisualState.Pressed;
        transform.localScale = _restScale * _pressedScale;
    }

    public void CompleteReveal()
    {
        if (_revealCoroutine != null)
        {
            StopCoroutine(_revealCoroutine);
            _revealCoroutine = null;
        }
        _canvasGroup.alpha = 1f;
    }

    private void UpdateSelectionVisual()
    {
        _state = _button.IsInteractable() && (_isPointerInside || _isSelected)
            ? VisualState.Highlighted : VisualState.Normal;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerInside = true;
        UpdateSelectionVisual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerInside = false;
        UpdateSelectionVisual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_button.IsInteractable() && eventData.button == PointerEventData.InputButton.Left)
        {
            Pressed?.Invoke();
            ApplyPressedVisual();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            UpdateSelectionVisual();
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        _isSelected = true;
        UpdateSelectionVisual();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isSelected = false;
        UpdateSelectionVisual();
    }
}
