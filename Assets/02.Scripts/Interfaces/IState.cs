public interface IState
{
    /// <summary>
    /// 해당 상태에 진입할 때 1회 호출 (초기화, 애니메이션 재생, 상태 플래그 설정 등)
    /// </summary>
    void Enter();

    /// <summary>
    /// 매 프레임(Update) 호출 (상태 전환 조건 검사, 타이머 업데이트 등)
    /// </summary>
    void Execute();

    /// <summary>
    /// 물리 업데이트(FixedUpdate) 시 호출 (Rigidbody 기반 이동/점프 연산)
    /// </summary>
    void PhysicsExecute();

    /// <summary>
    /// 다른 상태로 전환되어 현재 상태를 빠져나갈 때 1회 호출 (정리, 타이머 리셋 등)
    /// </summary>
    void Exit();
}