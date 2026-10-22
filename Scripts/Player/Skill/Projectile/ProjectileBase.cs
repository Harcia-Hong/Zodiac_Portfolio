using UnityEngine;

/// <summary>
/// 발사체 공통 베이스 — 모든 직선 발사체(폭발 화살, 얼음 화살, 독 화살 등)가 상속.
/// 
/// LaunchAction이 이 베이스의 Initialize 호출만으로 어떤 발사체든 발사 가능.
/// 발사체별 추가 옵션(폭발 반경, 스턴 시간 등)은 각 자식 클래스가 자기 인스펙터 필드로 보유.
/// 
/// 직접 사용 X — 상속해서만 사용.
/// </summary>
public abstract class ProjectileBase : MonoBehaviour
{
    /// <summary>
    /// 발사체 초기화 — LaunchAction에서 호출하는 공통 인터페이스.
    /// </summary>
    /// <param name="direction">진행 방향 (수평 평면, Y축 자동 제거)</param>
    /// <param name="speed">이동 속도 (유닛/초)</param>
    /// <param name="range">최대 사거리. 이 거리 도달 시 발사체별 종료 처리(폭발/소멸 등) 실행</param>
    /// <param name="damage">기본 데미지 (스탯 보정·크리 이미 적용된 값)</param>
    /// <param name="enemyLayer">적 레이어 마스크</param>
    /// <param name="obstacleLayer">벽/장애물 레이어 마스크</param>
    /// <param name="effect">피격 히트 이펙트 타입</param>
    public abstract void Initialize(
        Vector3 direction,
        float speed,
        float range,
        int damage,
        LayerMask enemyLayer,
        LayerMask obstacleLayer,
        HitEffectType effect
    );

    /// <summary>
    /// 슬롯에 부여된 불씨 효과를 발사체에 적용.
    /// 발사체 종류마다 적용 방식이 다르므로 virtual로 정의 — 기본 동작은 아무것도 안 함.
    /// 
    /// 호출 시점: LaunchAction이 Initialize() 직후 호출.
    /// 호출자가 slot=null로 전달할 수 있으니 null 체크 필수.
    /// </summary>
    public virtual void ApplyEmberModifiers(SkillSlot slot)
    {
        // 기본: 아무것도 안 함 (자식 클래스가 자기 식대로 override)
    }
}
