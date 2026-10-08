/// <summary>보스 상태 기본형. 상태마다 새 인스턴스를 만들어 전환 (적 FSM과 같은 방식)</summary>
public abstract class BossState
{
    protected readonly Boss _boss;

    protected BossState(Boss boss)
    {
        _boss = boss;
    }

    /// <summary>이 상태에서 플레이어 공격으로 데미지를 받는지</summary>
    public virtual bool CanTakeDamage => false;

    /// <summary>패링 그로기 / 페이즈 전환 등으로 끊길 수 있는지</summary>
    public virtual bool CanBeInterrupted => false;

    public virtual void Enter()
    {
    }

    public virtual void Update()
    {
    }

    public virtual void Exit()
    {
    }
}

public class BossStateMachine
{
    public BossState CurrentState { get; private set; }

    public void ChangeState(BossState nextState)
    {
        CurrentState?.Exit();
        CurrentState = nextState;
        CurrentState?.Enter();
    }

    public void Update()
    {
        CurrentState?.Update();
    }
}
