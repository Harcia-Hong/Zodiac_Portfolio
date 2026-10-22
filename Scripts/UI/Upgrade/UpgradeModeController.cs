using UnityEngine;
using TMPro;

/// <summary>
/// 불씨(Ember) 강화 모드 전체 흐름 관리.
/// 
/// 진입: EnterUpgradeMode() — 카드 UI에서 "업그레이드 포인트로 전환" 클릭 시 호출
/// 동작:
///   1. 화면에 "강화할 스킬 슬롯 클릭" 메시지 패널 표시
///   2. Time.timeScale = 0으로 게임 일시 정지
///   3. ESC → 취소
/// 슬롯 클릭 시: RequestEnhance(category, key) 호출 — 외부(SkillSlotUI)에서 호출
///   - 슬롯 비어있거나 Lv4 최대면 거부
///   - 정상이면 모드 종료 + EmberSelectionUI.Open
/// </summary>
public class UpgradeModeController : MonoBehaviour
{
    public static UpgradeModeController Instance { get; private set; }

    [Header("UI")]
    [Tooltip("강화 모드 안내 메시지 표시 패널 (Image + 자식 TMP).")]
    [SerializeField] private GameObject promptPanel;

    [Tooltip("메시지 텍스트 (옵션).")]
    [SerializeField] private TextMeshProUGUI promptText;

    [Header("동작 설정")]
    [SerializeField] private bool pauseTimeWhenActive = true;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;

    /// <summary>현재 강화 모드 활성 여부 (다른 시스템이 체크용).</summary>
    public bool IsActive { get; private set; }

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
        if (promptPanel != null) promptPanel.SetActive(false);
    }

    private void Update()
    {
        if (!IsActive) return;
        if (Input.GetKeyDown(cancelKey)) ExitUpgradeMode();
    }

    // ========================================================================
    // 외부 API — 모드 진입/취소
    // ========================================================================

    /// <summary>강화 모드 진입.</summary>
    public void EnterUpgradeMode()
    {
        if (IsActive) return;
        IsActive = true;

        if (promptPanel != null) promptPanel.SetActive(true);
        if (promptText != null) promptText.text = "강화할 스킬 슬롯 클릭  (ESC: 취소)";
        if (pauseTimeWhenActive) Time.timeScale = 0f;

        Debug.Log("[UpgradeModeController] 강화 모드 진입");
    }

    /// <summary>강화 모드 취소/종료.</summary>
    public void ExitUpgradeMode()
    {
        if (!IsActive) return;
        IsActive = false;

        if (promptPanel != null) promptPanel.SetActive(false);
        if (pauseTimeWhenActive) Time.timeScale = 1f;

        Debug.Log("[UpgradeModeController] 강화 모드 종료");
    }

    // ========================================================================
    // 슬롯 클릭 수신 — SkillSlotUI에서 호출
    // ========================================================================

    /// <summary>
    /// 슬롯 클릭 시 호출 — 강화 가능 여부 판정 후 EmberSelectionUI 호출.
    /// 비어있거나 Lv4 최대면 거부하고 모드 유지.
    /// </summary>
    public void RequestEnhance(SkillCategory category, KeyCode key)
    {
        if (!IsActive) return;

        if (SkillManager.Instance == null || EmberSelectionUI.Instance == null)
        {
            Debug.LogWarning("[UpgradeModeController] SkillManager/EmberSelectionUI 미존재");
            return;
        }

        SkillSlot slot = SkillManager.Instance.GetSlot(category, key);
        if (slot == null || slot.IsEmpty)
        {
            Debug.Log("[UpgradeModeController] 빈 슬롯 — 강화 불가");
            return;
        }
        if (slot.IsEmberMax)
        {
            Debug.Log("[UpgradeModeController] 이미 Lv4 최대 강화");
            return;
        }

        // 모드 종료 후 EmberSelectionUI 호출
        ExitUpgradeMode();
        EmberSelectionUI.Instance.Open(category, key);
    }
}
