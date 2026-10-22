using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 보상 선택 UI
/// - 3가지 등급 중 1개 선택
/// - 선택 전에는 내용 비공개
/// </summary>
public class RewardSelectionUI : MonoBehaviour
{
    public static RewardSelectionUI Instance { get; private set; }

    #region Serialized Fields
    [Header("UI 패널")]
    [SerializeField] private GameObject selectionPanel;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("선택지 버튼")]
    [SerializeField] private Button minorButton;
    [SerializeField] private Button moderateButton;
    [SerializeField] private Button majorButton;

    [Header("선택지 텍스트")]
    [SerializeField] private TextMeshProUGUI minorTitleText;
    [SerializeField] private TextMeshProUGUI minorDescText;
    [SerializeField] private TextMeshProUGUI moderateTitleText;
    [SerializeField] private TextMeshProUGUI moderateDescText;
    [SerializeField] private TextMeshProUGUI majorTitleText;
    [SerializeField] private TextMeshProUGUI majorDescText;

    [Header("선택지 설명")]
    [SerializeField] private string minorTitle = "미미한 보상";
    [SerializeField, TextArea(1, 3)]
    private string minorDescription = "미미하지만 약간의 보상을 얻을 수 있습니다.";

    [SerializeField] private string moderateTitle = "적당한 보상";
    [SerializeField, TextArea(1, 3)]
    private string moderateDescription = "적당한 보상을 얻지만, 약간의 위험이 따릅니다.";

    [SerializeField] private string majorTitle = "막대한 보상";
    [SerializeField, TextArea(1, 3)]
    private string majorDescription = "막대한 보상을 얻지만, 큰 위험이 따릅니다.";

    [Header("닫기 설정")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;
    [SerializeField] private Button closeButton;

    [Header("애니메이션")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.2f;
    #endregion

    #region Private Fields
    private RewardInteractable currentAltar;
    private bool isOpen = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (selectionPanel != null)
            selectionPanel.SetActive(false);

        SetupButtons();
        SetupTexts();
    }

    private void Update()
    {
        if (!isOpen) return;

        if (Input.GetKeyDown(closeKey))
        {
            Close(false);
        }
    }
    #endregion

    #region Setup
    /// <summary>
    /// 버튼 이벤트 설정
    /// </summary>
    private void SetupButtons()
    {
        if (minorButton != null)
            minorButton.onClick.AddListener(() => OnSelectReward(RewardTier.Minor));

        if (moderateButton != null)
            moderateButton.onClick.AddListener(() => OnSelectReward(RewardTier.Moderate));

        if (majorButton != null)
            majorButton.onClick.AddListener(() => OnSelectReward(RewardTier.Major));

        if (closeButton != null)
            closeButton.onClick.AddListener(() => Close(false));
    }

    /// <summary>
    /// 텍스트 설정
    /// </summary>
    private void SetupTexts()
    {
        if (minorTitleText != null)
            minorTitleText.text = minorTitle;

        if (minorDescText != null)
            minorDescText.text = minorDescription;

        if (moderateTitleText != null)
            moderateTitleText.text = moderateTitle;

        if (moderateDescText != null)
            moderateDescText.text = moderateDescription;

        if (majorTitleText != null)
            majorTitleText.text = majorTitle;

        if (majorDescText != null)
            majorDescText.text = majorDescription;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 선택 UI 열기
    /// </summary>
    public void Open(RewardInteractable altar, RewardPoolSO minor, RewardPoolSO moderate, RewardPoolSO major)
    {
        if (isOpen) return;

        currentAltar = altar;
        isOpen = true;

        // UI 표시
        selectionPanel.SetActive(true);

        // 페이드 인
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true);
        }

        // 게임 일시정지
        Time.timeScale = 0f;

        // 커서 표시
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Debug.Log("[RewardSelectionUI] UI 열림");
    }

    /// <summary>
    /// 선택 UI 닫기
    /// </summary>
    public void Close(bool selected)
    {
        if (!isOpen) return;

        isOpen = false;

        // 페이드 아웃
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.DOFade(0f, fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    selectionPanel.SetActive(false);
                });
        }
        else
        {
            selectionPanel.SetActive(false);
        }

        // 게임 재개
        Time.timeScale = 1f;

        // 커서 숨기기
        //Cursor.visible = false;
        //Cursor.lockState = CursorLockMode.Locked;

        // 선택 안 하고 닫았으면 제단에 알림
        if (!selected && currentAltar != null)
        {
            currentAltar.OnSelectionCancelled();
        }

        currentAltar = null;

        Debug.Log("[RewardSelectionUI] UI 닫힘");
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 보상 선택 시
    /// </summary>
    private void OnSelectReward(RewardTier tier)
    {
        if (currentAltar == null) return;

        Debug.Log($"[RewardSelectionUI] {tier} 선택됨");

        // 제단에 선택 결과 전달
        currentAltar.OnRewardSelected(tier);

        // UI 닫기
        Close(true);
    }
    #endregion
}
