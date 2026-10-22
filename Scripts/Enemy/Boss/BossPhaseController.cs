using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 페이즈 관리 컨트롤러
/// - 매 프레임 체력 비율을 감시하여 페이즈 전환 감지
/// - 전환 시 무적 + 강력한 기술 연출 + 배율 갱신
/// - 천벌 패턴(BossRainAttack) 연동 지원
/// - 중복 전환 방지를 위한 기록 관리
/// </summary>
public class BossPhaseController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private BossDataSO bossData;
    [SerializeField] private EnemyHealth health;
    [SerializeField] private BossPatternExecutor patternExecutor;
    [SerializeField] private BossRainAttack rainAttack;

    [Header("보스 비주얼 (사라짐/나타남 제어용)")]
    [SerializeField, Tooltip("보스 모델 루트 (사라질 때 비활성화)")]
    private GameObject bossVisualRoot;

    [Header("현재 상태 (디버깅용)")]
    [SerializeField] private int currentPhaseIndex = 0;
    [SerializeField] private bool isInvincible = false;
    [SerializeField] private bool isTransitioning = false;

    /// <summary>현재 페이즈 인덱스 (외부 읽기용)</summary>
    public int CurrentPhaseIndex => currentPhaseIndex;

    /// <summary>무적 상태 여부 (외부에서 데미지 차단에 사용)</summary>
    public bool IsInvincible => isInvincible;

    /// <summary>페이즈 전환 중 여부</summary>
    public bool IsTransitioning => isTransitioning;

    // 이미 전환 완료한 페이즈 인덱스 기록 (중복 전환 방지)
    private HashSet<int> completedTransitions = new HashSet<int>();

    // 페이즈 전환 이벤트 (BossStateMachine에서 구독)
    public event System.Action<int> OnPhaseTransitionStart;
    public event System.Action<int> OnPhaseTransitionEnd;

    // 전환 연출용 코루틴 참조
    private Coroutine transitionCoroutine;

    // 보스 Animator 캐싱
    private Animator bossAnimator;

    #region Unity Lifecycle

    private void Awake()
    {
        // 컴포넌트 자동 참조
        if (health == null)
            health = GetComponent<EnemyHealth>();

        if (patternExecutor == null)
            patternExecutor = GetComponent<BossPatternExecutor>();

        if (rainAttack == null)
            rainAttack = GetComponent<BossRainAttack>();

        bossAnimator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        // 첫 페이즈(0)는 전환 연출 없이 시작
        completedTransitions.Add(0);

        // 첫 페이즈 배율 적용
        ApplyPhaseMultipliers(0);
    }

    private void Update()
    {
        // 전환 중이면 감시 중단
        if (isTransitioning) return;

        // 체력 비율 계산 및 페이즈 전환 체크
        CheckPhaseTransition();
    }

    #endregion

    #region 페이즈 전환 감시

    /// <summary>
    /// 현재 체력 비율을 확인하고 페이즈 전환이 필요한지 체크
    /// </summary>
    private void CheckPhaseTransition()
    {
        if (bossData == null || health == null) return;
        if (health.isDead) return;

        float healthRatio = (float)health.curHealth / health.maxHealth;
        int targetPhaseIndex = bossData.GetPhaseIndex(healthRatio);

        if (targetPhaseIndex > currentPhaseIndex && !completedTransitions.Contains(targetPhaseIndex))
        {
            StartPhaseTransition(targetPhaseIndex);
        }
    }

    #endregion

    #region 페이즈 전환 실행

    /// <summary>
    /// 페이즈 전환 시작
    /// - 무적 ON → 현재 행동 중단 → 이벤트 발행 → 연출 실행
    /// </summary>
    private void StartPhaseTransition(int newPhaseIndex)
    {
        if (isTransitioning) return;

        Debug.Log($"[BossPhase] 페이즈 전환 시작: {currentPhaseIndex} → {newPhaseIndex}");

        isTransitioning = true;

        // 1. 무적 활성화
        SetInvincible(true);

        // 2. 현재 실행 중인 패턴 즉시 중단
        if (patternExecutor != null && patternExecutor.IsAttacking())
        {
            patternExecutor.StopAttack();
        }

        // 3. 페이즈 전환 이벤트 발행 (BossStateMachine이 상태 전환에 사용)
        OnPhaseTransitionStart?.Invoke(newPhaseIndex);

        // 4. 전환 연출 코루틴 시작
        transitionCoroutine = StartCoroutine(PhaseTransitionCoroutine(newPhaseIndex));
    }

    /// <summary>
    /// 페이즈 전환 연출 코루틴
    /// 
    /// 천벌 패턴 사용 시 흐름:
    ///   1. 전환 VFX (포효/에너지 방출)
    ///   2. 그로기 (잠시 멈춤)
    ///   3. 보스 사라짐 + Disappear VFX
    ///   4. 천벌 패턴 (BossRainAttack)
    ///   5. 보스 복귀 + Appear VFX
    ///   6. 전환 완료
    /// 
    /// 기존 방식 (천벌 미사용):
    ///   1. 전환 VFX
    ///   2. 전환 기술 패턴 or 무적 대기
    ///   3. 전환 완료
    /// </summary>
    private IEnumerator PhaseTransitionCoroutine(int newPhaseIndex)
    {
        BossPhaseData phaseData = bossData.GetPhaseData(newPhaseIndex);
        if (phaseData == null)
        {
            FinishPhaseTransition(newPhaseIndex);
            yield break;
        }

        // ====== 1. 전환 VFX 재생 (포효/에너지 방출) ======
        if (phaseData.transitionVFXPrefab != null)
        {
            GameObject vfx = Instantiate(
                phaseData.transitionVFXPrefab,
                transform.position,
                Quaternion.identity,
                transform
            );

            ParticleSystem ps = vfx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                float vfxDuration = ps.main.duration + ps.main.startLifetime.constantMax;
                Destroy(vfx, vfxDuration);
            }
            else
            {
                Destroy(vfx, phaseData.invincibleDuration);
            }
        }

        // ====== 천벌 패턴 분기 ======
        if (phaseData.useRainAttack && rainAttack != null)
        {
            yield return StartCoroutine(RainAttackTransition(phaseData));
        }
        else
        {
            yield return StartCoroutine(DefaultTransition(phaseData));
        }

        // ====== 전환 완료 ======
        FinishPhaseTransition(newPhaseIndex);
    }

    /// <summary>
    /// 기존 전환 방식 (전환 기술 패턴 or 무적 대기)
    /// </summary>
    private IEnumerator DefaultTransition(BossPhaseData phaseData)
    {
        if (phaseData.phaseTransitionPattern != null && patternExecutor != null)
        {
            patternExecutor.SetPhaseMultipliers(phaseData.damageMultiplier, phaseData.speedMultiplier);
            patternExecutor.ExecutePattern(phaseData.phaseTransitionPattern);

            yield return new WaitUntil(() => !patternExecutor.IsAttacking());
        }
        else
        {
            yield return new WaitForSeconds(phaseData.invincibleDuration);
        }
    }

    /// <summary>
    /// 천벌 패턴 전환 연출
    /// 그로기 → 사라짐 → 천벌 → 복귀
    /// </summary>
    private IEnumerator RainAttackTransition(BossPhaseData phaseData)
    {
        // --- 2. 그로기 (보스 잠시 멈춤) ---
        if (bossAnimator != null && !string.IsNullOrEmpty(phaseData.groggyAnimTrigger))
        {
            bossAnimator.SetTrigger(phaseData.groggyAnimTrigger);
        }

        Debug.Log($"[BossPhase] 그로기 시작 ({phaseData.groggyDuration}초)");
        yield return new WaitForSeconds(phaseData.groggyDuration);

        // --- 3. 보스 사라짐 ---
        if (phaseData.disappearVFXPrefab != null)
        {
            GameObject disappearVfx = Instantiate(
                phaseData.disappearVFXPrefab,
                transform.position,
                Quaternion.identity
            );
            Destroy(disappearVfx, 3f);
        }

        // 짧은 딜레이 후 비주얼 숨김 (VFX가 자연스럽게 보이도록)
        yield return new WaitForSeconds(0.3f);
        SetBossVisible(false);

        Debug.Log("[BossPhase] 보스 사라짐 → 천벌 패턴 시작");

        // --- 4. 천벌 패턴 실행 ---
        rainAttack.StartRainAttack();

        // 천벌 패턴 완료 대기
        yield return new WaitUntil(() => !rainAttack.IsRunning);

        Debug.Log("[BossPhase] 천벌 패턴 완료 → 보스 복귀");

        // --- 5. 보스 복귀 ---
        // 복귀 위치: 플레이어 근처 (너무 먼 곳에서 나타나지 않도록)
        RepositionBossNearPlayer();

        if (phaseData.appearVFXPrefab != null)
        {
            GameObject appearVfx = Instantiate(
                phaseData.appearVFXPrefab,
                transform.position,
                Quaternion.identity
            );
            Destroy(appearVfx, 3f);
        }

        SetBossVisible(true);

        // 복귀 후 짧은 경직 (바로 공격 안 하도록)
        yield return new WaitForSeconds(0.5f);
    }

    #endregion

    #region 보스 비주얼 제어

    /// <summary>
    /// 보스 모델 표시/숨김 (천벌 패턴 중 사라짐 연출)
    /// NavMeshAgent, Collider도 함께 제어하여 물리 충돌 방지
    /// </summary>
    private void SetBossVisible(bool visible)
    {
        // 모델 비주얼 토글
        if (bossVisualRoot != null)
        {
            bossVisualRoot.SetActive(visible);
        }
        else
        {
            // bossVisualRoot 미설정 시 Renderer 전체 토글
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
                r.enabled = visible;
        }

        // 콜라이더 토글 (안 보이는데 충돌하면 이상하니까)
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = visible;

        // NavMeshAgent 토글
        UnityEngine.AI.NavMeshAgent nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav != null)
            nav.enabled = visible;

        Debug.Log($"[BossPhase] 보스 비주얼: {(visible ? "표시" : "숨김")}");
    }

    /// <summary>
    /// 보스를 플레이어 근처 적절한 위치에 재배치
    /// 천벌 패턴 후 너무 먼 곳에서 나타나지 않도록
    /// </summary>
    private void RepositionBossNearPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        Vector3 playerPos = playerObj.transform.position;

        // 플레이어 앞 8m 거리에 복귀
        Vector3 dirFromPlayer = (transform.position - playerPos).normalized;
        if (dirFromPlayer == Vector3.zero)
            dirFromPlayer = Vector3.forward;

        Vector3 targetPos = playerPos + dirFromPlayer * 8f;
        targetPos.y = transform.position.y;

        // NavMesh 위에 배치
        UnityEngine.AI.NavMeshHit navHit;
        if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out navHit, 5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            transform.position = navHit.position;
        }
        else
        {
            transform.position = targetPos;
        }

        // 플레이어를 바라보도록 회전
        Vector3 lookDir = (playerPos - transform.position).normalized;
        lookDir.y = 0f;
        if (lookDir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(lookDir);

        Debug.Log($"[BossPhase] 보스 복귀 위치: {transform.position}");
    }

    #endregion

    #region 페이즈 전환 완료

    /// <summary>
    /// 페이즈 전환 완료 처리
    /// - 페이즈 번호 갱신 → 배율 적용 → 무적 해제 → 이벤트 발행
    /// </summary>
    private void FinishPhaseTransition(int newPhaseIndex)
    {
        int previousPhase = currentPhaseIndex;
        currentPhaseIndex = newPhaseIndex;
        completedTransitions.Add(newPhaseIndex);

        // 새 페이즈 배율 적용
        ApplyPhaseMultipliers(newPhaseIndex);

        // 무적 해제
        SetInvincible(false);
        isTransitioning = false;

        // 전환 완료 이벤트 발행
        OnPhaseTransitionEnd?.Invoke(newPhaseIndex);

        Debug.Log($"[BossPhase] 페이즈 전환 완료: {previousPhase} → {newPhaseIndex}");
    }

    #endregion

    #region 배율 및 무적 관리

    /// <summary>
    /// 특정 페이즈의 배율을 PatternExecutor에 적용
    /// </summary>
    private void ApplyPhaseMultipliers(int phaseIndex)
    {
        if (bossData == null || patternExecutor == null) return;

        BossPhaseData phaseData = bossData.GetPhaseData(phaseIndex);
        if (phaseData == null) return;

        patternExecutor.SetPhaseMultipliers(phaseData.damageMultiplier, phaseData.speedMultiplier);

        Debug.Log($"[BossPhase] 배율 적용 - 데미지: x{phaseData.damageMultiplier}, 속도: x{phaseData.speedMultiplier}, 이동: x{phaseData.moveSpeedMultiplier}");
    }

    /// <summary>
    /// 무적 상태 설정
    /// </summary>
    private void SetInvincible(bool invincible)
    {
        isInvincible = invincible;
        Debug.Log($"[BossPhase] 무적 상태: {(invincible ? "ON" : "OFF")}");
    }

    #endregion

    #region 외부 인터페이스

    /// <summary>
    /// 현재 페이즈의 데이터 반환
    /// </summary>
    public BossPhaseData GetCurrentPhaseData()
    {
        if (bossData == null) return null;
        return bossData.GetPhaseData(currentPhaseIndex);
    }

    /// <summary>
    /// 현재 페이즈에서 사용 가능한 패턴 목록 반환
    /// </summary>
    public BossAttackPatternSO[] GetCurrentPatterns()
    {
        BossPhaseData phaseData = GetCurrentPhaseData();
        if (phaseData == null) return null;
        return phaseData.attackPatterns;
    }

    /// <summary>
    /// 현재 페이즈의 이동 속도 배율 반환
    /// </summary>
    public float GetCurrentMoveSpeedMultiplier()
    {
        BossPhaseData phaseData = GetCurrentPhaseData();
        if (phaseData == null) return 1.0f;
        return phaseData.moveSpeedMultiplier;
    }

    /// <summary>
    /// 강제 페이즈 리셋 (테스트/디버깅용)
    /// </summary>
    [ContextMenu("페이즈 리셋")]
    public void ResetPhases()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        // 천벌 패턴 중이면 중단
        if (rainAttack != null && rainAttack.IsRunning)
            rainAttack.StopRainAttack();

        // 보스 비주얼 복구
        SetBossVisible(true);

        currentPhaseIndex = 0;
        isInvincible = false;
        isTransitioning = false;
        completedTransitions.Clear();
        completedTransitions.Add(0);

        ApplyPhaseMultipliers(0);

        Debug.Log("[BossPhase] 페이즈 리셋 완료");
    }

    #endregion

    #region 디버그

    [ContextMenu("현재 페이즈 확인")]
    public void LogCurrentPhase()
    {
        float healthRatio = health != null ? (float)health.curHealth / health.maxHealth : 1f;

        Debug.Log("=== 보스 페이즈 현황 ===");
        Debug.Log($"현재 페이즈: {currentPhaseIndex}");
        Debug.Log($"체력 비율: {healthRatio:P1}");
        Debug.Log($"무적 상태: {isInvincible}");
        Debug.Log($"전환 중: {isTransitioning}");
        Debug.Log($"완료된 전환: [{string.Join(", ", completedTransitions)}]");
    }

    #endregion
}