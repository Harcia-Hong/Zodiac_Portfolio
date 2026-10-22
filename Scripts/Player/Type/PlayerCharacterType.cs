/// <summary>
/// 플레이어 캐릭터 타입 구분자.
/// 
/// 사용처:
///   - 태그 컨트롤러가 활성 캐릭터 추적
///   - SkillData.OwnerCharacter 필드와 매칭해서 스킬 슬롯 필터링
///   - UI에서 슬롯 표시 분기
/// 
/// 확장 메모: 만약 나중에 "공용 스킬"이 필요하면 이 enum에 Both를 추가하지 말고,
/// SkillData 쪽에 별도의 SkillOwnerScope enum (Melee/Ranged/Both)을 두는 게 깔끔함.
/// 이 enum은 "실제 플레이어 캐릭터 종류"만 담는 책임.
/// </summary>
public enum PlayerCharacterType
{
    Melee,   // 근접 캐릭터 (검사)
    Ranged   // 원거리 캐릭터 (총잡이/궁수)
}
