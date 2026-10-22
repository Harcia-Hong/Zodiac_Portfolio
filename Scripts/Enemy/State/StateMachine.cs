/**
 * 상태머신 기반 클래스
 * 모든 StateMachine(Player, Enemy 등)이 상속받아 사용
 * 상태 전환과 업데이트를 관리
 */
public class StateMachine
{
    /// <summary>현재 활성화된 상태</summary>
    protected IState currentState;

    /// <summary>현재 상태 읽기 전용 접근</summary>
    public IState CurrentState => currentState;

    /// <summary>
    /// 상태 변경 메서드
    /// 안전한 상태 전환을 보장 (이전 상태 Exit → 새 상태 Enter)
    /// </summary>
    /// <param name="newState">변경할 새로운 상태</param>
    public void ChangeState(IState newState)
    {
        // 현재 상태가 있다면 종료 처리
        currentState?.Exit();

        // 새 상태로 변경
        currentState = newState;

        // 새 상태 시작 처리
        currentState?.Enter();
    }

    /// <summary>
    /// 입력 처리 - 매 프레임 호출
    /// </summary>
    public void HandleInput()
    {
        currentState?.HandleInput();
    }

    /// <summary>
    /// 상태 업데이트 - 매 프레임 호출
    /// </summary>
    public void Update()
    {
        currentState?.Update();
    }

    /// <summary>
    /// 물리 업데이트 - FixedUpdate에서 호출
    /// </summary>
    public void PhysicsUpdate()
    {
        currentState?.PhysicsUpdate();
    }
}
