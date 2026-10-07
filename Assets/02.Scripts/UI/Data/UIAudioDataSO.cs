using UnityEngine;

[CreateAssetMenu(fileName = "SO_UIAudioData", menuName = "Audio/UI Audio Data")]
public class UIAudioDataSO : ScriptableObject
{
    [Header("버튼 사운드")]
    [SerializeField] private AudioClip _buttonClickClip;
    [SerializeField, Range(0f, 1f)] private float _buttonClickVolume = 1f;

    [SerializeField] private AudioClip _buttonHoverClip;
    [SerializeField, Range(0f, 1f)] private float _buttonHoverVolume = 1f;

    [Header("패널 사운드")]
    [SerializeField] private AudioClip _panelOpenClip;
    [SerializeField, Range(0f, 1f)] private float _panelOpenVolume = 1f;

    [SerializeField] private AudioClip _panelCloseClip;
    [SerializeField, Range(0f, 1f)] private float _panelCloseVolume = 1f;

    [Header("슬라이더 사운드")]
    [SerializeField] private AudioClip _sliderTickClip;
    [SerializeField, Range(0f, 1f)] private float _sliderTickVolume = 1f;

    public AudioClip ButtonClickClip => _buttonClickClip;
    public float ButtonClickVolume => _buttonClickVolume;

    public AudioClip ButtonHoverClip => _buttonHoverClip;
    public float ButtonHoverVolume => _buttonHoverVolume;

    public AudioClip PanelOpenClip => _panelOpenClip;
    public float PanelOpenVolume => _panelOpenVolume;

    public AudioClip PanelCloseClip => _panelCloseClip;
    public float PanelCloseVolume => _panelCloseVolume;

    public AudioClip SliderTickClip => _sliderTickClip;
    public float SliderTickVolume => _sliderTickVolume;
}
