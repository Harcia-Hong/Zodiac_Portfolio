using UnityEngine;

/// <summary>
/// 보상 등급 (기존 제단 시스템용)
/// </summary>
public enum RewardTier
{
    Minor,      // 미미한 보상 (버프만)
    Moderate,   // 적당한 보상 (버프 + 약한 디버프)
    Major       // 막대한 보상 (강한 버프 + 강한 디버프)
}

/// <summary>
/// 보상 타입
/// </summary>
public enum RewardType
{
    StatBuff,       // 스탯 버프
    Currency,       // 재화 (골드, 별가루)
    SkillDrop,      // 스킬 드롭
    Heal            // HP 회복
}

/// <summary>
/// 재화 타입
/// </summary>
public enum CurrencyType
{
    Gold,
    Stardust
}

/// <summary>
/// 디버프 타입
/// - 기존: StatDebuff, DamageOnHit, SkillSeal, CurrencyLoss
/// - 추가: 금서의 잔재 전용 타입들
/// </summary>
public enum DebuffType
{
    // === 기존 ===
    StatDebuff,             // 스탯 감소 - FloorEffectSO 활용
    DamageOnHit,            // 피격 시 추가 고정 데미지 (저주받은 활자)
    SkillSeal,              // 스킬 봉인 - Q/E/R 중 랜덤 (봉인된 페이지)
    CurrencyLoss,           // 재화 즉시 손실 (흩날린 별가루)

    // === 금서의 잔재 전용 ===
    DamageTakenIncrease,    // 받는 모든 피해 n% 증가 (종이 갑옷)
    DamageDealtReduction,   // 주는 모든 피해 n% 감소 (바랜 의지)
    PeriodicDamage,         // n초마다 현재 체력의 n% 피해 (부식하는 문장)
    CooldownIncrease,       // 스킬 쿨타임 n% 증가 (얼어붙은 잉크)
    BattleStartDamage,      // 전투 시작 시 최대 체력의 n% 피해 (금이 간 표지)
    GoldGainReduction       // 골드 획득량 n% 감소 (빚진 지식)
}

/// <summary>
/// 금서의 잔재 — 저주 등급 (리스크/리턴 비례)
/// </summary>
public enum CurseGrade
{
    None,       // 덮어둔다 (무시)
    Grade_I,    // 찢어진 금서 — 약한 디버프 + 약한 보상
    Grade_II,   // 읽힌 금서 — 중간 디버프 + 중간 보상
    Grade_III   // 펼쳐진 금서 — 강한 디버프 + 큰 보상
}

/// <summary>
/// 흩어진 서판 — 스탯 강화 등급 (색상 + 수치 차이)
/// </summary>
public enum StatBoostGrade
{
    Common,     // 흰색 — 기본 수치
    Uncommon,   // 초록 — 중간 수치
    Rare,       // 보라 — 높은 수치
    Legendary   // 빨강 — 최고 수치
}

/// <summary>
/// 층 클리어 시 스폰 가능한 보상 오브젝트 종류
/// </summary>
public enum ClearRewardType
{
    None,               // 아무것도 안 나옴
    StatBoost,          // 흩어진 서판 (스탯 강화)
    SkillDrop,          // 힘의 조각 (스킬 획득)
    SkillUpgrade,       // 기억의 잉크병 (스킬 강화)
    CursedTome          // 금서의 잔재 (재앙)
}
