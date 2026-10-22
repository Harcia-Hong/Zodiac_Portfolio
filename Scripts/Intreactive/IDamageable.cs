using UnityEngine;

/// <summary>
/// 데미지를 받을 수 있는 모든 오브젝트가 구현하는 인터페이스
/// Enemy, BreakableObject 등 통합 관리
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// 데미지 받기
    /// </summary>
    /// <param name="damage">데미지량</param>
    void TakeDamage(int damage);

    /// <summary>
    /// 스킬 데미지 받기 (이펙트 타입 포함)
    /// </summary>
    /// <param name="damage">데미지량</param>
    /// <param name="effectType">피격 이펙트 타입</param>
    void ApplySkillDamage(int damage, HitEffectType effectType);

    /// <summary>
    /// 이미 파괴/사망 상태인지
    /// </summary>
    bool IsDead { get; }

    /// <summary>
    /// 오브젝트의 Transform (위치 참조용)
    /// </summary>
    Transform GetTransform();
}
