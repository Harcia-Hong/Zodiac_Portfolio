using UnityEngine;

/// <summary>
/// 페이즈 클리어 후 출현하는 보상 오브젝트의 공통 인터페이스
///
/// 용도:
///   여러 종류의 보상 오브젝트(StatBoost, CursedTome, SkillDrop 등)를
///   타입 구분 없이 일괄 제어하기 위한 인터페이스.
///
/// 사용 흐름 (RewardGateManager 시스템):
///   1. RewardGateController가 보상 N개 스폰 → 그룹에 등록
///   2. 플레이어가 하나와 상호작용 → 자체 처리 + OnSelectedEvent 발행
///   3. RewardGroup이 이벤트 수신 → 나머지에 OnDismissed() 호출
///   4. 다음 페이즈 진입 시 그룹이 잔여 멤버 정리
/// </summary>
public interface IRewardObject
{
    /// <summary>
    /// 플레이어가 이 보상을 선택했을 때 호출
    /// - 자체 연출 (획득 이펙트 등) 후 정리
    /// - 마지막에 OnSelectedEvent를 발행해야 그룹이 인지함
    /// </summary>
    void OnSelected();

    /// <summary>
    /// 다른 보상이 선택되어 이 오브젝트는 사라져야 할 때 호출
    /// - DOTween 등으로 가라앉는 연출 후 Destroy
    /// </summary>
    void OnDismissed();

    /// <summary>오브젝트 Transform 접근용</summary>
    Transform GetTransform();

    /// <summary>
    /// 선택됐다는 알림 이벤트 — RewardGroup이 구독.
    /// 구현체의 OnSelected() 메서드 안에서 발행해야 함.
    /// </summary>
    event System.Action<IRewardObject> OnSelectedEvent;

    /// <summary>
    /// 파괴됐다는 알림 이벤트 — RewardGroup이 구독해서 멤버 리스트 정리.
    /// 구현체의 OnDestroy()에서 발행 권장.
    /// </summary>
    event System.Action<IRewardObject> OnDestroyedEvent;
}
