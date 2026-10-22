using UnityEngine;
using TMPro;

/// <summary>
/// 상인 NPC
/// - 플레이어 접근 시 World Space UI 표시
/// - F키 → 상점 상품 생성 + 상점 UI 열기
/// - 상점이 열려있으면 F키 무시
/// </summary>
public class ShopNPC : MonoBehaviour
{
    [Header("World Space UI")]
    [SerializeField, Tooltip("Canvas 루트")]
    private GameObject canvasRoot;

    [SerializeField, Tooltip("NPC 이름 텍스트")]
    private TextMeshProUGUI nameText;

    [SerializeField, Tooltip("안내 텍스트")]
    private TextMeshProUGUI interactText;

    [Header("설정")]
    [SerializeField] private string npcName = "상인";
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    // === 런타임 ===
    private bool isPlayerInRange = false;
    private bool hasGeneratedItems = false;

    private void Awake()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    private void Start()
    {
        if (nameText != null)
            nameText.text = npcName;

        if (interactText != null)
            interactText.text = "[F] : 상점";
    }

    private void Update()
    {
        if (!isPlayerInRange) return;

        // 상점이 이미 열려있으면 무시
        if (ShopUI.Instance != null && ShopUI.Instance.IsOpen()) return;

        if (Input.GetKeyDown(interactKey))
        {
            OpenShop();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = true;

        if (canvasRoot != null)
            canvasRoot.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = false;

        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        // 상점 열려있으면 닫기
        if (ShopUI.Instance != null && ShopUI.Instance.IsOpen())
            ShopUI.Instance.CloseShop();
    }

    /// <summary>
    /// 상점 열기 — 최초 1회만 상품 생성, 이후엔 UI만 열기
    /// </summary>
    private void OpenShop()
    {
        if (ShopManager.Instance == null || ShopUI.Instance == null)
        {
            Debug.LogWarning("[ShopNPC] ShopManager 또는 ShopUI가 없습니다.");
            return;
        }

        // 최초 1회만 상품 생성
        if (!hasGeneratedItems)
        {
            int floor = 1;
            if (StageManager.Instance != null)
                floor = StageManager.Instance.CurrentFloor;

            ShopManager.Instance.GenerateShopItems(floor);
            hasGeneratedItems = true;
        }

        // 상점 UI 열기
        ShopUI.Instance.OpenShop();

        // 안내 텍스트 숨김
        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        Debug.Log("[ShopNPC] 상점 열림");
    }
}
