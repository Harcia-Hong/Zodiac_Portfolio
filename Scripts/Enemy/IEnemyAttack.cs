/**
 * Enemy 공격 시스템 통합 인터페이스
 * 모든 Enemy 공격 컴포넌트(A_Attack, Charge_Attack, Range_Attack)가 구현
 * FSM에서 공격 타입에 관계없이 통일된 방식으로 접근 가능
 */
public interface IEnemyAttack
{
    /// <summary>
    /// 현재 공격 중인지 확인
    /// FSM에서 상태 전환 조건 체크에 사용
    /// </summary>
    /// <returns>공격 중이면 true, 아니면 false</returns>
    bool IsAttacking();

    /// <summary>
    /// 공격 시작
    /// FSM의 AttackState에서 호출
    /// </summary>
    void StartAttack();

    /// <summary>
    /// 공격 중단/취소
    /// 상태 전환이나 피격 시 호출
    /// </summary>
    void StopAttack();

    /// <summary>
    /// 이 공격의 사거리 반환
    /// FSM에서 공격 가능 거리 판단에 사용
    /// </summary>
    /// <returns>공격 사거리 (미터)</returns>
    float GetAttackRange();

    /// <summary>
    /// 공격 쿨타임 반환  
    /// FSM에서 다음 공격까지의 대기 시간 계산에 사용
    /// </summary>
    /// <returns>쿨타임 (초)</returns>
    float GetCooldownTime();
}
