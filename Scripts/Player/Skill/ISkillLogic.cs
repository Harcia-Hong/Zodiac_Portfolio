using UnityEngine;

/// <summary>
/// 모든 스킬 로직 프리팹이 구현해야 하는 인터페이스
/// </summary>
public interface ISkillLogic
{
    /// <summary>
    /// 스킬 발동 시 호출되는 메인 함수
    /// </summary>
    /// <param name="owner">스킬을 시전한 게임 오브젝트 (주로 플레이어)</param>
    void Activate(GameObject owner);
}
