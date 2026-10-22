using UnityEngine;
using TMPro;

/// <summary>
/// "지식의 서고" 상호작용 오브젝트.
/// 플레이어가 접근하면 안내 UI 표시, F키로 강화 UI를 연다.
/// 베이스캠프(도서관)에 배치할 오브젝트에 붙인다.
/// </summary>
public class KnowledgeArchiveInteract : MonoBehaviour
{
    [Header("World Space UI")]
    [SerializeField, Tooltip("머리 위 Canvas 루트")]
    private GameObject canvasRoot;

    [SerializeField, Tooltip("오브젝트 이름 텍스트")]
    private TextMeshProUGUI nameText;

    [SerializeField, Tooltip("안내 텍스트 ([F] : 지식의 서고)")]
    private TextMeshProUGUI interactText;

    [Header("설정")]
    [SerializeField] private string displayName = "지식의 서고";
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    // --- 런타임 ---
    private bool isPlayerInRange = false;

    private void Awake()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    private void Update()
    {
        if (!isPlayerInRange) return;

        // 서고 UI가 이미 열려있으면 무시
        if (KnowledgeArchiveUI.Instance != null && KnowledgeArchiveUI.Instance.IsOpen())
            return;

        if (Input.GetKeyDown(interactKey))
        {
            OpenArchive();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = true;

        if (canvasRoot != null)
            canvasRoot.SetActive(true);

        ShowInteractUI();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = false;

        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        // 벗어나면 서고 UI도 닫기
        if (KnowledgeArchiveUI.Instance != null && KnowledgeArchiveUI.Instance.IsOpen())
            KnowledgeArchiveUI.Instance.Close();
    }

    /// <summary>
    /// 안내 UI 표시
    /// </summary>
    private void ShowInteractUI()
    {
        if (nameText != null)
            nameText.text = displayName;

        if (interactText != null)
            interactText.text = $"[F] : {displayName}";
    }

    /// <summary>
    /// 서고 UI 열기
    /// </summary>
    private void OpenArchive()
    {
        if (KnowledgeArchiveUI.Instance != null)
        {
            KnowledgeArchiveUI.Instance.Open();

            // 안내 UI 숨기기
            if (canvasRoot != null)
                canvasRoot.SetActive(false);

            Debug.Log("[지식의 서고] 상호작용으로 UI 열림");
        }
        else
        {
            Debug.LogWarning("[지식의 서고] KnowledgeArchiveUI.Instance가 없습니다.");
        }
    }
}
