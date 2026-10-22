using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 스킬 강화 화면 전체 컨트롤러.
/// 
/// 외부 호출 흐름:
///   1. Open(category, key) — 슬롯의 다음 강화 레벨에 맞춰 화면 구성
///   2. 카드 클릭 → SkillManager.ApplyEmberToSlot() → 화면 자동 닫기
///   3. ESC → 취소 (강화 안 함)
/// 
/// 카드 배치 규칙 (slot.appliedEmbers 기준):
///   - 과거 레벨: 좌카드에 픽한 거 (Picked, 회색), 우카드 Empty
///   - 현재 레벨: 풀에서 2장 랜덤 뽑아 Active
///   - 미래 레벨: 양쪽 다 Empty
/// </summary>
public class EmberSelectionUI : MonoBehaviour
{
    public static EmberSelectionUI Instance { get; private set; }

    [Header("루트 패널 (활성/비활성)")]
    [SerializeField] private GameObject overlayPanel;

    [Header("상단 타이틀")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("스킬 정보 패널")]
    [SerializeField] private Image skillIcon;
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private TextMeshProUGUI skillCooldownText;
    [SerializeField] private TextMeshProUGUI skillDescriptionText;

    [Header("Lv2 카드 (좌/우)")]
    [SerializeField] private GameObject lv2Row;
    [SerializeField] private EmberCardUI lv2LeftCard;
    [SerializeField] private EmberCardUI lv2RightCard;

    [Header("Lv3 카드 (좌/우)")]
    [SerializeField] private GameObject lv3Row;
    [SerializeField] private EmberCardUI lv3LeftCard;
    [SerializeField] private EmberCardUI lv3RightCard;

    [Header("Lv4 카드 (좌/우)")]
    [SerializeField] private GameObject lv4Row;
    [SerializeField] private EmberCardUI lv4LeftCard;
    [SerializeField] private EmberCardUI lv4RightCard;

    [Header("동작 설정")]
    [Tooltip("화면 열려있는 동안 게임 시간 정지.")]
    [SerializeField] private bool pauseTimeWhenOpen = true;

    [Tooltip("화면 취소 키.")]
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;

    private SkillCategory currentCategory;
    private KeyCode currentKey;
    private bool isOpen = false;

    // ========================================================================
    // Unity Lifecycle
    // ========================================================================

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        if (overlayPanel != null) overlayPanel.SetActive(false);
    }

    private void Update()
    {
        if (!isOpen) return;
        if (Input.GetKeyDown(cancelKey)) Close();
    }

    // ========================================================================
    // 외부 API
    // ========================================================================

    /// <summary>
    /// 강화 화면 열기.
    /// 슬롯이 비어있거나 이미 Lv4 최대 강화면 무시.
    /// </summary>
    public void Open(SkillCategory category, KeyCode key)
    {
        if (isOpen) return;

        SkillSlot slot = SkillManager.Instance?.GetSlot(category, key);
        if (slot == null || slot.IsEmpty)
        {
            Debug.Log("[EmberSelectionUI] 슬롯이 비어 있어 강화 불가");
            return;
        }
        if (slot.IsEmberMax)
        {
            Debug.Log("[EmberSelectionUI] 이미 Lv4 최대 강화 — 추가 강화 불가");
            return;
        }

        currentCategory = category;
        currentKey = key;
        isOpen = true;

        if (overlayPanel != null) overlayPanel.SetActive(true);
        if (pauseTimeWhenOpen) Time.timeScale = 0f;

        RefreshUI(slot);
    }

    /// <summary>화면 닫기 (카드 픽 후 또는 ESC).</summary>
    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        if (overlayPanel != null) overlayPanel.SetActive(false);
        if (pauseTimeWhenOpen) Time.timeScale = 1f;

        ClearAllCards();
    }

    // ========================================================================
    // UI 갱신
    // ========================================================================

    /// <summary>슬롯 정보로 전체 화면 갱신.</summary>
    private void RefreshUI(SkillSlot slot)
    {
        SkillData skill = slot.skill;
        int currentEmberCount = slot.appliedEmbers.Count;
        int nextLevel = currentEmberCount + 2; // 0→Lv2, 1→Lv3, 2→Lv4

        // 상단 타이틀
        if (titleText != null)
            titleText.text = $"스킬 업그레이드 <size=80%>Lv{nextLevel}</size>";

        // 스킬 정보
        if (skillIcon != null) skillIcon.sprite = skill.skillIcon;
        if (skillNameText != null) skillNameText.text = $"[{currentKey}] {skill.skillName}";
        if (skillCooldownText != null) skillCooldownText.text = $"{skill.GetCooldown(slot.level):F0}초";
        if (skillDescriptionText != null) skillDescriptionText.text = skill.description;

        // 각 레벨 행 배치
        SetupLevelRow(0, lv2Row, lv2LeftCard, lv2RightCard, skill.level2EmberPool, slot);
        SetupLevelRow(1, lv3Row, lv3LeftCard, lv3RightCard, skill.level3EmberPool, slot);
        SetupLevelRow(2, lv4Row, lv4LeftCard, lv4RightCard, skill.level4EmberPool, slot);
    }

    /// <summary>
    /// 한 레벨 행의 가시성 + 좌/우 카드 상태 결정.
    ///   - 미래 레벨 → 행 자체 숨김
    ///   - 과거 레벨 → 행 표시, 좌카드 픽한 거 회색, 우카드 빈 상태
    ///   - 현재 레벨 → 행 표시, 풀에서 2장 랜덤 활성
    /// </summary>
    /// <param name="levelIndex">0=Lv2, 1=Lv3, 2=Lv4</param>
    private void SetupLevelRow(
        int levelIndex,
        GameObject rowContainer,
        EmberCardUI leftCard,
        EmberCardUI rightCard,
        List<EmberData> pool,
        SkillSlot slot)
    {
        int currentCount = slot.appliedEmbers.Count;

        // 미래 레벨 — 행 자체 숨김
        if (levelIndex > currentCount)
        {
            if (rowContainer != null) rowContainer.SetActive(false);
            return;
        }

        // 과거/현재 레벨 — 행 표시
        if (rowContainer != null) rowContainer.SetActive(true);

        if (levelIndex < currentCount)
        {
            // 과거 — 픽한 카드 1장(회색), 우측 Empty
            leftCard?.SetEmber(slot.appliedEmbers[levelIndex], EmberCardUI.CardState.Picked, null);
            rightCard?.Clear();
        }
        else
        {
            // 현재 — 풀에서 2장 랜덤 Active
            List<EmberData> picked = PickRandomFromPool(pool, 2);

            if (leftCard != null)
            {
                if (picked.Count > 0) leftCard.SetEmber(picked[0], EmberCardUI.CardState.Active, OnCardPicked);
                else leftCard.Clear();
            }
            if (rightCard != null)
            {
                if (picked.Count > 1) rightCard.SetEmber(picked[1], EmberCardUI.CardState.Active, OnCardPicked);
                else rightCard.Clear();
            }
        }
    }

    /// <summary>풀에서 중복 없이 N장 랜덤 뽑기 (Fisher-Yates shuffle).</summary>
    private List<EmberData> PickRandomFromPool(List<EmberData> pool, int count)
    {
        List<EmberData> result = new List<EmberData>();
        if (pool == null || pool.Count == 0) return result;

        List<EmberData> shuffled = new List<EmberData>(pool);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int rand = Random.Range(i, shuffled.Count);
            (shuffled[i], shuffled[rand]) = (shuffled[rand], shuffled[i]);
        }

        int takeCount = Mathf.Min(count, shuffled.Count);
        for (int i = 0; i < takeCount; i++)
        {
            if (shuffled[i] != null) result.Add(shuffled[i]);
        }
        return result;
    }

    /// <summary>모든 카드를 Empty로 리셋 — 화면 닫을 때.</summary>
    private void ClearAllCards()
    {
        lv2LeftCard?.Clear();
        lv2RightCard?.Clear();
        lv3LeftCard?.Clear();
        lv3RightCard?.Clear();
        lv4LeftCard?.Clear();
        lv4RightCard?.Clear();
    }

    // ========================================================================
    // 카드 클릭
    // ========================================================================

    /// <summary>카드 클릭 시 호출 — SkillManager에 불씨 부여 요청.</summary>
    private void OnCardPicked(EmberData ember)
    {
        if (ember == null) return;

        bool success = SkillManager.Instance.ApplyEmberToSlot(currentCategory, currentKey, ember);
        if (success)
        {
            Debug.Log($"[EmberSelectionUI] '{ember.emberName}' 부여 성공");
            Close();
        }
        else
        {
            Debug.LogWarning("[EmberSelectionUI] 불씨 부여 실패");
        }
    }
}
