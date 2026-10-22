using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// 페이즈 전환 포탈 — 웨이브 클리어 후 출현
/// 
/// 동작:
///   1. 페이즈 클리어 시 StageManager가 보상 오브젝트들과 함께 스폰
///   2. 플레이어 접근 시 World Space Canvas로 상호작용 안내 표시
///   3. F키 입력 시 StageManager에 선택 완료 알림 → 다음 페이즈 진행
/// 
/// 특징:
///   - 포탈을 타면 남아있던 보상 오브젝트들은 StageManager가 일괄 정리(OnDismissed)
///   - 무제한 사용 가능한 오브젝트(스킬 강화 등)도 포탈 타기 전까지는 계속 사용 가능
///   - 플레이어가 "다음으로 진행"을 직접 결정하는 명시적 인터랙션
/// </summary>
public class PhaseTransitionPortal : MonoBehaviour
{
    [Header("World Space UI")]
    [SerializeField, Tooltip("Canvas 루트 (활성화/비활성화용)")]
    private GameObject canvasRoot;

    [SerializeField, Tooltip("상단 제목 텍스트 (예: '다음 웨이브')")]
    private TextMeshProUGUI titleText;

    [SerializeField, Tooltip("하단 안내 텍스트 (예: '[F] : 다음 웨이브')")]
    private TextMeshProUGUI interactText;

    [Header("표시 문구")]
    [SerializeField] private string titleLabel = "다음 웨이브";
    [SerializeField] private string interactLabel = "[F] : 진입";

    [Header("이펙트")]
    [SerializeField, Tooltip("진입 시 재생할 VFX 프리팹")]
    private GameObject enterEffectPrefab;

    [SerializeField, Tooltip("출현 시 솟아오르는 연출 시간")]
    private float appearDuration = 0.5f;

    [SerializeField, Tooltip("출현 시 솟아오르는 거리")]
    private float appearRiseHeight = 2f;

    [Header("입력 설정")]
    [SerializeField] private KeyCode enterKey = KeyCode.F;

    // ===== 런타임 상태 =====
    private bool isPlayerInRange = false;
    private bool isActivated = false;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    #region Unity Lifecycle

    private void Awake()
    {
        // 시작 시 Canvas 숨김
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    private void Start()
    {
        // 스폰 시 솟아오르는 연출
        PlayAppearAnimation();

        // 안내 텍스트 세팅
        if (titleText != null)
            titleText.text = titleLabel;

        if (interactText != null)
            interactText.text = interactLabel;
    }

    private void Update()
    {
        if (isActivated) return;
        if (!isPlayerInRange) return;

        if (Input.GetKeyDown(enterKey))
        {
            EnterPortal();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isActivated) return;
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = true;
        ShowPrompt(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = false;
        ShowPrompt(false);
    }

    private void OnDestroy()
    {
        // DOTween 정리
        transform.DOKill();
    }

    #endregion

    // ============================================================
    // 출현 연출
    // ============================================================

    #region Appear Animation

    /// <summary>
    /// 포탈 출현 연출 — 바닥에서 솟아오르는 효과
    /// </summary>
    private void PlayAppearAnimation()
    {
        Vector3 finalPos = transform.position;
        Vector3 startPos = finalPos - new Vector3(0f, appearRiseHeight, 0f);

        transform.position = startPos;
        transform.DOMoveY(finalPos.y, appearDuration).SetEase(Ease.OutBack);
    }

    #endregion

    // ============================================================
    // 포탈 진입 처리
    // ============================================================

    #region Portal Interaction

    /// <summary>
    /// 포탈 진입 — StageManager에 페이즈 전환 요청
    /// </summary>
    private void EnterPortal()
    {
        if (isActivated) return;
        isActivated = true;

        ShowPrompt(false);

        // 진입 이펙트
        if (enterEffectPrefab != null)
        {
            GameObject effect = Instantiate(enterEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 3f);
        }

        Debug.Log("[PhaseTransitionPortal] 포탈 진입 → 다음 페이즈로 진행");

        // StageManager에 알림 → 보상 오브젝트 정리 + 다음 페이즈 진행
        if (StageManager.Instance != null)
        {
            StageManager.Instance.NotifyRewardSelected();
        }
        else
        {
            Debug.LogWarning("[PhaseTransitionPortal] StageManager.Instance가 없습니다!");
        }

        // 포탈 자체도 제거 (다음 페이즈에 다시 스폰됨)
        Destroy(gameObject, 0.3f);
    }

    /// <summary>
    /// 상호작용 안내 Canvas 표시/숨김
    /// </summary>
    private void ShowPrompt(bool show)
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(show);
    }

    #endregion
}
