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
        [Range(0f, 1f)]
        [SerializeField] private float _horizontalStrength;

        [Header("세로 패럴렉스 강도")]
        [Range(0f, 1f)]
        [SerializeField] private float _verticalStrength = 0f;

        private Vector3 _startPosition;

        public void Initialize()
        {
            if (_layer == null) return;

            _startPosition = _layer.position;
        }

        public void Move(Vector3 cameraMovement)
        {
            if (_layer == null) return;

            float followX =
                cameraMovement.x * (1f - _horizontalStrength);

            float followY =
                cameraMovement.y * (1f - _verticalStrength);

            _layer.position = new Vector3(
                _startPosition.x + followX,
                _startPosition.y + followY,
                _startPosition.z
            );
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

    private void LateUpdate()
    {
        Vector3 cameraMovement =
            _targetCamera.position - _startCameraPosition;

        foreach (ParallaxLayerSetting layerSetting in _layers)
        {
            layerSetting.Move(cameraMovement);
        }
    }
}