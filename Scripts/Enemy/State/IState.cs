/**
 * FSM(Finite State Machine) 상태 인터페이스
 * 모든 상태 클래스가 구현해야 하는 기본 메서드들을 정의
 */
public interface IState
{
    /// <summary>
    /// 상태 진입 시 호출
    /// 애니메이션 시작, 초기화 작업 등을 처리
    /// </summary>
    void Enter();
   
    /// <summary>
    /// 상태 종료 시 호출  
    /// 애니메이션 정리, 리소스 해제 등을 처리
    /// </summary>
    void Exit();

    /// <summary>
    /// 매 프레임 업데이트
    /// 상태별 로직 실행, 상태 전환 조건 체크 등
    /// </summary>
    void Update();

    /// <summary>
    /// 물리 업데이트 (FixedUpdate)
    /// 물리 기반 이동, 충돌 처리 등
    /// </summary>
    void PhysicsUpdate();

    /// <summary>
    /// 입력 처리
    /// 플레이어 입력이나 AI 판단 로직 처리
    /// </summary>
    void HandleInput();
}
