using UnityEngine;
using TMPro;

/// <summary>
/// 층 이동 포탈 오브젝트
/// - 층 클리어 후 PortalSpawner에 의해 생성됨
/// - FloorChoice 데이터를 기반으로 색상 표시
/// - 플레이어 접근 시 World Space Canvas로 상호작용 안내
/// - F키 입력 시 다음 층으로 이동
/// </summary>
public class FloorPortal : MonoBehaviour
{
    [Header("World Space UI")]
    [SerializeField, Tooltip("Canvas 루트 (활성화/비활성화용)")]
    private GameObject canvasRoot;

    [SerializeField, Tooltip("상단 텍스트 (예: '2층 - 전투')")]
    private TextMeshProUGUI nameText;

    [SerializeField, Tooltip("하단 텍스트 (예: '[F] : 이동')")]
    private TextMeshProUGUI interactText;

    [Header("포탈 이펙트")]
    [SerializeField, Tooltip("포탈 파티클 시스템 (색상 변경용)")]
    private ParticleSystem portalParticle;

    [SerializeField, Tooltip("포탈 빛 (색상 변경용)")]
    private Light portalLight;

    [SerializeField, Tooltip("진입 시 재생할 이펙트 프리팹")]
    private GameObject enterEffectPrefab;

    [Header("설정")]
    [SerializeField, Tooltip("진입 키")]
    private KeyCode enterKey = KeyCode.F;

    // === 런타임 데이터 ===
    private FloorChoice choiceData;
    private bool isPlayerInRange = false;
    private bool isActivated = false;
    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;

    // 셰이더 프로퍼티 ID 캐싱
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();

        // 시작 시 Canvas 숨김
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    private void Update()
    {
        if (isPlayerInRange && !isActivated)
        {
            if (Input.GetKeyDown(enterKey))
            {
                EnterPortal();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isActivated) return;
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = true;

        // Canvas 표시
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

    // ============================================================
    // 초기화 — PortalSpawner에서 호출
    // ============================================================

    /// <summary>
    /// 포탈 초기화 — FloorChoice 데이터를 받아 색상/텍스트 설정
    /// </summary>
    public void Initialize(FloorChoice choice)
    {
        choiceData = choice;
        isActivated = false;

        // 색상 적용
        ApplyPortalColor(choice.portalColor);

        // 텍스트 설정
        int nextFloor = (StageManager.Instance?.GetCurrentFloor() ?? 0) + 1;

        if (nameText != null)
        {
            nameText.text = $"{nextFloor}층 - {choice.displayName}";
            nameText.color = Color.white;
        }

        if (interactText != null)
        {
            interactText.text = "[F] : 이동";
        }

        // Canvas 숨김 상태로 시작
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    // ============================================================
    // 포탈 진입 처리
    // ============================================================

    /// <summary>
    /// 포탈 진입 — StageManager에 선택 결과 전달
    /// </summary>
    private void EnterPortal()
    {
        if (isActivated) return;
        isActivated = true;

        // 진입 이펙트
        if (enterEffectPrefab != null)
        {
            GameObject effect = Instantiate(enterEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 3f);
        }

        // Canvas 숨김
        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        Debug.Log($"[FloorPortal] 포탈 진입: {choiceData.displayName} ({choiceData.stageType})");

        // StageManager에 선택 결과 전달
        StageManager.Instance?.OnPortalEntered(choiceData);
    }

    // ============================================================
    // 시각 효과
    // ============================================================

    /// <summary>
    /// 포탈 색상 적용 — 렌더러 Emission + 파티클 + 라이트
    /// </summary>
    private void ApplyPortalColor(Color color)
    {
        foreach (var rend in renderers)
        {
            if (rend == null) continue;

            rend.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorID, color * 2f);
            rend.SetPropertyBlock(propertyBlock);
        }

        if (portalParticle != null)
        {
            var main = portalParticle.main;
            main.startColor = color;
        }

        if (portalLight != null)
        {
            portalLight.color = color;
        }
    }

    /// <summary>
    /// 이 포탈이 담고 있는 선택지 데이터 반환
    /// </summary>
    public FloorChoice GetChoiceData() => choiceData;
}
