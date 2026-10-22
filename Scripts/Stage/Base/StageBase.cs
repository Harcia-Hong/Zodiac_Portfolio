using System.Collections;
using UnityEngine;

/// <summary>
/// 모든 Stage 컨트롤러의 추상 베이스 클래스
/// - 공통: 인트로 UI / Player 스폰 / 게이지 / 미션 시작 / 클리어 / 포탈 스폰 또는 자동 씬 전환
/// - 자식: ObeliskStage / BossStage 등
///
/// 흐름:
///   씬 진입 → Start() → MovePlayerToSpawnPoint() → OnStageStart() (자식)
///   → ShowStageIntro() → 인트로 종료 콜백 → autoStartAfterIntro=true면 StartMission() 자동
///   → 자식이 클리어 조건 감지 → OnStageCleared() → clearDelay 대기
///   → portalPrefab 있으면 SpawnStagePortal(), 없으면 AutoTransitionToNextScene()
/// </summary>
public abstract class StageBase : MonoBehaviour
{
    // ============================================================
    // Inspector 설정
    // ============================================================

    [Header("Stage 정보")]
    [SerializeField, Tooltip("Stage 식별자 (인트로 UI에 크게 표시. 예: '1-1', '1-2')")]
    protected string stageId = "1-1";

    [SerializeField, Tooltip("미션 설명 (인트로 UI 부제목. 예: '모든 적을 쓰러뜨려라!')")]
    protected string missionDescription = "모든 적을 쓰러뜨려라!";

    [SerializeField, Tooltip("ON: 인트로 종료 후 자동으로 StartMission() 호출")]
    protected bool autoStartAfterIntro = true;

    [Header("Player 스폰 위치")]
    [SerializeField, Tooltip("Stage 진입 시 Player가 이동할 위치 (비워두면 현재 위치 유지)")]
    protected Transform playerSpawnPoint;

    [Header("다음 Stage 설정")]
    [SerializeField, Tooltip("다음 씬 이름 (예: 'Stage1-2'). 비워두면 MainMenu로 복귀")]
    protected string nextSceneName;

    [SerializeField, Tooltip("페이드 컷씬에서 표시할 메시지 (예: 'ADVANCING...')")]
    protected string transitionMessage = "ADVANCING...";

    [Header("포탈 스폰 (선택)")]
    [SerializeField, Tooltip("클리어 시 스폰할 포탈 프리팹 (없으면 자동 씬 전환)")]
    protected GameObject portalPrefab;

    [SerializeField, Tooltip("포탈 스폰 위치 (없으면 이 Stage GameObject 위치)")]
    protected Transform portalSpawnPoint;

    [Header("클리어 보상 (선택)")]
    [SerializeField, Tooltip("클리어 시 스폰할 보상 오브젝트 프리팹 (스킬 아이템 등). 없으면 스폰 안 함")]
    protected GameObject rewardObjectPrefab;

    [SerializeField, Tooltip("보상 오브젝트 스폰 위치 (없으면 이 Stage GameObject 위치)")]
    protected Transform rewardSpawnPoint;

    [Header("클리어 연출")]
    [SerializeField, Tooltip("클리어 후 포탈 스폰까지 대기 시간 (초)")]
    protected float clearDelay = 2f;

    // ============================================================
    // 런타임 상태 (디버깅용)
    // ============================================================

    [Header("현재 상태 (디버깅용 - 직접 수정 금지)")]
    [SerializeField, Tooltip("미션 진행도 0~100%")]
    protected float currentProgress = 0f;

    [SerializeField, Tooltip("미션이 시작되었는지")]
    protected bool isMissionStarted = false;

    [SerializeField, Tooltip("스테이지가 클리어되었는지")]
    protected bool isCleared = false;

    // ============================================================
    // 외부 접근용 프로퍼티
    // ============================================================

    public float CurrentProgress => currentProgress;
    public bool IsMissionStarted => isMissionStarted;
    public bool IsCleared => isCleared;

    // ============================================================
    // Unity 생명주기
    // ============================================================

    /// <summary>
    /// Stage 씬 진입 시 자동 호출
    /// 1) Player를 SpawnPoint로 이동
    /// 2) 자식의 OnStageStart() 호출 (이벤트 구독, 캐싱 등)
    /// 3) 인트로 UI 표시 → 종료 후 StartMission() 자동 호출 (autoStartAfterIntro=true 시)
    /// </summary>
    protected virtual void Start()
    {
        MovePlayerToSpawnPoint();
        OnStageStart();
        ShowStageIntro();
    }

    // ============================================================
    // 인트로 UI
    // ============================================================

    /// <summary>
    /// Stage 인트로 UI 표시 (DDOL된 StageIntroUI 싱글톤 사용)
    /// 인트로 종료 시 OnIntroComplete 콜백 호출됨
    /// </summary>
    private void ShowStageIntro()
    {
        if (StageIntroUI.Instance == null)
        {
            Debug.LogWarning("[StageBase] StageIntroUI.Instance가 없습니다. 인트로 없이 미션 시작.");
            if (autoStartAfterIntro) StartMission();
            return;
        }

        StageIntroUI.Instance.Show(stageId, missionDescription, OnIntroComplete);
    }

    /// <summary>
    /// 인트로 UI 종료 시 콜백 — autoStartAfterIntro=true면 미션 자동 시작
    /// </summary>
    private void OnIntroComplete()
    {
        if (autoStartAfterIntro)
        {
            StartMission();
        }
    }

    // ============================================================
    // Player 스폰
    // ============================================================

    /// <summary>
    /// Player를 playerSpawnPoint 위치로 순간이동
    /// CharacterController / NavMeshAgent / Rigidbody가 있는 경우 안전 처리
    /// SpawnPoint가 비어있으면 아무것도 하지 않음 (현재 위치 유지)
    /// </summary>
    protected virtual void MovePlayerToSpawnPoint()
    {
        if (playerSpawnPoint == null) return;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogWarning("[StageBase] 'Player' 태그 GameObject를 찾을 수 없습니다.");
            return;
        }

        // CharacterController 임시 비활성화 (강제 위치 변경 차단 방지)
        var charController = playerObj.GetComponent<CharacterController>();
        bool wasCharControllerEnabled = false;
        if (charController != null)
        {
            wasCharControllerEnabled = charController.enabled;
            charController.enabled = false;
        }

        playerObj.transform.position = playerSpawnPoint.position;
        playerObj.transform.rotation = playerSpawnPoint.rotation;

        // NavMeshAgent가 있다면 Warp (NavMesh 위치 동기화)
        var navAgent = playerObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.Warp(playerSpawnPoint.position);
        }

        // Rigidbody 속도 초기화 (이전 씬에서의 관성 제거)
        var rb = playerObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (charController != null)
        {
            charController.enabled = wasCharControllerEnabled;
        }

        Debug.Log($"[StageBase] Player를 SpawnPoint({playerSpawnPoint.position})로 이동");
    }

    // ============================================================
    // 자식이 override할 가상 메서드
    // ============================================================

    /// <summary>Stage 진입 시 초기화 (자식이 이벤트 구독, 캐싱 등 수행)</summary>
    protected virtual void OnStageStart() { }

    /// <summary>
    /// 미션 시작 — 인트로 종료 콜백에서 자동 호출 (또는 외부에서 호출)
    /// 자식이 override해서 잡몹 스폰 시작 등 추가 작업 수행
    /// </summary>
    public virtual void StartMission()
    {
        if (isMissionStarted)
        {
            Debug.LogWarning("[StageBase] 이미 미션이 시작되었습니다.");
            return;
        }

        isMissionStarted = true;
        currentProgress = 0f;

        Debug.Log($"[{GetType().Name}] 미션 시작!");
    }

    /// <summary>
    /// Stage 클리어 처리 — 자식이 명시적으로 호출 또는 게이지 100% 도달 시 자동 호출
    /// clearDelay 후 포탈 스폰 또는 자동 씬 전환
    /// </summary>
    protected virtual void OnStageCleared()
    {
        if (isCleared) return;
        isCleared = true;

        Debug.Log($"[{GetType().Name}] Stage 클리어!");
        StartCoroutine(NextStageRoutine());
    }

    // ============================================================
    // 진행도(게이지) 처리 (자식이 호출 — 게이지 시스템 미사용 시 무시)
    // ============================================================

    protected void AddProgress(float amount)
    {
        if (!isMissionStarted || isCleared) return;
        currentProgress = Mathf.Min(currentProgress + amount, 100f);
        Debug.Log($"[{GetType().Name}] 진행도: {currentProgress:F1}%");
        if (currentProgress >= 100f) OnStageCleared();
    }

    protected void RemoveProgress(float amount)
    {
        if (!isMissionStarted || isCleared) return;
        currentProgress = Mathf.Max(currentProgress - amount, 0f);
    }

    protected void SetProgress(float value)
    {
        if (!isMissionStarted || isCleared) return;
        currentProgress = Mathf.Clamp(value, 0f, 100f);
        if (currentProgress >= 100f) OnStageCleared();
    }

    // ============================================================
    // 다음 Stage 전환 (포탈 스폰 또는 자동 씬 전환)
    // ============================================================

    /// <summary>
    /// 클리어 후 처리 (clearDelay 대기 후 분기)
    /// - 보상 오브젝트 스폰 (rewardObjectPrefab 있을 때)
    /// - portalPrefab 있으면 포탈 스폰 / 없으면 자동 씬 전환
    /// </summary>
    private IEnumerator NextStageRoutine()
    {
        yield return new WaitForSeconds(clearDelay);

        // 1. 보상 오브젝트 스폰 (있을 때만)
        SpawnRewardObject();

        // 2. 포탈 또는 자동 전환
        if (portalPrefab != null)
        {
            SpawnStagePortal();
        }
        else
        {
            AutoTransitionToNextScene();
        }
    }

    /// <summary>
    /// 클리어 보상 오브젝트 스폰 (InteractableRewardObject 등)
    /// 스폰된 오브젝트가 자체적으로 상호작용 + 스킬 선택 UI 처리
    /// </summary>
    private void SpawnRewardObject()
    {
        if (rewardObjectPrefab == null) return;

        Vector3 pos = (rewardSpawnPoint != null) ? rewardSpawnPoint.position : transform.position;
        Quaternion rot = (rewardSpawnPoint != null) ? rewardSpawnPoint.rotation : Quaternion.identity;

        Instantiate(rewardObjectPrefab, pos, rot);

        Debug.Log($"[StageBase] 클리어 보상 스폰: {rewardObjectPrefab.name} @ {pos}");
    }

    /// <summary>
    /// 포탈 프리팹 스폰 + 동적 씬 정보 주입
    /// StagePortal 컴포넌트의 SetTargetScene으로 nextSceneName/transitionMessage 전달
    /// </summary>
    private void SpawnStagePortal()
    {
        Vector3 pos = (portalSpawnPoint != null) ? portalSpawnPoint.position : transform.position;
        Quaternion rot = (portalSpawnPoint != null) ? portalSpawnPoint.rotation : Quaternion.identity;

        GameObject portalObj = Instantiate(portalPrefab, pos, rot);

        StagePortal portal = portalObj.GetComponent<StagePortal>();
        if (portal != null)
        {
            portal.SetTargetScene(nextSceneName, transitionMessage);
        }
        else
        {
            Debug.LogWarning("[StageBase] 포탈 프리팹에 StagePortal 컴포넌트가 없습니다.");
        }

        Debug.Log($"[StageBase] 포탈 스폰 완료 → 다음 씬: {nextSceneName}");
    }

    /// <summary>
    /// 포탈 없이 즉시 자동 씬 전환 (fallback)
    /// nextSceneName 비어있으면 MainMenu로
    /// </summary>
    private void AutoTransitionToNextScene()
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("[StageBase] 다음 씬 이름이 설정되지 않음. MainMenu로 복귀.");
            SceneLoader.LoadStageTransition("MainMenu", "MISSION COMPLETE");
            return;
        }

        SceneLoader.LoadStageTransition(nextSceneName, transitionMessage);
    }
}
