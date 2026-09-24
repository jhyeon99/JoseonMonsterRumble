using UnityEngine;

public class EnemyStateMachine
{
    private IEnemyState _currentState;

    public void ChangeState(IEnemyState newState)
    {
        if (_currentState != null)
        {
            _currentState.ExitState();
        }

        _currentState = newState;

        _currentState.EnterState();
    }

    public void UpdateState()
    {
        if (_currentState != null)
        {
            _currentState.UpdateState();
        }
    }
}
