using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UI_EnemyDamageText : MonoBehaviour
{
    [SerializeField] private Camera _worldCamera;
    [SerializeField] private TextMeshProUGUI _textPrefab;
    [SerializeField, Min(0.01f)] private float _duration = 0.8f;
    [SerializeField] private float _riseDistance = 60f;
    [SerializeField] private Vector2 _offset = new Vector2(10f, 30f);
    [SerializeField] private Color _textColor = Color.white;
    [SerializeField, Min(1f)] private float _fontSize = 56f;
    private Canvas _canvas;
    private RectTransform _container;
    private readonly List<TextMeshProUGUI> _activeTexts = new List<TextMeshProUGUI>();

    private void Awake()
    {
        _container = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
        {
            Debug.LogWarning("UI_EnemyDamageText requires a parent Canvas.", this);
            enabled = false;
        }
    }

    private void OnEnable() => GameEvents.OnEnemyHit += ShowDamage;

    private void OnDisable()
    {
        GameEvents.OnEnemyHit -= ShowDamage;
        StopAllCoroutines();
        foreach (TextMeshProUGUI text in _activeTexts)
        {
            if (text != null)
                Destroy(text.gameObject);
        }

        _activeTexts.Clear();
    }

    private void ShowDamage(HitData hitData)
    {
        if (_canvas == null || hitData.Damage <= 0f) return;
        Camera worldCamera = _worldCamera != null ? _worldCamera : Camera.main;
        if (worldCamera == null) return;
        Vector3 position = hitData.HitPoint;
        if (worldCamera.WorldToScreenPoint(position).z <= 0f) return;

        TextMeshProUGUI text;
        if (_textPrefab != null)
        {
            text = Instantiate(_textPrefab, _container, false);
        }
        else
        {
            var textObject = new GameObject("Enemy Damage", typeof(RectTransform));
            textObject.transform.SetParent(_container, false);
            text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = _fontSize;
            text.color = _textColor;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(160f, 60f);
        }

        text.text = hitData.Damage.ToString("0.#");
        text.raycastTarget = false;
        text.gameObject.SetActive(true);
        _activeTexts.Add(text);
        StartCoroutine(Animate(text, position, worldCamera));
    }

    private IEnumerator Animate(TextMeshProUGUI text, Vector3 position, Camera worldCamera)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, _duration);
        Color color = text.color;
        while (elapsed < duration && text != null && worldCamera != null)
        {
            float progress = elapsed / duration;
            Canvas root = _canvas.rootCanvas;
            Camera uiCamera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            Vector3 screenPosition = worldCamera.WorldToScreenPoint(position);
            if (screenPosition.z <= 0f) break;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _container, screenPosition, uiCamera, out Vector2 localPosition))
            {
                text.rectTransform.localPosition = localPosition + _offset + Vector2.up * (_riseDistance * progress);
            }

            text.color = new Color(color.r, color.g, color.b, color.a * (1f - progress));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _activeTexts.Remove(text);
        if (text != null) Destroy(text.gameObject);
    }
}