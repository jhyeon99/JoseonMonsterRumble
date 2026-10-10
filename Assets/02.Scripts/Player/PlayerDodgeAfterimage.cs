using System.Collections.Generic;
using UnityEngine;

public class PlayerDodgeAfterimage : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _source;
    [SerializeField, Min(0.01f)] private float _spawnInterval = 0.05f;
    [SerializeField, Min(0.01f)] private float _lifetime = 0.25f;
    [SerializeField] private Color _color = new Color(0.5f, 0.8f, 1f, 0.45f);
    [SerializeField, Min(1)] private int _maxImages = 16;

    private sealed class Image
    {
        public SpriteRenderer Renderer;
        public float Remaining;
        public float Lifetime;
        public Color Color;
    }

    private readonly List<Image> _images = new List<Image>();
    private bool _emitting;
    private float _timer;

    private void Awake()
    {
        if (_source == null)
        {
            _source = GetComponentInChildren<SpriteRenderer>();
        }
    }

    public void Begin()
    {
        if (!isActiveAndEnabled) return;
        _emitting = true;
        _timer = 0f;
    }

    public void Stop()
    {
        _emitting = false;
    }

    private void LateUpdate()
    {
        foreach (Image image in _images)
        {
            if (image.Remaining <= 0f) continue;
            image.Remaining = Mathf.Max(0f, image.Remaining - Time.deltaTime);
            Color color = image.Color;
            color.a *= image.Remaining / image.Lifetime;
            image.Renderer.color = color;
            if (image.Remaining <= 0f)
            {
                image.Renderer.gameObject.SetActive(false);
            }
        }

        if (!_emitting) return;
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = Mathf.Max(0.01f, _spawnInterval);
        Emit();
    }

    private void Emit()
    {
        if (_source == null || !_source.enabled || !_source.gameObject.activeInHierarchy || _source.sprite == null)
            return;

        Image image = null;
        foreach (Image candidate in _images)
        {
            if (candidate.Remaining <= 0f)
            {
                image = candidate;
                break;
            }
        }

        if (image == null)
        {
            if (_images.Count >= Mathf.Max(1, _maxImages)) return;
            var imageObject = new GameObject("Dodge Afterimage");
            imageObject.layer = _source.gameObject.layer;
            // 플레이어가 이동해도 잔상은 생성된 월드 위치에 남는다.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(imageObject, gameObject.scene);
            image = new Image { Renderer = imageObject.AddComponent<SpriteRenderer>() };
            _images.Add(image);
        }

        SpriteRenderer renderer = image.Renderer;
        renderer.transform.SetPositionAndRotation(_source.transform.position, _source.transform.rotation);
        renderer.transform.localScale = _source.transform.lossyScale;
        renderer.sprite = _source.sprite;
        renderer.sharedMaterial = _source.sharedMaterial;
        renderer.flipX = _source.flipX;
        renderer.flipY = _source.flipY;
        renderer.drawMode = _source.drawMode;
        renderer.size = _source.size;
        renderer.sortingLayerID = _source.sortingLayerID;
        renderer.sortingOrder = _source.sortingOrder - 1;
        renderer.color = _color;
        renderer.gameObject.SetActive(true);
        image.Color = _color;
        image.Lifetime = Mathf.Max(0.01f, _lifetime);
        image.Remaining = image.Lifetime;
    }

    private void OnDisable()
    {
        Stop();
        foreach (Image image in _images)
        {
            image.Remaining = 0f;
            if (image.Renderer != null)
            {
                image.Renderer.gameObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        foreach (Image image in _images)
        {
            if (image.Renderer != null)
            {
                Destroy(image.Renderer.gameObject);
            }
        }

        _images.Clear();
    }
}