using UnityEngine;
using TMPro;

/// <summary>
/// 스킬 강화 오브젝트
/// - 플레이어 접근 시 UI 표시
/// - F키 상호작용 시 강화 UI 열기
/// - 무제한 사용 가능 (별조각 있는 한)
/// </summary>
public class SkillUpgradeObject : MonoBehaviour
{
    #region Serialized Fields
    [Header("상호작용 설정")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private string objectName = "빛나는 우물";
    [SerializeField] private string objectDescription = "별조각을 소모하여 스킬을 강화할 수 있습니다.";

    [Header("UI 참조")]
    [SerializeField] private GameObject interactionPromptUI;
    [SerializeField] private TextMeshProUGUI nameTextUI;
    [SerializeField] private TextMeshProUGUI descriptionTextUI;

    [Header("Glow 설정")]
    [SerializeField] private Color glowColor = new Color(0.5f, 0.8f, 1f, 1f);
    [SerializeField] private float glowIntensity = 1.5f;
    #endregion

    #region Private Fields
    private bool isPlayerInRange = false;
    private bool isUIOpen = false;

    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionSelfGlowID = Shader.PropertyToID("_EmissionSelfGlow");
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();

        if (interactionPromptUI != null)
            interactionPromptUI.SetActive(false);
    }

    private void Start()
    {
        ApplyGlow(glowIntensity);
    }

    private void Update()
    {
        if (isPlayerInRange && !isUIOpen && Input.GetKeyDown(interactKey))
        {
            OnInteract();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = true;
        ShowInteractionUI(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = false;
        ShowInteractionUI(false);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 상호작용 UI 표시/숨기기
    /// </summary>
    private void ShowInteractionUI(bool show)
    {
        if (interactionPromptUI != null)
        {
            if (show)
            {
                if (nameTextUI != null)
                    nameTextUI.text = objectName;

                if (descriptionTextUI != null)
                    descriptionTextUI.text = objectDescription;
            }

            interactionPromptUI.SetActive(show);
        }
    }

    /// <summary>
    /// F키 상호작용 시 호출
    /// </summary>
    private void OnInteract()
    {
        isUIOpen = true;
        ShowInteractionUI(false);

        // 강화 UI 열기
        if (SkillUpgradeUI.Instance != null)
        {
            SkillUpgradeUI.Instance.Open(this);
        }
        else
        {
            Debug.LogWarning("[SkillUpgradeObject] SkillUpgradeUI.Instance가 없습니다!");
            isUIOpen = false;
        }
    }

    /// <summary>
    /// Glow 적용
    /// </summary>
    private void ApplyGlow(float intensity)
    {
        foreach (var rend in renderers)
        {
            if (rend == null) continue;

            rend.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorID, glowColor * intensity);
            propertyBlock.SetFloat(EmissionSelfGlowID, intensity);
            rend.SetPropertyBlock(propertyBlock);
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 강화 UI가 닫힐 때 호출
    /// </summary>
    public void OnUIClosed()
    {
        isUIOpen = false;
    }
    #endregion
}
