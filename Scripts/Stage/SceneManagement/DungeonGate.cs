using UnityEngine;
using TMPro;

/// <summary>
/// 차원문 — 거점에서 황도탑(인게임)으로 진입하는 오브젝트
/// - 플레이어 접근 시 World Space UI 표시
/// - F키 입력 시 로딩 거쳐서 인게임 씬으로 이동
/// </summary>
public class DungeonGate : MonoBehaviour
{
    [Header("World Space UI")]
    [SerializeField, Tooltip("Canvas 루트")]
    private GameObject canvasRoot;

    [SerializeField, Tooltip("상단 텍스트 (예: '황도탑')")]
    private TextMeshProUGUI nameText;

    [SerializeField, Tooltip("하단 텍스트 (예: '[F] : 진입')")]
    private TextMeshProUGUI interactText;

    [Header("설정")]
    [SerializeField, Tooltip("이동할 씬 이름")]
    private string targetScene = "Stage1-1";

    [SerializeField, Tooltip("진입 키")]
    private KeyCode enterKey = KeyCode.F;

    private bool isPlayerInRange = false;

    private void Awake()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    private void Update()
    {
        if (isPlayerInRange && Input.GetKeyDown(enterKey))
        {
            EnterDungeon();
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
    }

    /// <summary>
    /// 황도탑 진입 — 로딩 거쳐서 인게임 씬으로 이동
    /// </summary>
    private void EnterDungeon()
    {
        Debug.Log("[DungeonGate] 황도탑 진입!");

        // UI 숨김
        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        // 씬 전환
        SceneLoader.LoadScene(targetScene);
    }
}
