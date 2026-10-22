// KnowledgeArchiveUI.cs

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// "지식의 서고" UI 컨트롤러.
/// 탭 전환, 좌측 항목 리스트 생성, 우측 상세 패널 표시,
/// 강화 버튼 처리, ESC 닫기를 담당한다.
/// </summary>
public class KnowledgeArchiveUI : MonoBehaviour
{
    public static KnowledgeArchiveUI Instance { get; private set; }

    // ============================
    //  Inspector 연결
    // ============================

    [Header("전체 패널")]
    [SerializeField] private GameObject archivePanel;

    [Header("탭 버튼")]
    [SerializeField] private Button tabStatButton;
    [SerializeField] private Button tabSpecialButton;
    [SerializeField] private Button tabSkillButton;

    [Header("탭 하이라이트 (선택된 탭 표시용)")]
    [SerializeField] private GameObject tabStatHighlight;
    [SerializeField] private GameObject tabSpecialHighlight;
    [SerializeField] private GameObject tabSkillHighlight;

    [Header("좌측 리스트")]
    [SerializeField] private Transform listContainer;
    [SerializeField] private GameObject slotPrefab;

    [Header("우측 상세 패널")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private TextMeshProUGUI detailNameText;
    [SerializeField] private TextMeshProUGUI detailDescriptionText;
    [SerializeField] private TextMeshProUGUI detailLevelText;
    [SerializeField] private TextMeshProUGUI detailCostText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TextMeshProUGUI upgradeButtonText;

    [Header("보유 재화 표시")]
    [SerializeField] private TextMeshProUGUI stardustAmountText;

    [Header("Export 이미지 (Figma에서 가져온 배경들)")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image tabBarImage;
    [SerializeField] private Image detailPanelImage;

    [Header("서고 열릴 때 숨길 HUD")]
    [SerializeField, Tooltip("플레이어 HP, EXP, 재화 UI")]
    private GameObject gameUI;
    [SerializeField, Tooltip("스킬 슬롯, 닷지 등 전투 UI")]
    private GameObject combatCanvas;

    // ============================
    //  런타임 변수
    // ============================

    private UpgradeTabType currentTab = UpgradeTabType.Stat;
    private StatUpgradeDataSO selectedUpgrade;
    private List<UpgradeSlot> activeSlots = new List<UpgradeSlot>();
    private bool isOpen = false;

    // ============================
    //  초기화
    // ============================

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 패널 비활성화
        if (archivePanel != null)
            archivePanel.SetActive(false);

        // 탭 버튼 이벤트 연결
        tabStatButton?.onClick.AddListener(() => SwitchTab(UpgradeTabType.Stat));
        tabSpecialButton?.onClick.AddListener(() => SwitchTab(UpgradeTabType.Special));
        tabSkillButton?.onClick.AddListener(() => SwitchTab(UpgradeTabType.Skill));

        // 강화 버튼 이벤트 연결
        upgradeButton?.onClick.AddListener(OnUpgradeButtonClicked);
    }

    private void OnEnable()
    {
        // 매니저의 강화 이벤트 구독 (강화 성공 시 UI 갱신)
        if (KnowledgeArchiveManager.Instance != null)
            KnowledgeArchiveManager.Instance.OnUpgradeChanged += RefreshAll;
    }

    private void OnDisable()
    {
        if (KnowledgeArchiveManager.Instance != null)
            KnowledgeArchiveManager.Instance.OnUpgradeChanged -= RefreshAll;
    }

    private void Update()
    {
        if (!isOpen) return;

        // ESC로 닫기
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    // ============================
    //  열기 / 닫기
    // ============================

    /// <summary>
    /// 서고 UI 열기. NPC 상호작용 등에서 호출.
    /// </summary>
    public void Open()
    {
        if (archivePanel == null) return;

        isOpen = true;
        archivePanel.SetActive(true);

        // 전투 HUD 숨기기
        SetHUDVisible(false);

        // 기본 탭으로 초기화
        SwitchTab(UpgradeTabType.Stat);

        // 상세 패널 초기화
        ClearDetail();

        // 재화 갱신
        RefreshStardustDisplay();

        // 플레이어 조작 잠금
        LockPlayer(true);

        // 커서 표시
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[지식의 서고] UI 열림");
    }

    /// <summary>
    /// 서고 UI 닫기
    /// </summary>
    public void Close()
    {
        if (archivePanel == null) return;

        isOpen = false;
        archivePanel.SetActive(false);

        selectedUpgrade = null;

        // 전투 HUD 복원
        SetHUDVisible(true);

        // 플레이어 조작 해제
        LockPlayer(false);

        Debug.Log("[지식의 서고] UI 닫힘");
    }

    /// <summary>
    /// UI가 열려있는지 확인
    /// </summary>
    public bool IsOpen() => isOpen;

    // ============================
    //  HUD 표시/숨김
    // ============================

    /// <summary>
    /// 전투 HUD(GameUI, Combat_Canvas)의 표시 여부를 제어한다.
    /// 서고 UI가 열릴 때 숨기고, 닫힐 때 복원한다.
    /// SerializeField가 비어있으면 이름으로 자동 탐색한다.
    /// </summary>
    private void SetHUDVisible(bool visible)
    {
        // 숨기기 전에 찾아서 캐싱 (활성 상태일 때만 Find 가능)
        if (!visible)
        {
            if (gameUI == null)
                gameUI = GameObject.Find("GameUI");

            if (combatCanvas == null)
                combatCanvas = GameObject.Find("Combat_Canvas");
        }

        if (gameUI != null)
            gameUI.SetActive(visible);

        if (combatCanvas != null)
            combatCanvas.SetActive(visible);
    }

    // ============================
    //  탭 전환
    // ============================

    /// <summary>
    /// 탭을 전환하고 해당 탭의 항목을 좌측 리스트에 표시
    /// </summary>
    private void SwitchTab(UpgradeTabType tab)
    {
        currentTab = tab;

        // 탭 하이라이트 갱신
        if (tabStatHighlight != null) tabStatHighlight.SetActive(tab == UpgradeTabType.Stat);
        if (tabSpecialHighlight != null) tabSpecialHighlight.SetActive(tab == UpgradeTabType.Special);
        if (tabSkillHighlight != null) tabSkillHighlight.SetActive(tab == UpgradeTabType.Skill);

        // 리스트 갱신
        PopulateList();

        // 상세 패널 초기화
        ClearDetail();
    }

    // ============================
    //  좌측 리스트
    // ============================

    /// <summary>
    /// 현재 탭의 항목들로 좌측 리스트를 채운다
    /// </summary>
    private void PopulateList()
    {
        // 기존 슬롯 정리
        ClearList();

        if (KnowledgeArchiveManager.Instance == null) return;

        List<StatUpgradeDataSO> upgrades = KnowledgeArchiveManager.Instance.GetUpgradesByTab(currentTab);

        foreach (var data in upgrades)
        {
            // 슬롯 생성
            GameObject slotObj = Instantiate(slotPrefab, listContainer);
            UpgradeSlot slot = slotObj.GetComponent<UpgradeSlot>();

            if (slot != null)
            {
                bool isVisible = KnowledgeArchiveManager.Instance.IsVisible(data);
                int currentLevel = KnowledgeArchiveManager.Instance.GetCurrentLevel(data);

                slot.Setup(data, isVisible, currentLevel, OnSlotSelected);
                activeSlots.Add(slot);
            }
        }
    }

    /// <summary>
    /// 좌측 리스트의 모든 슬롯을 제거
    /// </summary>
    private void ClearList()
    {
        foreach (var slot in activeSlots)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }
        activeSlots.Clear();
    }

    // ============================
    //  항목 선택 → 상세 패널
    // ============================

    /// <summary>
    /// 좌측 리스트에서 항목이 선택되었을 때 호출 (슬롯의 콜백)
    /// </summary>
    private void OnSlotSelected(StatUpgradeDataSO data)
    {
        selectedUpgrade = data;
        ShowDetail(data);

        // 선택된 슬롯 하이라이트 갱신
        foreach (var slot in activeSlots)
        {
            slot.SetSelected(slot.Data == data);
        }
    }

    /// <summary>
    /// 우측 상세 패널에 선택된 항목의 정보를 표시
    /// </summary>
    private void ShowDetail(StatUpgradeDataSO data)
    {
        if (detailPanel != null)
            detailPanel.SetActive(true);

        if (KnowledgeArchiveManager.Instance == null) return;

        int currentLevel = KnowledgeArchiveManager.Instance.GetCurrentLevel(data);
        bool isMaxLevel = KnowledgeArchiveManager.Instance.IsMaxLevel(data);
        bool isVisible = KnowledgeArchiveManager.Instance.IsVisible(data);

        // 비공개 항목
        if (!isVisible)
        {
            if (detailNameText != null) detailNameText.text = "???";
            if (detailDescriptionText != null) detailDescriptionText.text = "아직 발견되지 않은 지식입니다.";
            if (detailLevelText != null) detailLevelText.text = "";
            if (detailCostText != null) detailCostText.text = "";
            if (upgradeButton != null) upgradeButton.gameObject.SetActive(false);
            return;
        }

        // 이름
        if (detailNameText != null)
            detailNameText.text = data.upgradeName;

        // 설명 (현재 레벨 수치 대입)
        if (detailDescriptionText != null)
            detailDescriptionText.text = data.GetFormattedDescription(currentLevel);

        // 레벨
        if (detailLevelText != null)
            detailLevelText.text = $"Lv. {currentLevel} / {data.maxLevel}";

        // 비용 및 버튼
        if (isMaxLevel)
        {
            // 만렙
            if (detailCostText != null) detailCostText.text = "";
            if (upgradeButton != null) upgradeButton.gameObject.SetActive(false);
            if (detailLevelText != null) detailLevelText.text = $"Lv. {currentLevel} / {data.maxLevel} (MAX)";
        }
        else
        {
            // 다음 레벨 비용 표시
            int nextLevel = currentLevel + 1;
            int cost = data.GetCostForLevel(nextLevel);

            if (detailCostText != null)
                detailCostText.text = $"{cost}";

            // 강화 버튼 활성화/비활성화
            if (upgradeButton != null)
            {
                upgradeButton.gameObject.SetActive(true);
                bool canUpgrade = KnowledgeArchiveManager.Instance.CanUpgrade(data);
                upgradeButton.interactable = canUpgrade;

                if (upgradeButtonText != null)
                    upgradeButtonText.text = canUpgrade ? "강화" : "재화 부족";
            }
        }
    }

    /// <summary>
    /// 상세 패널을 빈 상태로 초기화
    /// </summary>
    private void ClearDetail()
    {
        selectedUpgrade = null;

        if (detailPanel != null)
            detailPanel.SetActive(false);
    }

    // ============================
    //  강화 버튼
    // ============================

    /// <summary>
    /// 강화 버튼 클릭 시 호출
    /// </summary>
    private void OnUpgradeButtonClicked()
    {
        if (selectedUpgrade == null) return;
        if (KnowledgeArchiveManager.Instance == null) return;

        bool success = KnowledgeArchiveManager.Instance.TryUpgrade(selectedUpgrade);

        if (success)
        {
            Debug.Log($"[지식의 서고] {selectedUpgrade.upgradeName} 강화 성공!");
            // RefreshAll은 매니저의 OnUpgradeChanged 이벤트로 자동 호출됨
        }
    }

    // ============================
    //  전체 갱신
    // ============================

    /// <summary>
    /// 강화 성공 후 전체 UI를 갱신
    /// </summary>
    private void RefreshAll()
    {
        // 재화 갱신
        RefreshStardustDisplay();

        // 리스트 갱신
        PopulateList();

        // 선택된 항목이 있으면 상세 패널도 갱신
        if (selectedUpgrade != null)
        {
            ShowDetail(selectedUpgrade);

            // 슬롯 하이라이트 복원
            foreach (var slot in activeSlots)
            {
                slot.SetSelected(slot.Data == selectedUpgrade);
            }
        }
    }

    /// <summary>
    /// 보유 별가루 표시 갱신
    /// </summary>
    private void RefreshStardustDisplay()
    {
        if (stardustAmountText != null && PlayerStats.Instance != null)
            stardustAmountText.text = PlayerStats.Instance.Stardust.ToString("N0");
    }

    // ============================
    //  유틸
    // ============================

    /// <summary>
    /// 플레이어 조작 잠금/해제
    /// </summary>
    private void LockPlayer(bool locked)
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            player.isSkillCasting = locked;
    }
}
