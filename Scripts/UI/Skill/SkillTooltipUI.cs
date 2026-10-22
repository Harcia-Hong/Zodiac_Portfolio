using UnityEngine;
using TMPro; // TextMeshPro 사용
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 스킬 툴팁 UI를 관리합니다.
/// 평소에는 스킬 정보를, 강화 모드일 때는 강화 예측 정보를 보여줍니다.
/// </summary>
public class SkillTooltipUI : MonoBehaviour
{
    [Header("UI 요소 연결")]
    [SerializeField] private TextMeshProUGUI skillNameText;       // 이름 표시
    [SerializeField] private TextMeshProUGUI skillDescriptionText; // 설명 표시
    [SerializeField] private TextMeshProUGUI skillInfoText;       // 쿨타임/등급 표시
    [SerializeField] private RectTransform backgroundRect;        // 배경 박스 (크기 조절용)

    [Header("툴팁 위치 / 깊이")]
    [Tooltip("슬롯 위치 기준 오프셋. Y 양수=위로, X 음수=좌측으로 (예: -100,150,0).\n" +
         "스킬 슬롯이 화면 우측 하단이면 (-100, 200, 0) 정도로 좌상단에 띄우는 게 자연스러움.")]
    [SerializeField] private Vector3 tooltipOffset = new Vector3(0f, 150f, 0f);

    [Tooltip("툴팁이 다른 UI 위에 그려지도록 같은 부모 안에서 맨 마지막으로 이동.\n" +
             "끄면 하이어라키 순서대로 그려져 가려질 수 있음.")]
    [SerializeField] private bool bringToFront = true;

    private Canvas rootCanvas;

    // 싱글톤 추가 (어디서든 부르기 쉽게)
    public static SkillTooltipUI Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 캔버스 찾기 (부모 타고 올라감)
        if (GetComponentInParent<Canvas>() != null)
            rootCanvas = GetComponentInParent<Canvas>().rootCanvas;

        gameObject.SetActive(false); // 시작할 땐 숨김
    }

    /// <summary>
    /// 최종 표시 데미지 = 베이스 + 스탯 보정 + DamagePercent 불씨 합산.
    /// 크리는 무작위 요소라 툴팁 평균값에서 제외.
    /// </summary>
    private float CalculateTotalDamage(SkillData data, int level, List<EmberData> appliedEmbers)
    {
        if (PlayerStats.Instance == null) return data.GetDamage(level);

        // 1. 스킬 자체 기본 데미지 + 플레이어 스탯 보정
        float baseDmg = data.GetDamage(level);
        float playerStat = PlayerStats.Instance.GetStat(data.statType);
        float pre = baseDmg + playerStat * data.damageRatio;

        // 2. 불씨 DamagePercent 합산 적용 (백분율)
        float dmgPercent = SumEmberValue(appliedEmbers, EmberEffectType.DamagePercent);
        return pre * (1f + dmgPercent / 100f);
    }

    /// <summary>
    /// 최종 표시 쿨다운 = 베이스에서 불씨 CooldownReduction(절대 초) 차감 후 글로벌 쿨감 적용.
    /// SkillManager.CalculateFinalCooldown과 동일 흐름. 최소 0.1초 클램프.
    /// </summary>
    private float CalculateFinalCooldown(SkillData data, int level, List<EmberData> appliedEmbers)
    {
        float baseCool = data.GetCooldown(level);

        // 불씨 CooldownReduction — 절대 초 차감
        float emberReduction = SumEmberValue(appliedEmbers, EmberEffectType.CooldownReduction);
        float afterEmbers = Mathf.Max(0.1f, baseCool - emberReduction);

        // 글로벌 쿨감 (장비/패시브 등 PlayerStats 단)
        if (PlayerStats.Instance != null)
            return PlayerStats.Instance.GetReducedCooldown(afterEmbers);
        return afterEmbers;
    }

    public void Show(SkillData data, int currentLevel, Vector3 slotPosition, List<EmberData> appliedEmbers = null)
    {
        if (data == null) return;

        skillNameText.text = $"{data.skillName} <size=80%>(Lv.{currentLevel})</size>";

        // 쿨타임 — 불씨 CooldownReduction + 글로벌 쿨감 반영
        float finalCool = CalculateFinalCooldown(data, currentLevel, appliedEmbers);
        skillInfoText.text = $"[ {data.grade} | 쿨타임: {finalCool:F1}초 ]";

        // 데미지 — 불씨 DamagePercent + 스탯 보정 반영
        float totalDmg = CalculateTotalDamage(data, currentLevel, appliedEmbers);
        string dmgStr = $"<color=#ADD8E6>{totalDmg:F0}</color>";

        skillDescriptionText.text = BuildDescription(data, dmgStr, appliedEmbers);

        UpdateSizeAndPosition(slotPosition);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// [강화 미리보기 + 불씨 표시] 강화 모드 툴팁.
    /// 강화 모드에서도 현재 적용된 불씨를 함께 보여줌 — 강화 전후 빌드 비교용.
    /// </summary>
    public void ShowUpgradePreview(SkillData data, int currentLevel, Vector3 slotPosition, List<EmberData> appliedEmbers = null)
    {
        if (data == null) return;

        int nextLevel = currentLevel + 1;
        bool isMax = currentLevel >= 20;

        string lvStr = isMax ? "MAX" : $"<color=yellow>Lv.{nextLevel}</color>";
        skillNameText.text = $"{data.skillName} <size=80%>(Lv.{currentLevel} ▶ {lvStr})</size>";

        // 쿨타임 — 현재/다음 모두 불씨 반영
        float curCool = CalculateFinalCooldown(data, currentLevel, appliedEmbers);
        float nextCool = CalculateFinalCooldown(data, nextLevel, appliedEmbers);
        string coolStr = isMax ? "" : $" ▶ <color=yellow>{nextCool:F1}초</color>";
        skillInfoText.text = $"[ {data.grade} | 쿨타임: {curCool:F1}초{coolStr} ]";

        // 데미지 — 현재/다음 모두 불씨 반영
        float curDmg = CalculateTotalDamage(data, currentLevel, appliedEmbers);
        float nextDmg = CalculateTotalDamage(data, nextLevel, appliedEmbers);

        string dmgChangeStr = isMax
            ? $"<color=#ADD8E6>{curDmg:F0}</color>"
            : $"{curDmg:F0} ▶ <color=yellow>{nextDmg:F0}</color>";

        skillDescriptionText.text = BuildDescription(data, dmgChangeStr, appliedEmbers);

        UpdateSizeAndPosition(slotPosition);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// description 본문 + 적용 불씨 목록을 통합 빌드.
    /// 불씨 표시는 ember.description의 {0}을 effectValue로 치환한 풀 문장으로 출력.
    /// </summary>
    private string BuildDescription(SkillData data, string dmgStr, List<EmberData> appliedEmbers)
    {
        // 1) 기본 description 빌드
        string desc;
        if (data.description.Contains("{0}"))
            desc = string.Format(data.description, dmgStr);
        else
            desc = $"{data.description}\n\n[위력] {dmgStr}";

        // 2) 적용된 불씨 — 풀 문장(description) 사용
        if (appliedEmbers != null && appliedEmbers.Count > 0)
        {
            desc += "\n\n<color=#FFA500>─ 적용된 불씨 ─</color>";
            foreach (var ember in appliedEmbers)
            {
                if (ember == null) continue;

                // ember.description의 {0}을 effectValue로 치환
                // 예: "재사용 대기시간이 {0}초 감소합니다." + 4 → "재사용 대기시간이 4초 감소합니다."
                string emberLine = ember.description.Contains("{0}")
                    ? string.Format(ember.description, ember.effectValue)
                    : ember.description;

                desc += $"\n<color=#FFA500>▶</color> {emberLine}";
            }
        }

        return desc;
    }

    /// <summary>
    /// 불씨 effectType별로 effectValue를 사용자 친화적 라벨로 변환.
    /// 예: DamagePercent 30 → "+30%", CooldownReduction 4 → "-4.0초", ExtraCharges 1 → "+1"
    /// 
    /// effectType이 추가/변경되면 이 메서드도 같이 갱신할 것.
    /// </summary>
    private string GetEmberValueLabel(EmberData ember)
    {
        switch (ember.effectType)
        {
            // 백분율 (+N%)
            case EmberEffectType.DamagePercent:
            case EmberEffectType.CritChanceBonus:
            case EmberEffectType.CritDamagePercent:
            case EmberEffectType.RangeMultiplier:
            case EmberEffectType.DurationPercent:
                return $"+{ember.effectValue:F0}%";

            // 절대 초 (-N초)
            case EmberEffectType.CooldownReduction:
            case EmberEffectType.HitCooldownReduction:
            case EmberEffectType.MaxChargeTimeReduction:
                return $"-{ember.effectValue:F1}초";

            // 정수 추가량 (+N)
            case EmberEffectType.ExtraCharges:
                return $"+{ember.effectValue:F0}";

            // 거리 스케일링 (최대 +N%)
            case EmberEffectType.DistanceScaling:
                return $"최대 +{ember.effectValue:F0}%";

            // 액션 전용 특수 효과
            case EmberEffectType.FanFocusSinglePoint:
                return $"(일점사 -{ember.effectValue:F0}%)";
            case EmberEffectType.SecondaryBlast:
                return $"(2차 {ember.effectValue:F0}%)";

            // 미지 타입 — 단순 표시
            default:
                return $"{ember.effectValue:F1}";
        }
    }

    /// <summary>
    /// appliedEmbers에서 특정 effectType의 effectValue 합산.
    /// null/빈 리스트면 0 반환. 같은 타입 불씨가 여러 개여도 모두 누적.
    /// </summary>
    private float SumEmberValue(List<EmberData> embers, EmberEffectType type)
    {
        if (embers == null) return 0f;
        float sum = 0f;
        foreach (var e in embers)
        {
            if (e != null && e.effectType == type) sum += e.effectValue;
        }
        return sum;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 텍스트 길이에 맞춰 배경 크기 갱신 + 슬롯 기준 위치 이동.
    /// bringToFront 활성 시 매번 SetAsLastSibling으로 다른 UI 위에 그려지게 함.
    /// </summary>
    private void UpdateSizeAndPosition(Vector3 slotPosition)
    {
        // 다른 UI 위에 그려지도록 같은 부모 안에서 맨 마지막으로 이동
        if (bringToFront)
        {
            transform.SetAsLastSibling();
        }

        // 텍스트 길이에 맞춰 배경 크기 갱신
        LayoutRebuilder.ForceRebuildLayoutImmediate(backgroundRect);

        // 슬롯 기준 인스펙터 오프셋만큼 이동
        transform.position = slotPosition + tooltipOffset;
    }
}
