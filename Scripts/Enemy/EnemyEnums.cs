/**
 * Enemy 관련 열거형들을 모아놓은 파일
 * 다른 클래스들에서 자유롭게 접근 가능
 */

/// <summary>
/// Enemy 타입 (기존 호환성)
/// </summary>
public enum EnemyType
{
    Standard,    // 기본 근접 공격
    Charge,      // 돌진 공격  
    Stationary,   // 원거리 공격
    Hybrid,      // 혼합

}

/// <summary>
/// Enemy 등급
/// </summary>
public enum EnemyRank
{
    Normal,  // 일반
    Elite,   // 엘리트
    Epic,    // 에픽
    Boss     // 보스
}

/// <summary>
/// Enemy 상태 (기존 호환성)
/// </summary>
public enum EnemyState
{
    Idle,      // 대기
    Patrol,    // 순찰
    Trace,     // 추적
    Attack,    // 공격
    CoolDown,  // 쿨타임
    Hit,       // 피격
    Die        // 죽음
}
