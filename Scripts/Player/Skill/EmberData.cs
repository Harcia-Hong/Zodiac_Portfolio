using UnityEngine;

/// <summary>
/// 불씨 효과 타입.
/// 
/// 단순 모디파이어: SkillManager가 시전 시 일괄 수치 적용 (데미지/크리/쿨/차지 등).
/// 복잡 효과: 각 컴포넌트(발사체/액션 등)가 자체 체크해서 처리 (거리 스케일링 등).
/// </summary>
public enum EmberEffectType
{
    // ─── 단순 모디파이어 (SkillManager에서 일괄 처리) ───

    DamagePercent,          // 데미지 +N% (effectValue: 백분율, 예: 30 = +30%)
    CritChanceBonus,        // 크리 확률 +N% (절대값 추가)
    CritDamagePercent,      // 크리 피해 +N%
    CooldownReduction,      // 쿨다운 -N초 (절대 초 차감)
    ExtraCharges,           // 차지 최대값 +N (정수, effectValue 반올림)
    RangeMultiplier,        // 범위 +N% (액션 측에서 자체 곱연산)
    DurationPercent,        // 지속시간 +N% (폭풍/얼음가시 등 지속형 스킬)

    // ─── 복잡 효과 (각 컴포넌트가 자체 처리) ───

    DistanceScaling,        // 비행 거리에 따라 데미지 누적 증가, 최대 effectValue % (발사체 측)
    HitCooldownReduction,   // 명중 시 모든 스킬 쿨 -effectValue초 (전역 이벤트 필요)

    // ─── 액션 전용 효과 (특정 SkillAction이 자체 처리) ───

    MaxChargeTimeReduction, // 차징 액션의 최대 차징 시간 -N초 (FanProjectileChargeAction 등 차징 스킬용)
    FanFocusSinglePoint,    // 부채꼴 → 한 점 집중 + 2번째+ 발사체 피해 -N% (FanProjectileChargeAction 전용)
    SecondaryBlast,         // 1차 폭발 후 짧은 딜레이로 2차 폭발 추가, 2차 데미지 = 1차의 N% (블랙홀/폭발화살 등)
}

/// <summary>
/// 불씨 1개의 정의 (ScriptableObject).
/// 
/// 인스펙터에서 SkillData의 levelN_EmberPool에 등록 → 스킬 강화 시 등장.
/// 각 불씨는 하나의 효과 타입 + 수치를 가짐. 복합 효과는 별도 EmberData로 분리.
/// 
/// 사용 예:
///   - "데미지 +30%" : DamagePercent, effectValue=30
///   - "차지 +2" : ExtraCharges, effectValue=2
///   - "비행 거리 최대 +80%" : DistanceScaling, effectValue=80
/// </summary>
[CreateAssetMenu(fileName = "EM_", menuName = "Skill System/Ember Data")]
public class EmberData : ScriptableObject
{
    [Header("표시 정보")]
    public string emberName;
    public Sprite icon;

    [TextArea(2, 4)]
    [Tooltip("툴팁 설명. 수치 자리에 {0}을 쓰면 effectValue로 치환됨.\n" +
             "예: '데미지가 {0}% 증가합니다.' → '데미지가 30% 증가합니다.'")]
    public string description;

    [Header("효과")]
    [Tooltip("불씨의 효과 타입.")]
    public EmberEffectType effectType;

    [Tooltip("효과 수치. 타입에 따라 의미가 다름:\n" +
         "- DamagePercent / CritChanceBonus / CritDamagePercent / RangeMultiplier / DurationPercent: 백분율 (예: 30 = 30%)\n" +
         "- CooldownReduction / HitCooldownReduction / MaxChargeTimeReduction: 절대 초 (예: 0.5 = 0.5초)\n" +
         "- ExtraCharges: 정수 추가량 (예: 1 = +1발)\n" +
         "- DistanceScaling: 최대 누적 백분율 (예: 80 = 최대 +80%까지)\n" +
         "- FanFocusSinglePoint: 후속 발사체 피해 감소율 (예: 50 = 2번째부터 -50%)\n" +
         "- SecondaryBlast: 2차 폭발 데미지 비율 (예: 50 = 1차의 50% 데미지로 한 번 더)")]
    public float effectValue;

    // ========================================================================
    // 헬퍼
    // ========================================================================

    /// <summary>설명 텍스트에 effectValue를 채워 반환 (UI 툴팁용).</summary>
    public string GetFormattedDescription()
    {
        return string.IsNullOrEmpty(description)
            ? ""
            : string.Format(description, effectValue);
    }
}
