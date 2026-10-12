using UnityEngine;

public class EnemyStateMachine
{
    private EnemyState _currentState;
    public EnemyState CurrentState => _currentState;
    public event System.Action<EnemyState, EnemyState> OnStateChanged;

    public void ChangeState(EnemyState newState)
    {
        if (_currentState != null)
        {
            _currentState.Exit();
        }

        EnemyState previous = _currentState;
        _currentState = newState;
        OnStateChanged?.Invoke(previous, newState);
        _currentState.Enter();
    }

    public void Update()
    {
        if (_currentState != null)
        {
            _currentState.Update();
        }
    }

    public void FixedUpdate()
    {
        if (_currentState != null)
        {
            _currentState.FixedUpdate();
        }
    }
}
