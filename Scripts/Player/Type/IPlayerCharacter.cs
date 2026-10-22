using UnityEngine;

/// <summary>
/// 태그 시스템에서 전환 가능한 플레이어 캐릭터의 공통 인터페이스.
/// MeleeCharacter, RangedCharacter 같은 구체 클래스가 구현함.
/// 
/// 책임 범위 (의도적으로 좁게 잡음):
///   - 태그 컨트롤러가 캐릭터를 활성/비활성 전환할 때 필요한 최소 API
///   - 캐릭터별 스탯 보정값 (방어력/회피)
///   - 어택캔슬을 위한 현재 행동 강제 종료
///   - 공유 레이어(PlayerController)가 활성 캐릭터에 이동/대시 애니메이션 의도를 전달
/// 
/// 책임 범위 밖 (구체 클래스 내부 구현):
///   - Animator, Weapon, AttackManager 등 디테일
///   - 입력 처리 (활성 상태에서 자체 Update로 받음)
///   - 콤보 상태머신 (PlayerCombatStateMachine 자체적으로 관리)
/// </summary>
public interface IPlayerCharacter
{
    /// <summary>이 캐릭터의 타입(근접/원거리). 스킬 매칭·UI 필터링·디버깅에 사용.</summary>
    PlayerCharacterType CharacterType { get; }

    /// <summary>현재 이 캐릭터가 활성 상태인지(입력 받고 있는지). 비활성 캐릭은 false.</summary>
    bool IsActive { get; }

    /// <summary>
    /// 공격/스킬 등 인터럽트 불가능한 행동 중인지 여부.
    /// 태그 컨트롤러가 "자유 전환 vs 어택캔슬 전환"을 분기할 때 참조.
    /// 어택캔슬 활성화 모드에서는 IsBusy=true여도 강제로 CancelCurrentAction 호출 후 전환.
    /// </summary>
    bool IsBusy { get; }

    /// <summary>
    /// 지금 태그 전환을 받아도 되는 타이밍인지 (옵션 2: 후딜 윈도우 기반).
    /// 
    /// true 반환 조건:
    ///   - 캐릭터가 Idle 상태 (행동 안 함)
    ///   - 또는 공격/스킬의 후딜 윈도우 안 (Animator의 CanTag 이벤트로 켜진 상태)
    /// 
    /// false 반환 조건:
    ///   - 공격의 선딜/타격 판정 구간 (모션 강제 캔슬하면 어색해짐)
    ///   - 캔슬 불가능한 스킬 진행 중
    /// 
    /// PlayerTagController가 Tab 입력 받았을 때 이 값을 체크해서 전환 허용/거부 결정.
    /// 옵션 1(완전 자유 캔슬)로 회귀하려면 그냥 항상 true 반환하면 됨.
    /// </summary>
    bool CanTag { get; }

    /// <summary>
    /// 캐릭터의 GameObject 루트 Transform.
    /// 태그 전환 시 비활성 캐릭터를 활성 캐릭터 위치로 텔레포트시키는 데 사용
    /// (두 캐릭터가 같은 위치에서 자연스럽게 인계되도록).
    /// </summary>
    Transform Root { get; }

    /// <summary>
    /// 방어력 보정. 1.0 = 보정 없음, 0.8 = 받는 데미지 20% 감소.
    /// 활성화 시 PlayerStats에 곱연산으로 적용. 비활성 시 해제.
    /// </summary>
    float DefenseModifier { get; }

    /// <summary>
    /// 회피율 보정. 0.0 = 회피 없음, 0.3 = 30% 확률로 피해 완전 무시.
    /// 활성화 시 PlayerStats에 합연산으로 적용. 비활성 시 해제.
    /// </summary>
    float DodgeModifier { get; }

    /// <summary>
    /// 캐릭터 활성화. GameObject SetActive(true) + 콜라이더/애니메이터/입력 수신 ON.
    /// 태그 컨트롤러가 전환 시 호출. 위치 동기화는 호출 측(태그 컨트롤러)에서 먼저 처리.
    /// </summary>
    void Activate();

    /// <summary>
    /// 캐릭터 비활성화. 진행 중이던 콤보·애니메이션을 정리하고 GameObject SetActive(false).
    /// 단, 글로벌 쿨다운/지속 효과는 해제하지 않음 (그건 SkillManager 책임).
    /// </summary>
    void Deactivate();

    /// <summary>
    /// 진행 중인 공격/스킬을 즉시 강제 종료. 어택캔슬 전환 시 호출됨.
    /// Deactivate 직전에 호출되어 콤보 상태를 깨끗이 정리하는 용도.
    /// IsBusy=false 상태라면 아무것도 안 해도 됨(no-op).
    /// </summary>
    void CancelCurrentAction();

    // ============================================================
    // 공유 레이어 → 활성 캐릭터 애니메이션 위임
    // ============================================================

    /// <summary>
    /// 이동 상태 알림. 활성 캐릭터의 이동 애니메이션 파라미터(isRunning 등) 컨트롤용.
    /// PlayerController.MoveInput에서 매 프레임 호출. idempotent해야 함
    /// (같은 값으로 반복 호출되어도 부작용 없어야 함 — Animator.SetBool은 이미 idempotent).
    /// 캐릭터 내부에서 자기 Animator의 SetBool("isRunning", isMoving) 같은 식으로 처리.
    /// </summary>
    /// <param name="isMoving">현재 이동 중인지 여부</param>
    void SetMoving(bool isMoving);

    /// <summary>
    /// 캐릭터 로컬 기준 이동 방향 전달 (트윈 스틱 4방향 Blend Tree용).
    /// X: 좌우 (+1=캐릭터 오른쪽, -1=캐릭터 왼쪽)
    /// Y: 전후 (+1=캐릭터 전방, -1=캐릭터 후방)
    /// 트윈 스틱이 아닌 캐릭터(MeleeCharacter)는 빈 구현으로 무시.
    /// </summary>
    void SetMoveDirection(Vector2 localDirection);

    /// <summary>
    /// 대시 시작 알림. 활성 캐릭터의 대시 애니메이션 트리거.
    /// PlayerController.DodgeInput에서 대시 코루틴 시작 직전 1회 호출.
    /// 캐릭터 내부에서 자기 Animator의 SetTrigger("Dodge") 같은 식으로 처리.
    /// </summary>
    void TriggerDash();
}
