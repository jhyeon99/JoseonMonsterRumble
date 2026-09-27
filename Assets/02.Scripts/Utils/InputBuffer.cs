using UnityEngine;

[System.Serializable]
public class InputBuffer
{
    [SerializeField] private float _bufferTime = 0.1f;

    private float _lastPressedTime = float.NegativeInfinity;

    public InputBuffer(float bufferTime)
    {
        _bufferTime = bufferTime;
    }

    public void Press()
    {
        _lastPressedTime = Time.time;
    }

    public void Clear()
    {
        _lastPressedTime = float.NegativeInfinity;
    }

    public bool TryUse()
    {
        if (Time.time - _lastPressedTime > _bufferTime) return false;

        Clear();
        return true;
    }
}