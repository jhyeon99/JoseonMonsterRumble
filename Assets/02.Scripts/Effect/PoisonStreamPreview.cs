using UnityEngine;

/// <summary>Used only by Test_PoisonStream. Does not reference boss or stage systems.</summary>
public sealed class PoisonStreamPreview : MonoBehaviour
{
    [SerializeField] private PoisonStream2D _streamPrefab;
    [SerializeField, Min(0.1f)] private float _emissionDuration = 3f;
    [SerializeField, Min(2f)] private float _pauseDuration = 2f;

    private PoisonStream2D _stream;
    private float _cycleTime;
    private bool _isEmitting;

    private void Start()
    {
        if (_streamPrefab == null)
        {
            Debug.LogWarning("독줄기 테스트: 프리팹이 지정되지 않았습니다.", this);
            enabled = false;
            return;
        }

        var cameraObject = new GameObject("PoisonPreviewCamera", typeof(Camera));
        Camera previewCamera = cameraObject.GetComponent<Camera>();
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = 6f;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.13f, 0.15f, 0.14f);
        cameraObject.transform.position = new Vector3(2f, 0f, -10f);

        _stream = Instantiate(_streamPrefab);
        _stream.name = "PoisonStream_TestInstance";
        _stream.SetMuzzle(transform);
        StartEmission();
    }

    private void Update()
    {
        _cycleTime += Time.deltaTime;
        if (_isEmitting && _cycleTime >= _emissionDuration)
        {
            _stream.StopEmission();
            _isEmitting = false;
        }
        if (_cycleTime >= _emissionDuration + _pauseDuration)
            StartEmission();
    }

    private void StartEmission()
    {
        _cycleTime = 0f;
        _isEmitting = true;
        _stream.StartEmission();
    }
}
