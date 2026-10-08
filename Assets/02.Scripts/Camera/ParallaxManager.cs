using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class ParallaxManager : MonoBehaviour
{
    [Serializable]
    private class ParallaxLayerSetting
    {
        [Header("레이어")]
        [SerializeField] private string _layerName;

        [SerializeField] private Transform _layer;

        [Header("가로 패럴렉스 강도")]
        [Range(0f, 1f)][SerializeField] private float _horizontalStrength;

        [Header("세로 패럴렉스 강도")]
        [Range(0f, 1f)][SerializeField] private float _verticalStrength = 0f;

        [Header("가로 반복")]
        [Tooltip("배경 묶음을 정확히 2번 반복해서 나란히 배치 (AA, ABAB, ABCABC)")]
        [SerializeField]
        private bool _loopHorizontally;

        [Tooltip("그림이 반복되는 간격. 0이면 자식 전체 폭의 절반으로 자동 계산")]
        [SerializeField]
        private float _loopWidth;

        private Vector3 _startPosition;
        private float _stripCenterOffset;

        public void Initialize()
        {
            if (_layer == null) return;

            _startPosition = _layer.position;

            if (!_loopHorizontally) return;

            SpriteRenderer[] renderers =
                _layer.GetComponentsInChildren<SpriteRenderer>();

            if (renderers.Length == 0)
            {
                Debug.LogWarning(
                    $"ParallaxManager: '{_layerName}' 레이어에서 SpriteRenderer를 찾지 못해 반복을 끕니다."
                );
                _loopHorizontally = false;
                return;
            }

            Bounds stripBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                stripBounds.Encapsulate(renderers[i].bounds);
            }

            // 부모 기준 배경 묶음 전체의 중심 위치
            _stripCenterOffset = stripBounds.center.x - _layer.position.x;

            // 같은 스프라이트가 다시 나오는 실제 위치 간격을 반복 간격으로 사용
            if (_loopWidth <= 0f)
            {
                _loopWidth = FindRepeatInterval(renderers);

                if (_loopWidth <= 0f)
                {
                    _loopWidth = stripBounds.size.x * 0.5f;
                    Debug.LogWarning(
                        $"ParallaxManager: '{_layerName}' 레이어에서 같은 스프라이트 2장을 찾지 못해 전체 폭의 절반({_loopWidth})을 사용합니다. Loop Width를 직접 입력하세요."
                    );
                }
            }
        }

        private static float FindRepeatInterval(SpriteRenderer[] renderers)
        {
            Sprite referenceSprite = renderers[0].sprite;
            var positions = new List<float>();

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer.sprite == referenceSprite)
                {
                    positions.Add(renderer.transform.position.x);
                }
            }

            positions.Sort();

            float minInterval = float.MaxValue;
            for (int i = 1; i < positions.Count; i++)
            {
                float interval = positions[i] - positions[i - 1];
                if (interval > 0.001f && interval < minInterval)
                {
                    minInterval = interval;
                }
            }

            return minInterval == float.MaxValue ? 0f : minInterval;
        }

        public void Move(Vector3 cameraMovement, float cameraX)
        {
            if (_layer == null) return;

            float x =
                _startPosition.x + cameraMovement.x * (1f - _horizontalStrength);

            float y =
                _startPosition.y + cameraMovement.y * (1f - _verticalStrength);

            if (_loopHorizontally && _loopWidth > 0f)
            {
                // 카메라가 항상 배경 묶음의 가운데 근처에 오도록 반복 간격 단위로 이동
                float stripCenter = x + _stripCenterOffset;
                float shift =
                    Mathf.Round((cameraX - stripCenter) / _loopWidth) * _loopWidth;

                x += shift;
            }

            _layer.position = new Vector3(x, y, _startPosition.z);
        }
    }

    [Header("카메라")]
    [SerializeField] private Transform _targetCamera;

    [Header("패럴렉스 레이어")]
    [SerializeField]
    private List<ParallaxLayerSetting> _layers =
        new List<ParallaxLayerSetting>();

    private Vector3 _startCameraPosition;

    private void Awake()
    {
        if (_targetCamera == null && Camera.main != null)
        {
            _targetCamera = Camera.main.transform;
        }

        if (_targetCamera == null)
        {
            Debug.LogError(
                "ParallaxManager: Target Camera를 연결하세요.",
                this
            );

            enabled = false;
            return;
        }

        _startCameraPosition = _targetCamera.position;

        foreach (ParallaxLayerSetting layerSetting in _layers)
        {
            layerSetting.Initialize();
        }
    }

    public void RebaseAtCurrentPosition()
    {
        if (_targetCamera == null && Camera.main != null)
            _targetCamera = Camera.main.transform;
        if (_targetCamera == null) return;
        _startCameraPosition = _targetCamera.position;
        foreach (ParallaxLayerSetting layerSetting in _layers)
            layerSetting.Initialize();
    }

    private void LateUpdate()
    {
        Vector3 cameraPosition = _targetCamera.position;
        Vector3 cameraMovement = cameraPosition - _startCameraPosition;

        foreach (ParallaxLayerSetting layerSetting in _layers)
        {
            layerSetting.Move(cameraMovement, cameraPosition.x);
        }
    }
}