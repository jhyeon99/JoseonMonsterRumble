using UnityEngine;

public class StateMachine
{
    /// <summary>
    /// 현재 활성화된 상태
    /// </summary>
    public IState CurrentState { get; private set; }
    public event System.Action<IState, IState> OnStateChanged;

    /// <summary>
    /// 상태 머신을 초기화하고 시작 상태를 설정합니다.
    /// </summary>
    /// <param name="startingState">처음 시작할 상태</param>
    public void Initialize(IState startingState)
    {
        if (startingState == null)
        {
            Debug.LogError("[StateMachine] Starting state is null!");
            return;
        }

        CurrentState = startingState;
        CurrentState.Enter();
        OnStateChanged?.Invoke(null, CurrentState);
    }

    /// <summary>
    /// 새로운 상태로 전환합니다.
    /// </summary>
    /// <param name="newState">전환할 목표 상태</param>
    public void ChangeState(IState newState)
    {
        if (newState == null)
        {
            Debug.LogWarning("[StateMachine] Attempted to change to a null state.");
            return;
        }

        // 동일한 상태로의 중복 전환 방지 (필요 시 연타 입력을 위해 허용하도록 수정 가능)
        if (CurrentState == newState)
            return;

        IState previous = CurrentState;
        // 기존 상태 정리
        CurrentState?.Exit();

        // 상태 교체
        CurrentState = newState;

        // 새 상태 진입
        CurrentState.Enter();
        OnStateChanged?.Invoke(previous, CurrentState);
    }

    /// <summary>
    /// MonoBehavior의 Update에서 매 프레임 호출
    /// </summary>
    public void Update()
    {
        CurrentState?.Execute();
    }

    /// <summary>
    /// MonoBehavior의 FixedUpdate에서 매 물리 프레임 호출
    /// </summary>
    public void FixedUpdate()
    {
        CurrentState?.PhysicsExecute();
    }
}