using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 한 카드 슬롯의 UI 참조들을 묶은 구조체.
/// 카드 N장을 인스펙터에서 배열로 관리하기 편하도록.
/// </summary>
[System.Serializable]
public class SkillCardSlot
{
    [Tooltip("카드 전체 루트 (활성/비활성용).")]
    public GameObject root;

    [Tooltip("카드 클릭 받을 Button 컴포넌트.")]
    public Button clickButton;

    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI cooldownText;
    public TextMeshProUGUI descriptionText;
}

/// <summary>
/// 마법 카드 스킬 선택 UI — 마녀의 카드 보상 등에서 호출.
/// 
/// 동작 흐름:
///   1. Open(skillPool) — 외부에서 스킬 풀 받음
///   2. 풀에서 카드 슬롯 수만큼 랜덤 뽑아 표시
///   3. 사용자 선택:
///      - 카드 클릭 → 스킬 획득 (SkillManager.AutoEquipSkill)
///      - "업그레이드 포인트로 전환" → 업그레이드 모드 진입 (다음 단계)
///      - "새로 고침" → 풀에서 다시 뽑기 (제한 횟수)
/// </summary>
public class SkillCardUI : MonoBehaviour
{
    public static SkillCardUI Instance { get; private set; }

    [Header("루트 패널 (활성/비활성)")]
    [SerializeField] private GameObject overlayPanel;

    [Header("타이틀")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("카드 슬롯들 (인스펙터에서 2개 등록)")]
    [SerializeField] private SkillCardSlot[] cardSlots;

    [Header("하단 버튼")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button refreshButton;

    [Header("새로고침 설정")]
    [Tooltip("최대 새로고침 횟수.")]
    [SerializeField] private int maxRefreshCount = 3;

    [Tooltip("새로고침 남은 횟수 표시할 TMP (옵션).")]
    [SerializeField] private TextMeshProUGUI refreshCountText;

    [Header("동작")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    private List<SkillData> currentPool;         // 외부에서 받은 전체 풀
    private List<SkillData> displayedCards;       // 현재 화면 표시 중인 카드
    private System.Action<SkillData> onCardPicked;
    private System.Action onUpgradeRequested;
    private int remainingRefreshes;
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

        // 버튼 리스너 등록
        if (upgradeButton != null) upgradeButton.onClick.AddListener(OnUpgradeButtonClicked);
        if (refreshButton != null) refreshButton.onClick.AddListener(OnRefreshButtonClicked);

        // 각 카드 클릭 리스너 등록
        if (cardSlots != null)
        {
            for (int i = 0; i < cardSlots.Length; i++)
            {
                int index = i; // 클로저 캡처
                if (cardSlots[i] != null && cardSlots[i].clickButton != null)
                {
                    cardSlots[i].clickButton.onClick.AddListener(() => OnCardClicked(index));
                }
            }
        }
    }

    private void Update()
    {
        if (!isOpen) return;
        if (Input.GetKeyDown(closeKey)) Close();
    }

    // ========================================================================
    // 외부 API
    // ========================================================================

    /// <summary>
    /// 카드 선택 화면 열기.
    /// </summary>
    /// <param name="skillPool">스킬 풀</param>
    /// <param name="onPicked">카드 픽 시 콜백</param>
    /// <param name="onUpgradeRequested">"업그레이드 포인트로 전환" 클릭 시 콜백 (외부에서 캡슐 소진 처리 등).</param>
    public void Open(
        List<SkillData> skillPool,
        System.Action<SkillData> onPicked = null,
        System.Action onUpgradeRequested = null)
    {
        if (isOpen) return;
        if (skillPool == null || skillPool.Count == 0)
        {
            Debug.LogWarning("[MagicCardUI] 스킬 풀이 비어있음 — 화면 안 엶");
            return;
        }

        currentPool = new List<SkillData>(skillPool);
        remainingRefreshes = maxRefreshCount;
        onCardPicked = onPicked;
        this.onUpgradeRequested = onUpgradeRequested;  // ← 저장

        isOpen = true;
        if (overlayPanel != null) overlayPanel.SetActive(true);
        Time.timeScale = 0f;

        PickAndDisplayCards();
        UpdateRefreshText();
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        if (overlayPanel != null) overlayPanel.SetActive(false);
        Time.timeScale = 1f;

        currentPool = null;
        displayedCards = null;
        onCardPicked = null;
        onUpgradeRequested = null;
    }

    // ========================================================================
    // 카드 표시
    // ========================================================================

    /// <summary>풀에서 슬롯 수만큼 랜덤 뽑아 화면에 표시.</summary>
    /// <summary>풀에서 슬롯 수만큼 랜덤 뽑아 화면에 표시.</summary>
    private void PickAndDisplayCards()
    {
        displayedCards = new List<SkillData>();

        if (currentPool == null || currentPool.Count == 0 || cardSlots == null) return;

        // Fisher-Yates shuffle 후 N장 뽑기
        List<SkillData> shuffled = new List<SkillData>(currentPool);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int rand = Random.Range(i, shuffled.Count);
            (shuffled[i], shuffled[rand]) = (shuffled[rand], shuffled[i]);
        }

        int takeCount = Mathf.Min(cardSlots.Length, shuffled.Count);
        for (int i = 0; i < takeCount; i++)
        {
            if (shuffled[i] != null) displayedCards.Add(shuffled[i]);
        }

        // [NEW] 슬롯 가득 찼는지 — 찼으면 카드 클릭 막기 (업그레이드 전환만 허용)
        bool slotsFull = (SkillManager.Instance != null && !SkillManager.Instance.HasEmptySlot());

        // 카드 슬롯에 표시
        for (int i = 0; i < cardSlots.Length; i++)
        {
            SkillCardSlot slot = cardSlots[i];
            if (slot == null) continue;

            if (i < displayedCards.Count)
            {
                SkillData s = displayedCards[i];
                if (slot.root != null) slot.root.SetActive(true);
                if (slot.iconImage != null) slot.iconImage.sprite = s.skillIcon;
                if (slot.nameText != null) slot.nameText.text = s.skillName;
                if (slot.cooldownText != null) slot.cooldownText.text = $"{s.baseCooldown:F0}초";
                if (slot.descriptionText != null) slot.descriptionText.text = s.description;

                // [NEW] 슬롯 가득 차면 카드 클릭 비활성화 (회색 처리)
                if (slot.clickButton != null) slot.clickButton.interactable = !slotsFull;
            }
            else
            {
                if (slot.root != null) slot.root.SetActive(false);
            }
        }
    }

    private void UpdateRefreshText()
    {
        if (refreshCountText != null)
            refreshCountText.text = remainingRefreshes.ToString();
    }

    // ========================================================================
    // 입력 처리
    // ========================================================================

    /// <summary>카드 클릭 → 그 스킬을 SkillManager에 자동 장착.</summary>
    /// <summary>카드 클릭 → 그 스킬을 SkillManager에 자동 장착.</summary>
    private void OnCardClicked(int index)
    {
        // [NEW] 슬롯 가득 차면 픽 차단 — 업그레이드 전환만 가능
        if (SkillManager.Instance != null && !SkillManager.Instance.HasEmptySlot())
        {
            Debug.Log("[SkillCardUI] 슬롯이 가득 참 — 카드 픽 불가. 업그레이드 전환을 이용하세요.");
            return;
        }

        if (displayedCards == null || index < 0 || index >= displayedCards.Count) return;

        SkillData picked = displayedCards[index];
        if (picked == null) return;

        // 외부 콜백이 등록되어 있으면 그쪽에 위임 (드롭/장착 등은 호출자 결정)
        if (onCardPicked != null)
        {
            Debug.Log($"[MagicCardUI] '{picked.skillName}' 픽 — 외부 콜백 호출");
            onCardPicked.Invoke(picked);
            Close();
            return;
        }

        // 콜백 없으면 폴백: 자동 장착
        bool success = SkillManager.Instance.AutoEquipSkill(picked);
        if (success)
        {
            Debug.Log($"[MagicCardUI] '{picked.skillName}' 자동 장착");
            Close();
        }
        else
        {
            Debug.LogWarning($"[MagicCardUI] '{picked.skillName}' 장착 실패 — 슬롯 가득 찬 듯");
        }
    }

    /// <summary>
    /// "업그레이드 포인트로 전환" 클릭.
    /// 외부 콜백 우선 호출 (캡슐 소진 처리 등) → 카드 UI 닫고 강화 모드 진입.
    /// </summary>
    private void OnUpgradeButtonClicked()
    {
        // 외부 콜백 호출 — 보통 InteractableRewardObject가 캡슐 소진 처리
        onUpgradeRequested?.Invoke();

        // 카드 UI 닫고 강화 모드 진입
        Close();

        if (UpgradeModeController.Instance != null)
        {
            UpgradeModeController.Instance.EnterUpgradeMode();
        }
        else
        {
            Debug.LogWarning("[SkillCardUI] UpgradeModeController 미존재 — 업그레이드 모드 진입 실패");
        }
    }

    /// <summary>"새로 고침" 클릭 — 풀에서 다시 뽑기.</summary>
    private void OnRefreshButtonClicked()
    {
        if (remainingRefreshes <= 0)
        {
            Debug.Log("[MagicCardUI] 새로고침 횟수 소진");
            return;
        }

        remainingRefreshes--;
        UpdateRefreshText();

        PickAndDisplayCards();
    }
}
