using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// 보상 제단 오브젝트
/// - 접근 시 설명 UI 표시
/// - F키로 선택 UI 열기
/// - 1회용 (선택 후 비활성화)
/// </summary>
public class RewardInteractable : MonoBehaviour
{
    #region Serialized Fields
    [Header("제단 정보")]
    [SerializeField] private string altarName = "신비로운 제단";
    [SerializeField, TextArea(2, 4)]
    private string altarDescription = "이 제단에서 보상을 선택할 수 있습니다.";

    [Header("보상 풀 (등급별)")]
    [SerializeField] private RewardPoolSO minorPool;
    [SerializeField] private RewardPoolSO moderatePool;
    [SerializeField] private RewardPoolSO majorPool;

    [Header("상호작용 설정")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("UI 참조")]
    [SerializeField] private GameObject promptUI;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("이펙트")]
    [SerializeField] private GameObject selectEffectPrefab;
    [SerializeField] private GameObject rewardEffectPrefab;

    [Header("Glow 설정")]
    [SerializeField] private Color glowColor = new Color(0.8f, 0.6f, 1f, 1f);
    [SerializeField] private float glowIntensity = 1.5f;
    [SerializeField] private float glowFadeOutDuration = 0.5f;
    #endregion

    #region Private Fields
    private bool isPlayerInRange = false;
    private bool isInteracted = false;

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

        if (promptUI != null)
            promptUI.SetActive(false);
    }

    private void Start()
    {
        ApplyGlow(glowIntensity);
    }

    private void Update()
    {
        if (isPlayerInRange && !isInteracted && Input.GetKeyDown(interactKey))
        {
            OnInteract();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isInteracted || !other.CompareTag("Player"))
            return;

        isPlayerInRange = true;
        ShowPromptUI(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (isInteracted || !other.CompareTag("Player"))
            return;

        isPlayerInRange = false;
        ShowPromptUI(false);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 프롬프트 UI 표시/숨기기
    /// </summary>
    private void ShowPromptUI(bool show)
    {
        if (promptUI == null) return;

        if (show)
        {
            if (nameText != null)
                nameText.text = altarName;

            if (descriptionText != null)
                descriptionText.text = altarDescription;
        }

        promptUI.SetActive(show);
    }

    /// <summary>
    /// F키 상호작용
    /// </summary>
    private void OnInteract()
    {
        ShowPromptUI(false);

        // 선택 UI 열기
        RewardSelectionUI.Instance?.Open(this, minorPool, moderatePool, majorPool);
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

    /// <summary>
    /// Glow 페이드 아웃
    /// </summary>
    private IEnumerator GlowFadeOutRoutine()
    {
        float elapsed = 0f;

        while (elapsed < glowFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / glowFadeOutDuration;
            float currentIntensity = Mathf.Lerp(glowIntensity, 0f, t);
            ApplyGlow(currentIntensity);
            yield return null;
        }

        ApplyGlow(0f);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 보상 선택 완료 시 호출 (RewardSelectionUI에서 호출)
    /// </summary>
    public void OnRewardSelected(RewardTier selectedTier)
    {
        isInteracted = true;
        isPlayerInRange = false;

        // 선택 이펙트
        if (selectEffectPrefab != null)
        {
            GameObject effect = Instantiate(selectEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }

        // 보상 적용
        StartCoroutine(ApplyRewardRoutine(selectedTier));
    }

    /// <summary>
    /// 보상 적용 코루틴
    /// </summary>
    private IEnumerator ApplyRewardRoutine(RewardTier tier)
    {
        yield return new WaitForSeconds(0.3f);

        // 보상 이펙트
        if (rewardEffectPrefab != null)
        {
            GameObject effect = Instantiate(rewardEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }

        // 해당 등급 풀에서 보상 적용
        RewardPoolSO selectedPool = GetPoolByTier(tier);
        if (selectedPool != null)
        {
            RewardManager.Instance?.ApplyRewardFromPool(selectedPool);
        }

        // Glow 꺼짐
        StartCoroutine(GlowFadeOutRoutine());

        // 콜라이더 비활성화
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Debug.Log($"[RewardInteractable] {altarName} - {tier} 보상 선택 완료");
    }

    /// <summary>
    /// 등급에 맞는 풀 반환
    /// </summary>
    private RewardPoolSO GetPoolByTier(RewardTier tier)
    {
        switch (tier)
        {
            case RewardTier.Minor: return minorPool;
            case RewardTier.Moderate: return moderatePool;
            case RewardTier.Major: return majorPool;
            default: return minorPool;
        }
    }

    /// <summary>
    /// 선택 취소 시 호출
    /// </summary>
    public void OnSelectionCancelled()
    {
        // 다시 상호작용 가능
        if (isPlayerInRange)
        {
            ShowPromptUI(true);
        }
    }
    #endregion
}
