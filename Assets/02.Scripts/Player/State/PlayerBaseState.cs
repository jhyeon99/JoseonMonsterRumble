using UnityEngine;

public abstract class PlayerBaseState : IState
{
    protected Player _player;
    protected StateMachine _stateMachine;
    
    protected PlayerInputHandler _inputHandler;
    protected Rigidbody2D _rigidbody2D;
    
    public virtual PlatformInteractionMode PlatformMode => PlatformInteractionMode.Carried;

    protected PlayerBaseState(Player player, StateMachine stateMachine)
    {
        _player = player;
        _stateMachine = stateMachine;

        _inputHandler = player.GetComponent<PlayerInputHandler>();
        _rigidbody2D = player.GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 상태 진입 시 1회 실행
    /// </summary>
    public virtual void Enter()
    {
    }

    /// <summary>
    /// 매 프레임(Update) 실행 (상태 전환 조건 및 일반 로직 처리)
    /// </summary>
    public virtual void Execute()
    {
    }

    /// <summary>
    /// 물리 프레임(FixedUpdate) 실행 (Rigidbody 기반 이동 연산 처리)
    /// </summary>
    public virtual void PhysicsExecute()
    {
    }

    /// <summary>
    /// 상태 탈출 시 1회 실행
    /// </summary>
    public virtual void Exit()
    {
    }
}