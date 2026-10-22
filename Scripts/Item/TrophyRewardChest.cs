using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// 전리품 보상 상자
/// - 스테이지 클리어 시 스폰
/// - 플레이어가 F키로 열면 전리품을 TrophyInventory에 직접 추가
/// - 획득 연출 (아이콘 + 이름) 표시 후 비활성화
/// 
/// 사용 흐름:
///   1. StageManager가 클리어 시 상자 프리팹 스폰
///   2. Initialize()로 드랍 테이블 + 드랍 개수 설정
///   3. 플레이어 접근 → F키 → 전리품 획득
/// </summary>
public class TrophyRewardChest : MonoBehaviour
{
    [Header("상호작용 설정")]
    [SerializeField, Tooltip("상호작용 키")]
    private KeyCode interactKey = KeyCode.F;

    [SerializeField, Tooltip("상자 이름")]
    private string chestName = "전리품 상자";

    [Header("드랍 설정")]
    [SerializeField, Tooltip("전리품 드랍 테이블")]
    private TrophyLootTable lootTable;

    [SerializeField, Tooltip("상자에서 나오는 전리품 수")]
    private int dropCount = 1;

    [Header("UI 참조")]
    [SerializeField, Tooltip("상호작용 프롬프트 UI (F키 안내)")]
    private GameObject interactionPromptUI;

    [SerializeField, Tooltip("상자 이름 텍스트")]
    private TextMeshProUGUI nameTextUI;

    [SerializeField, Tooltip("획득 결과 표시 UI")]
    private GameObject resultUI;

    [SerializeField, Tooltip("획득 아이템 이름 텍스트")]
    private TextMeshProUGUI resultNameText;

    [SerializeField, Tooltip("획득 아이템 아이콘")]
    private SpriteRenderer resultIconRenderer;

    [Header("연출 설정")]
    [SerializeField, Tooltip("Glow 발광 색상")]
    private Color glowColor = new Color(1f, 0.85f, 0.3f, 1f);

    [SerializeField, Tooltip("Glow 발광 강도")]
    private float glowIntensity = 1.5f;

    [SerializeField, Tooltip("획득 연출 표시 시간")]
    private float resultDisplayDuration = 2f;

    // === 상태 ===
    private bool isPlayerInRange = false;
    private bool isOpened = false;

    // === Glow ===
    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    #region Unity Lifecycle

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();

        if (interactionPromptUI != null)
            interactionPromptUI.SetActive(false);

        if (resultUI != null)
            resultUI.SetActive(false);
    }

    private void Start()
    {
        ApplyGlow(glowIntensity);
    }

    private void Update()
    {
        if (isPlayerInRange && !isOpened && Input.GetKeyDown(interactKey))
        {
            OpenChest();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (isOpened) return;

        isPlayerInRange = true;
        ShowPrompt(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = false;
        ShowPrompt(false);
    }

    #endregion

    #region 초기화

    /// <summary>
    /// 외부에서 상자 세팅 (스폰 시 호출)
    /// - 드랍 테이블과 드랍 개수를 런타임에 지정 가능
    /// </summary>
    public void Initialize(TrophyLootTable table, int count = 1)
    {
        lootTable = table;
        dropCount = count;
    }

    #endregion

    #region 상자 열기

    /// <summary>
    /// 상자를 열어 전리품을 TrophyInventory에 추가
    /// </summary>
    private void OpenChest()
    {
        if (lootTable == null)
        {
            Debug.LogWarning("[TrophyRewardChest] 드랍 테이블이 없습니다!");
            return;
        }

        isOpened = true;
        ShowPrompt(false);

        // 전리품 뽑기
        List<TrophyItemData> rewards = lootTable.GetRandomTrophies(dropCount);

        if (rewards.Count == 0)
        {
            Debug.LogWarning("[TrophyRewardChest] 전리품이 뽑히지 않았습니다!");
            return;
        }

        // TrophyInventory에 추가
        List<TrophyItemData> acquired = new List<TrophyItemData>();

        foreach (var trophy in rewards)
        {
            if (TrophyInventory.Instance != null)
            {
                bool success = TrophyInventory.Instance.AddItem(trophy);
                if (success)
                {
                    acquired.Add(trophy);
                }
                else
                {
                    Debug.Log($"[TrophyRewardChest] 인벤토리 가득 참 — '{trophy.itemName}' 획득 실패");
                }
            }
        }

        // 획득 연출
        StartCoroutine(ShowResultRoutine(acquired));

        // Glow 끄기
        ApplyGlow(0f);

        Debug.Log($"[TrophyRewardChest] 상자 오픈 — {acquired.Count}개 전리품 획득");
    }

    #endregion

    #region 연출

    /// <summary>
    /// 획득한 전리품 결과를 순서대로 표시
    /// </summary>
    private IEnumerator ShowResultRoutine(List<TrophyItemData> acquired)
    {
        if (resultUI == null || acquired.Count == 0) yield break;

        foreach (var trophy in acquired)
        {
            // 결과 UI 표시
            if (resultNameText != null)
                resultNameText.text = $"{trophy.itemName} ({trophy.grade})";

            if (resultIconRenderer != null && trophy.icon != null)
                resultIconRenderer.sprite = trophy.icon;

            resultUI.SetActive(true);

            // 팝업 애니메이션 (DOTween)
            resultUI.transform.localScale = Vector3.zero;
            resultUI.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

            yield return new WaitForSeconds(resultDisplayDuration);

            // 페이드 아웃
            resultUI.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack);
            yield return new WaitForSeconds(0.2f);

            resultUI.SetActive(false);
        }
    }

    /// <summary>
    /// 상호작용 프롬프트 표시/숨기기
    /// </summary>
    private void ShowPrompt(bool show)
    {
        if (interactionPromptUI != null)
        {
            if (show && nameTextUI != null)
                nameTextUI.text = chestName;

            interactionPromptUI.SetActive(show);
        }
    }

    /// <summary>
    /// Glow 발광 효과 적용
    /// </summary>
    private void ApplyGlow(float intensity)
    {
        foreach (var rend in renderers)
        {
            if (rend == null) continue;

            rend.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorID, glowColor * intensity);
            rend.SetPropertyBlock(propertyBlock);
        }
    }

    #endregion
}