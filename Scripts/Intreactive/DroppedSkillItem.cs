using UnityEngine;
using TMPro;

/// <summary>
/// 바닥에 드랍된 스킬 아이템의 로직.
/// F키(교체) 또는 G키(분해) 상호작용을 처리합니다.
/// </summary>
public class DroppedSkillItem : MonoBehaviour
{
    [Header("스킬 정보")]
    [SerializeField]
    private SkillData skillData; // 이 아이템이 담고 있는 스킬의 데이터
    [SerializeField] private int skillLevel = 1;

    [Header("UI 및 상호작용")]
    [SerializeField]
    private GameObject interactionPromptUI; // "F-교체 / G-분해" UI
    [SerializeField]
    private KeyCode interactKey = KeyCode.F; // 교체 키
    [SerializeField]
    private KeyCode disassembleKey = KeyCode.G; // 분해 키

    [Header("UI 텍스트")]
    [SerializeField]
    private TextMeshProUGUI skillNameText;
    [SerializeField]
    private TextMeshProUGUI keysText;

    [Header("분해 설정")]
    [SerializeField]
    private float disassembleHoldTime = 1.0f; // G키를 누르고 있어야 하는 시간

    [Header("아이콘 설정")]
    [SerializeField]
    private SpriteRenderer iconRenderer;

    private float currentHoldTime = 0f;
    private bool isPlayerInRange = false;
    private bool isMouseHovering = false;

    private SkillManager playerSkillManager;

    private void Awake()
    {
        if (interactionPromptUI != null)
            interactionPromptUI.SetActive(false);
    }

    /// <summary>
    /// 이 아이템을 스폰한 Altar가 호출해주는 함수
    /// </summary>
    public void Initialize(SkillData dataToHold, int level = 1)
    {
        skillData = dataToHold;
        skillLevel = level;

        UpdateUIText();

        if (iconRenderer != null && skillData.skillIcon != null)
            iconRenderer.sprite = skillData.skillIcon;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            playerSkillManager = other.GetComponent<SkillManager>();

            CheckUIVisibility();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            ResetDisassembleHold();
            playerSkillManager = null;

            CheckUIVisibility();
        }
    }

    private void OnMouseEnter()
    {
        isMouseHovering = true;
        CheckUIVisibility();
    }

    private void OnMouseExit()
    {
        isMouseHovering = false;
        CheckUIVisibility();
    }

    private void CheckUIVisibility()
    {
        bool Show = isPlayerInRange || isMouseHovering;
        ShowInteractionUI(Show);
    }

    private void Update()
    {
        if (!isPlayerInRange) return;

        // --- F키 (교체) 처리 ---
        if (Input.GetKeyDown(interactKey))
        {
            AttemptEquipOrSwap();
        }

        // --- G키 (분해) 처리 ---
        if (Input.GetKey(disassembleKey))
        {
            currentHoldTime += Time.deltaTime;

            // TODO: 분해 게이지 UI 업데이트 (currentHoldTime / disassembleHoldTime)

            if (currentHoldTime >= disassembleHoldTime)
            {
                DisassembleItem();
            }
        }

        if (Input.GetKeyUp(disassembleKey))
        {
            ResetDisassembleHold();
        }
    }

    public SkillData GetSkillData()
    {
        return skillData;
    }
    public int GetSkillLevel()
    {
        return skillLevel;
    }

    /// <summary>
    /// F키: 스킬 교체 프로세스 시작
    /// </summary>
    private void AttemptEquipOrSwap()
    {
        if (skillData == null || playerSkillManager == null) return;

        bool equipSuccess = playerSkillManager.AutoEquipSkill(skillData, skillLevel);

        if (equipSuccess) Destroy(gameObject);
        else
        {
            playerSkillManager.StartSwapProcess(this);
            ShowInteractionUI(false);
        }
        // TODO:
        // 1. Player의 SkillManager.cs 스크립트 참조 가져오기
        // 2. SkillManager.ShowSwapUI(skillData) 호출
        // 3. (교체가 완료되면) 이 오브젝트는 파괴되어야 함
        // Destroy(gameObject);
    }

    /// <summary>
    /// G키 홀드: 아이템 분해
    /// </summary>
    private void DisassembleItem()
    {
        Debug.Log($"[DroppedSkillItem] '{skillData.skillName}' Lv.{skillLevel} 분해 완료!");
        Destroy(gameObject);
    }

    private void ResetDisassembleHold()
    {
        currentHoldTime = 0f;
        // TODO: 분해 게이지 UI 리셋
    }

    private void ShowInteractionUI(bool show)
    {
        if (interactionPromptUI != null)
        {
            // TODO: UI 텍스트에 skillData.skillName 등을 표시
            if (show)
                UpdateUIText();
            interactionPromptUI.SetActive(show);
        }
    }

    private void UpdateUIText()
    {
        if (skillData == null) return;

        if (skillNameText != null)
            skillNameText.text = skillData.skillName;

        if (keysText != null)
            keysText.text = "F - 장착 / G - 분해";
    }
}
