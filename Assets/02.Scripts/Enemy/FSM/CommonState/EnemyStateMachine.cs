using UnityEngine;

public class EnemyStateMachine
{
    private EnemyState _currentState;

    public void ChangeState(EnemyState newState)
    {
        if (_currentState != null)
        {
            _currentState.Exit();
        }

        _currentState = newState;

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