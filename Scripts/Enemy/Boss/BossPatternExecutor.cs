using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;

/// <summary>
/// 보스 패턴 실행기 (타임라인 기반 병렬 실행 + 진행도 트리거 지원)
/// 
/// - BossAttackPatternSO를 받아서 각 스텝을 실행
/// - 시간 기반 스텝: startTime에 맞춰 발사 (기존 방식)
/// - 진행도 기반 스텝: 텔레그래프 Progress가 triggerProgress에 도달하면 발사
/// - 같은 조건의 스텝들은 동시에 실행 (개별 코루틴)
/// - 모든 스텝이 완료되면 패턴 종료
/// - IEnemyAttack 구현으로 기존 FSM과 호환
/// 
/// 실행 흐름:
///   ExecutePattern() 호출
///   → 메인 코루틴이 시간 기반 스텝 발사
///   → 진행도 감시 코루틴이 Telegraph.Progress 추적 → 진행도 기반 스텝 발사
///   → 모든 스텝 코루틴이 끝나면 패턴 완료
/// </summary>
public class BossPatternExecutor : MonoBehaviour, IEnemyAttack
{
    [Header("컴포넌트 참조")]
    [SerializeField] private AttackTelegraph telegraph;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private LayerMask playerLayer;

    // 내부 상태
    private bool isExecuting = false;
    private Coroutine mainCoroutine;
    private Coroutine progressWatcherCoroutine; // 진행도 감시 코루틴
    private BossAttackPatternSO currentPattern;
    private NavMeshAgent nav;
    private Animator anim;
    private BossPhaseController phaseController;   // 페이즈 인덱스 조회용

    // 페이즈 배율 (외부에서 설정)
    private float damageMultiplier = 1.0f;
    private float speedMultiplier = 1.0f;

    // 현재 활성 스텝 코루틴 추적 (전부 끝나야 패턴 완료)
    private List<Coroutine> activeStepCoroutines = new List<Coroutine>();
    private int runningStepCount = 0;

    // 텔레그래프 위치/방향 공유 (Telegraph 스텝 → Attack/Teleport 스텝 참조용)
    private Vector3 sharedTelegraphCenter;
    private Vector3 sharedTelegraphDirection;

    // 진행도 기반 스텝 발사 추적
    private bool[] launched;

    #region Unity Lifecycle

    private void Awake()
    {
        nav = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        phaseController = GetComponent<BossPhaseController>();

        if (telegraph == null)
            telegraph = GetComponent<AttackTelegraph>();

        // 플레이어 자동 탐색
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                playerTransform = playerObj.transform;
        }
    }

    private void OnDisable()
    {
        CleanupAll();
    }

    #endregion

    #region 외부 설정 메서드

    /// <summary>
    /// 페이즈 배율 설정 (BossPhaseController에서 호출)
    /// </summary>
    public void SetPhaseMultipliers(float damage, float speed)
    {
        damageMultiplier = damage;
        speedMultiplier = speed;
    }

    /// <summary>
    /// 패턴 실행 시작 (BossPatternSelectState에서 호출)
    /// </summary>
    public void ExecutePattern(BossAttackPatternSO pattern)
    {
        if (isExecuting || pattern == null) return;

        currentPattern = pattern;
        StartAttack();
    }

    #endregion

    #region IEnemyAttack 구현

    public bool IsAttacking() => isExecuting;

    public void StartAttack()
    {
        if (isExecuting || currentPattern == null) return;

        isExecuting = true;
        sharedTelegraphCenter = Vector3.zero;
        sharedTelegraphDirection = Vector3.zero;

        // NavMesh 비활성화 (패턴 실행 중 수동 제어)
        if (nav != null) nav.enabled = false;

        mainCoroutine = StartCoroutine(MainPatternCoroutine());
    }

    public void StopAttack()
    {
        if (!isExecuting) return;

        // 메인 코루틴 중단
        if (mainCoroutine != null)
        {
            StopCoroutine(mainCoroutine);
            mainCoroutine = null;
        }

        // 진행도 감시 코루틴 중단
        if (progressWatcherCoroutine != null)
        {
            StopCoroutine(progressWatcherCoroutine);
            progressWatcherCoroutine = null;
        }

        // 활성 스텝 코루틴 전부 중단
        foreach (var co in activeStepCoroutines)
        {
            if (co != null) StopCoroutine(co);
        }
        activeStepCoroutines.Clear();
        runningStepCount = 0;

        // 텔레그래프 취소
        if (telegraph != null)
            telegraph.Cancel();

        // DOTween 정리
        if (transform != null)
            transform.DOKill();

        // NavMesh 복구
        RestoreNavMesh();

        isExecuting = false;
        currentPattern = null;
    }

    public float GetAttackRange()
    {
        if (currentPattern != null && currentPattern.maxRange > 0f)
            return currentPattern.maxRange;

        return 15f;
    }

    public float GetCooldownTime()
    {
        if (currentPattern != null)
            return currentPattern.cooldown;

        return 2.0f;
    }

    #endregion

    #region 메인 패턴 코루틴

    /// <summary>
    /// 패턴 메인 코루틴
    /// - 시간 기반 스텝: 경과 시간 추적 → startTime 도달 시 발사
    /// - 진행도 기반 스텝: 별도 감시 코루틴에 위임
    /// - 모든 스텝이 완료되면 패턴 종료
    /// </summary>
    private IEnumerator MainPatternCoroutine()
    {
        // 커스텀 패턴 체크 — actionSteps가 비어있으면 전용 컴포넌트에 위임
        if (currentPattern.actionSteps == null || currentPattern.actionSteps.Length == 0)
        {
            yield return ExecuteCustomPattern();
            FinishPattern();
            yield break;
        }

        int stepCount = currentPattern.actionSteps.Length;
        Debug.Log($"[BossPattern] '{currentPattern.patternName}' 시작 ({stepCount}스텝)");

        // 스텝별 발사 여부 추적 (시간 + 진행도 공용)
        launched = new bool[stepCount];

        // 진행도 기반 스텝이 있으면 감시 코루틴 시작
        if (HasProgressBasedSteps())
        {
            progressWatcherCoroutine = StartCoroutine(ProgressWatcherCoroutine());
        }

        // 시간 기반 스텝 발사 루프
        float elapsedTime = 0f;
        bool allTimedLaunched = false;

        while (!allTimedLaunched)
        {
            allTimedLaunched = true;

            for (int i = 0; i < stepCount; i++)
            {
                if (launched[i]) continue;

                BossActionStep step = currentPattern.actionSteps[i];

                // 시간 기반 스텝만 여기서 처리
                if (!step.IsTimeBased) continue;

                allTimedLaunched = false;

                float adjustedStartTime = step.startTime / speedMultiplier;
                if (elapsedTime >= adjustedStartTime)
                {
                    launched[i] = true;
                    LaunchStep(step, i);
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 모든 스텝이 발사될 때까지 대기 (진행도 기반 포함)
        yield return new WaitUntil(() => AllStepsLaunched());

        // 모든 코루틴이 끝날 때까지 대기
        yield return new WaitUntil(() => runningStepCount <= 0);

        Debug.Log($"[BossPattern] '{currentPattern.patternName}' 완료");
        FinishPattern();
    }

    /// <summary>
    /// 진행도 기반 스텝 감시 코루틴
    /// - 텔레그래프가 활성화되어 있는 동안 매 프레임 Progress를 체크
    /// - triggerProgress에 도달한 스텝을 발사
    /// </summary>
    private IEnumerator ProgressWatcherCoroutine()
    {
        int stepCount = currentPattern.actionSteps.Length;

        // 텔레그래프가 시작될 때까지 대기
        yield return new WaitUntil(() => telegraph != null && telegraph.IsShowing);

        // 텔레그래프가 활성화된 동안 진행도 감시
        while (telegraph != null && telegraph.IsShowing)
        {
            float currentProgress = telegraph.Progress;

            for (int i = 0; i < stepCount; i++)
            {
                if (launched[i]) continue;

                BossActionStep step = currentPattern.actionSteps[i];

                // 진행도 기반 스텝만 여기서 처리
                if (!step.IsProgressBased) continue;

                if (currentProgress >= step.triggerProgress)
                {
                    launched[i] = true;
                    LaunchStep(step, i);
                    Debug.Log($"[BossPattern] 스텝 {i} ({step.actionType}) 진행도 트리거 @ {step.triggerProgress:P0}");
                }
            }

            yield return null;
        }

        // 텔레그래프가 끝난 후 아직 발사 안 된 진행도 스텝이 있으면 즉시 발사
        // (Progress 1.0 트리거인데 텔레그래프 완료 콜백과 타이밍이 엇갈린 경우 안전장치)
        for (int i = 0; i < stepCount; i++)
        {
            if (launched[i]) continue;

            BossActionStep step = currentPattern.actionSteps[i];
            if (!step.IsProgressBased) continue;

            launched[i] = true;
            LaunchStep(step, i);
            Debug.Log($"[BossPattern] 스텝 {i} ({step.actionType}) 텔레그래프 종료 후 강제 발사");
        }
    }

    /// <summary>
    /// 개별 스텝을 코루틴으로 발사
    /// </summary>
    private void LaunchStep(BossActionStep step, int index)
    {
        runningStepCount++;

        Coroutine co = StartCoroutine(ExecuteStepCoroutine(step, index));
        activeStepCoroutines.Add(co);
    }

    /// <summary>
    /// 개별 스텝 코루틴 래퍼 (완료 시 카운트 감소)
    /// </summary>
    private IEnumerator ExecuteStepCoroutine(BossActionStep step, int index)
    {
        // 애니메이션 재생 (offset 지원)
        PlayAnimation(step.animationTrigger, step.animationStartOffset);

        // 스텝 타입별 실행
        yield return ExecuteStep(step);

        // 완료 처리
        runningStepCount--;
        Debug.Log($"[BossPattern] 스텝 {index} ({step.actionType}) 완료");
    }

    #endregion

    #region 스텝별 실행 분기

    /// <summary>
    /// 스텝 타입에 따라 해당 로직으로 분기
    /// </summary>
    private IEnumerator ExecuteStep(BossActionStep step)
    {
        switch (step.actionType)
        {
            case BossActionType.Prepare:
                yield return ExecutePrepare(step);
                break;

            case BossActionType.Teleport:
                yield return ExecuteTeleport(step);
                break;

            case BossActionType.Move:
                yield return ExecuteMove(step);
                break;

            case BossActionType.Telegraph:
                yield return ExecuteTelegraph(step);
                break;

            case BossActionType.Attack:
                yield return ExecuteAttack(step);
                break;

            case BossActionType.Recovery:
                yield return ExecuteRecovery(step);
                break;
        }
    }

    #endregion

    #region 스텝 실행 로직

    /// <summary>
    /// 준비자세 - 애니메이션 재생 + 플레이어 방향 회전 + 대기
    /// </summary>
    private IEnumerator ExecutePrepare(BossActionStep step)
    {
        float adjustedDuration = step.duration / speedMultiplier;

        LookAtPlayer();

        yield return new WaitForSeconds(adjustedDuration);
    }

    /// <summary>
    /// 순간이동 - DOTween 스케일 연출 → 위치 이동 → 복구
    /// </summary>
    private IEnumerator ExecuteTeleport(BossActionStep step)
    {
        if (playerTransform == null) yield break;

        // 1. 목적지 계산
        Vector3 destination = CalculateTeleportDestination(step);

        // 2. 사라지는 VFX
        if (step.teleportDisappearVFX != null)
            Instantiate(step.teleportDisappearVFX, transform.position, Quaternion.identity);

        // 3. 사라지는 연출 (스케일 축소)
        Vector3 originalScale = transform.localScale;
        yield return transform.DOScale(Vector3.zero, 0.15f)
            .SetEase(Ease.InBack)
            .WaitForCompletion();

        // 4. 위치 이동
        transform.position = destination;
        LookAtPlayer();

        // 5. 나타나는 VFX
        if (step.teleportAppearVFX != null)
            Instantiate(step.teleportAppearVFX, destination, Quaternion.identity);

        // 6. 나타나는 연출 (스케일 복구)
        yield return transform.DOScale(originalScale, 0.15f)
            .SetEase(Ease.OutBack)
            .WaitForCompletion();

        // 7. 남은 duration 대기
        float remainDuration = Mathf.Max(0f, (step.duration / speedMultiplier) - 0.3f);
        if (remainDuration > 0f)
            yield return new WaitForSeconds(remainDuration);
    }

    /// <summary>
    /// 이동 - DOTween으로 목적지까지 이동 + VFX 지원
    /// - moveTowardPlayer: true → 플레이어 근처까지 이동 (moveDistance = 멈출 거리)
    /// - moveTowardPlayer: false → 현재 바라보는 방향으로 고정 거리 이동
    /// </summary>
    private IEnumerator ExecuteMove(BossActionStep step)
    {
        if (playerTransform == null) yield break;

        Vector3 destination;

        if (step.moveTowardPlayer)
        {
            // 플레이어 방향 계산
            Vector3 dirToPlayer = (playerTransform.position - transform.position);
            dirToPlayer.y = 0f;

            float distanceToPlayer = dirToPlayer.magnitude;

            // 플레이어로부터 moveDistance만큼 떨어진 위치에서 멈춤
            // 이미 충분히 가까우면 이동 안 함
            if (distanceToPlayer <= step.moveDistance)
            {
                LookAtPlayer();
                yield break;
            }

            float moveAmount = distanceToPlayer - step.moveDistance;
            Vector3 moveDir = dirToPlayer.normalized;

            // 이동 방향 회전
            transform.rotation = Quaternion.LookRotation(moveDir);

            destination = transform.position + moveDir * moveAmount;
        }
        else
        {
            // 기존 방식: 바라보는 방향으로 고정 거리 이동
            Vector3 moveDir = transform.forward;
            destination = transform.position + moveDir * step.moveDistance;

            if (moveDir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(moveDir);
        }

        // 이동 중 VFX 재생 (번개 이펙트 등)
        GameObject moveVFXInstance = null;
        if (step.moveVFXPrefab != null)
        {
            moveVFXInstance = Instantiate(step.moveVFXPrefab, transform.position, transform.rotation, transform);
        }

        // DOTween 이동
        float adjustedDuration = step.duration / speedMultiplier;

        yield return transform.DOMove(destination, adjustedDuration)
            .SetEase(Ease.InOutQuad)
            .WaitForCompletion();

        // 이동 VFX 정리
        if (moveVFXInstance != null)
            Destroy(moveVFXInstance);
    }

    /// <summary>
    /// 텔레그래프 - 공격 범위 표시 (AttackTelegraph 활용)
    /// - useFillTelegraph에 따라 Fill 모드 전환
    /// - 표시 완료까지 대기, 중심 위치와 방향을 공유 변수에 저장
    /// </summary>
    private IEnumerator ExecuteTelegraph(BossActionStep step)
    {
        // 텔레그래프 중심 위치 계산 + 공유 변수에 저장
        Vector3 center = CalculateTelegraphCenter(step);
        sharedTelegraphCenter = center;

        // 방향 계산 + 공유 변수에 저장 (Cone, Line용)
        Vector3 direction = Vector3.zero;
        if (playerTransform != null)
        {
            direction = (playerTransform.position - transform.position).normalized;
            direction.y = 0f;
        }
        sharedTelegraphDirection = direction;

        // 텔레그래프 컴포넌트 없으면 시간만 대기
        if (telegraph == null)
        {
            yield return new WaitForSeconds(step.duration / speedMultiplier);
            yield break;
        }

        // Fill 모드 설정 (패턴 스텝별로 제어)
        telegraph.SetFillMode(step.useFillTelegraph);

        // 완료 대기 플래그
        bool telegraphComplete = false;

        // 텔레그래프 VFX 재생 (번개 빌드업 등)
        GameObject telegraphVFXInstance = null;
        if (step.telegraphVFXPrefab != null)
        {
            telegraphVFXInstance = Instantiate(step.telegraphVFXPrefab, center, Quaternion.identity);

            // 공격 범위에 맞춰 스케일 (필요 시 조절)
            float vfxScale = step.telegraphRange * 0.5f;
            telegraphVFXInstance.transform.localScale = Vector3.one * vfxScale;
        }

        // 형태별 텔레그래프 표시
        switch (step.telegraphShape)
        {
            case BossTelegraphShape.Circle:
                telegraph.ShowCircle(center, step.telegraphRange, () => telegraphComplete = true);
                break;

            case BossTelegraphShape.Cone:
                telegraph.ShowCone(center, direction, step.telegraphRange, () => telegraphComplete = true);
                break;

            case BossTelegraphShape.Line:
                telegraph.ShowArrow(center, direction, step.telegraphRange, () => telegraphComplete = true);
                break;
        }

        // 텔레그래프 완료까지 대기
        while (!telegraphComplete)
        {
            yield return null;
        }
    }

    /// <summary>
    /// 공격 - 투사체 발사 또는 즉시 데미지 판정
    /// - attackProjectilePrefab이 있으면 → 투사체 생성 (검기 등 원거리)
    /// - 없으면 → 기존 OverlapSphere 즉시 판정 (근거리)
    /// </summary>
    private IEnumerator ExecuteAttack(BossActionStep step)
    {
        // 1. 판정 중심 결정 (텔레그래프 위치 or 보스 위치)
        Vector3 center = sharedTelegraphCenter != Vector3.zero
            ? sharedTelegraphCenter
            : transform.position;

        // 2. 데미지 딜레이 대기 (애니메이션 타격 시점까지)
        if (step.damageDelay > 0f)
        {
            yield return new WaitForSeconds(step.damageDelay / speedMultiplier);
        }

        // 3. 최종 데미지 계산
        int finalDamage = Mathf.RoundToInt(step.baseDamage * damageMultiplier);

        // 4. 투사체 vs 즉시 판정 분기
        if (step.attackProjectilePrefab != null)
        {
            // === 투사체 발사 (원거리 공격) ===
            Vector3 spawnPos = transform.position + Vector3.up * 1.0f; // 보스 몸통 높이에서 발사
            Vector3 direction = sharedTelegraphDirection != Vector3.zero
                ? sharedTelegraphDirection
                : transform.forward;

            GameObject projObj = Instantiate(step.attackProjectilePrefab, spawnPos, Quaternion.identity);
            BossProjectile projectile = projObj.GetComponent<BossProjectile>();

            if (projectile != null)
            {
                projectile.Initialize(direction, finalDamage, step.projectileSpeed, step.knockbackForce);
            }

            // 공격 VFX (발사 시 이펙트)
            if (step.attackVFXPrefab != null)
                Instantiate(step.attackVFXPrefab, spawnPos, transform.rotation);
        }
        else
        {
            // === 즉시 판정 (근거리 공격) ===
            if (step.attackVFXPrefab != null)
            {
                // 보스 위치에서 공격 방향으로 VFX 생성
                Vector3 vfxPos = transform.position;
                Quaternion vfxRot = Quaternion.LookRotation(
                    sharedTelegraphDirection != Vector3.zero ? sharedTelegraphDirection : transform.forward
                );

                GameObject vfx = Instantiate(step.attackVFXPrefab, vfxPos, vfxRot);

                // 공격 범위에 맞춰 스케일 (기본 크기 대비 조절)
                float vfxScale = step.attackRadius * step.attackVFXScale;
                vfx.transform.localScale = Vector3.one * vfxScale;
            }

            ApplyDamage(step, finalDamage, center);
            // 추가 원형 번개 (페이즈 조건부) — 슬램 후 중심 주변에 번개
            if (step.spawnCircularLightning && GetCurrentPhaseIndex() >= step.lightningMinPhase)
            {
                StartCoroutine(SpawnCircularLightning(center, step));
            }
        }

        // 5. 남은 지속 시간 대기
        float remainDuration = Mathf.Max(0f, (step.duration / speedMultiplier) - (step.damageDelay / speedMultiplier));
        yield return new WaitForSeconds(remainDuration);
    }

    /// <summary>
    /// 현재 페이즈 인덱스 반환 (0=1페이즈, 1=2페이즈, 2=3페이즈)
    /// phaseController 없으면 0
    /// </summary>
    private int GetCurrentPhaseIndex()
    {
        return phaseController != null ? phaseController.CurrentPhaseIndex : 0;
    }

    /// <summary>
    /// 중심 주변에 원형으로 번개 N개를 떨어뜨림 (페이즈 조건부 추가 공격)
    /// - 슬램 후 lightningDelay 대기 → 반경에 균등 배치된 위치마다 VFX + 데미지
    /// - 데미지는 페이즈 배율(damageMultiplier) 적용
    /// </summary>
    private IEnumerator SpawnCircularLightning(Vector3 center, BossActionStep step)
    {
        // 슬램 후 반응 시간
        yield return new WaitForSeconds(step.lightningDelay);

        int count = Mathf.Max(1, step.lightningStrikeCount);
        float angleStep = 360f / count;
        int finalDamage = Mathf.RoundToInt(step.lightningDamage * damageMultiplier);

        for (int i = 0; i < count; i++)
        {
            // 원형 균등 배치 위치 계산
            float rad = angleStep * i * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * step.lightningRadius;
            Vector3 strikePos = center + offset;

            // VFX 생성
            // VFX 생성 (판정 범위에 맞춰 크기 스케일)
            if (step.lightningVFXPrefab != null)
            {
                GameObject vfx = Instantiate(step.lightningVFXPrefab, strikePos, Quaternion.identity);

                // VFX 크기를 판정 범위(hitRadius)에 맞춰 조절
                float vfxScale = step.lightningHitRadius * step.lightningVFXScale;
                vfx.transform.localScale = Vector3.one * vfxScale;
            }

            // 데미지 판정 (각 번개 위치 원형 범위)
            Collider[] hits = Physics.OverlapSphere(strikePos, step.lightningHitRadius, playerLayer);
            foreach (var hit in hits)
            {
                PlayerHealth ph = hit.GetComponent<PlayerHealth>();
                if (ph != null)
                {
                    ph.TakeDamage(finalDamage);
                }
            }
        }

        Debug.Log($"[BossPattern] 원형 번개 {count}개 발동 (페이즈 {GetCurrentPhaseIndex()})");
    }

    /// <summary>
    /// 후딜 - 복귀 애니메이션 + 대기 (회전 옵션)
    /// </summary>
    private IEnumerator ExecuteRecovery(BossActionStep step)
    {
        float adjustedDuration = step.duration / speedMultiplier;
        float elapsed = 0f;

        while (elapsed < adjustedDuration)
        {
            if (step.canRotateDuringRecovery)
                LookAtPlayerSmooth();

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// actionSteps가 비어있는 패턴 → 전용 컴포넌트에 실행 위임
    /// 패턴 이름으로 어떤 컴포넌트를 호출할지 판단
    /// </summary>
    private IEnumerator ExecuteCustomPattern()
    {
        Debug.Log($"[BossPattern] 커스텀 패턴 실행: '{currentPattern.patternName}'");

        // BossRadialAttack 체크
        BossRadialAttack radialAttack = GetComponent<BossRadialAttack>();
        if (radialAttack != null && currentPattern.patternName.Contains("탄막"))
        {
            radialAttack.Execute();
            yield return new WaitUntil(() => !radialAttack.IsExecuting);
            yield break;
        }

        // BossGroundThorns 체크
        BossGroundThorns groundThorns = GetComponent<BossGroundThorns>();
        if (groundThorns != null && currentPattern.patternName.Contains("가시"))
        {
            groundThorns.Execute();
            yield return new WaitUntil(() => !groundThorns.IsExecuting);
            yield break;
        }

        Debug.LogWarning($"[BossPattern] '{currentPattern.patternName}' — 실행할 커스텀 컴포넌트를 찾을 수 없음!");
    }

    #endregion

    #region 데미지 판정

    /// <summary>
    /// 형태별 데미지 판정 처리
    /// - Circle: OverlapSphere (원형 범위)
    /// - Cone: OverlapSphere + 각도 체크 (부채꼴)
    /// - Line: BoxCast (직선 경로 전체 동시 판정)
    /// </summary>
    private void ApplyDamage(BossActionStep step, int damage, Vector3 center)
    {
        Collider[] hits;

        if (step.attackShape == BossTelegraphShape.Line)
        {
            // === 직선 판정 (경로 전체 동시 데미지) ===
            Vector3 direction = sharedTelegraphDirection != Vector3.zero
                ? sharedTelegraphDirection
                : transform.forward;
            direction.y = 0f;

            // 박스 중심 = 보스 위치에서 경로 절반 앞
            Vector3 boxCenter = center + direction.normalized * (step.attackRadius * 0.5f);

            // 박스 크기: 길이 = attackRadius, 폭 = attackAngle(유용하게 재활용), 높이 = 2
            Vector3 halfExtents = new Vector3(step.attackAngle * 0.5f, 1f, step.attackRadius * 0.5f);

            // 박스 회전 (방향에 맞춤)
            Quaternion boxRotation = Quaternion.LookRotation(direction);

            hits = Physics.OverlapBox(boxCenter, halfExtents, boxRotation, playerLayer);
        }
        else
        {
            // === 원형/부채꼴 판정 (기존 방식) ===
            hits = Physics.OverlapSphere(center, step.attackRadius, playerLayer);
        }

        foreach (var hit in hits)
        {
            // Cone 형태: 각도 체크
            if (step.attackShape == BossTelegraphShape.Cone)
            {
                Vector3 dirToTarget = (hit.transform.position - center).normalized;
                dirToTarget.y = 0f;

                Vector3 attackDir = sharedTelegraphDirection != Vector3.zero
                    ? sharedTelegraphDirection
                    : transform.forward;
                attackDir.y = 0f;

                float angle = Vector3.Angle(attackDir, dirToTarget);
                if (angle > step.attackAngle * 0.5f)
                    continue;
            }

            PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
                Debug.Log($"[BossPattern] 플레이어에게 {damage} 데미지");

                if (step.knockbackForce > 0f)
                    ApplyKnockback(hit, center, step.knockbackForce);
            }
        }
    }

    /// <summary>
    /// 넉백 적용
    /// </summary>
    private void ApplyKnockback(Collider target, Vector3 center, float force)
    {
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            Vector3 knockbackDir = (target.transform.position - center).normalized;
            knockbackDir.y = 0f;
            targetRb.AddForce(knockbackDir * force, ForceMode.Impulse);
        }
    }

    #endregion

    #region 유틸리티 - 위치 계산

    /// <summary>
    /// 순간이동 목적지 계산
    /// </summary>
    private Vector3 CalculateTeleportDestination(BossActionStep step)
    {
        Vector3 destination = transform.position;

        switch (step.teleportTarget)
        {
            case TeleportTarget.NearPlayer:
                Vector3 dirFromPlayer = (transform.position - playerTransform.position).normalized;
                destination = playerTransform.position + dirFromPlayer * step.teleportOffset;
                break;

            case TeleportTarget.OnPlayer:
                destination = playerTransform.position;
                break;

            case TeleportTarget.RandomAroundPlayer:
                Vector2 randomOffset = Random.insideUnitCircle.normalized * step.teleportOffset;
                destination = playerTransform.position + new Vector3(randomOffset.x, 0f, randomOffset.y);
                break;

            case TeleportTarget.OnTelegraph:
                if (sharedTelegraphCenter != Vector3.zero)
                    destination = sharedTelegraphCenter;
                else
                    destination = playerTransform.position;
                break;
        }

        destination.y = transform.position.y;
        return destination;
    }

    /// <summary>
    /// 텔레그래프 중심 위치 계산
    /// </summary>
    private Vector3 CalculateTelegraphCenter(BossActionStep step)
    {
        switch (step.telegraphOrigin)
        {
            case TelegraphOrigin.Boss:
                return transform.position;

            case TelegraphOrigin.Player:
                return playerTransform != null ? playerTransform.position : transform.position;

            case TelegraphOrigin.Custom:
                return transform.position;

            default:
                return transform.position;
        }
    }

    #endregion

    #region 유틸리티 - 회전/애니메이션/정리

    /// <summary>
    /// 플레이어 방향으로 즉시 회전
    /// </summary>
    private void LookAtPlayer()
    {
        if (playerTransform == null) return;

        Vector3 dir = (playerTransform.position - transform.position);
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    /// <summary>
    /// 플레이어 방향으로 부드럽게 회전
    /// </summary>
    private void LookAtPlayerSmooth()
    {
        if (playerTransform == null) return;

        Vector3 dir = (playerTransform.position - transform.position);
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 5f * Time.deltaTime);
        }
    }

    /// <summary>
    /// 애니메이션 재생
    /// - offset이 0이면 Trigger 방식
    /// - offset이 0보다 크면 Animator.Play로 중간부터 재생
    /// </summary>
    private void PlayAnimation(string triggerName, float startOffset = 0f)
    {
        if (anim == null || string.IsNullOrEmpty(triggerName)) return;

        if (startOffset > 0f)
        {
            anim.Play(triggerName, 0, startOffset);
        }
        else
        {
            anim.SetTrigger(triggerName);
        }
    }

    /// <summary>
    /// 패턴 완료 처리
    /// </summary>
    private void FinishPattern()
    {
        sharedTelegraphCenter = Vector3.zero;
        sharedTelegraphDirection = Vector3.zero;
        activeStepCoroutines.Clear();
        runningStepCount = 0;
        launched = null;

        // 진행도 감시 코루틴 정리
        if (progressWatcherCoroutine != null)
        {
            StopCoroutine(progressWatcherCoroutine);
            progressWatcherCoroutine = null;
        }

        RestoreNavMesh();

        isExecuting = false;
    }

    /// <summary>
    /// NavMesh 복구 (위치 동기화 포함)
    /// </summary>
    private void RestoreNavMesh()
    {
        if (nav != null && !nav.enabled)
        {
            nav.enabled = true;
            nav.Warp(transform.position);
        }
    }

    /// <summary>
    /// 모든 코루틴 + DOTween 정리
    /// </summary>
    private void CleanupAll()
    {
        if (mainCoroutine != null)
        {
            StopCoroutine(mainCoroutine);
            mainCoroutine = null;
        }

        if (progressWatcherCoroutine != null)
        {
            StopCoroutine(progressWatcherCoroutine);
            progressWatcherCoroutine = null;
        }

        foreach (var co in activeStepCoroutines)
        {
            if (co != null) StopCoroutine(co);
        }
        activeStepCoroutines.Clear();
        runningStepCount = 0;

        if (transform != null)
            transform.DOKill();
    }

    /// <summary>
    /// 진행도 기반 스텝이 존재하는지 확인
    /// </summary>
    private bool HasProgressBasedSteps()
    {
        if (currentPattern?.actionSteps == null) return false;

        foreach (var step in currentPattern.actionSteps)
        {
            if (step.IsProgressBased) return true;
        }
        return false;
    }

    /// <summary>
    /// 모든 스텝이 발사되었는지 확인
    /// </summary>
    private bool AllStepsLaunched()
    {
        if (launched == null) return true;

        foreach (bool l in launched)
        {
            if (!l) return false;
        }
        return true;
    }

    #endregion

    #region 디버그

    private void OnDrawGizmosSelected()
    {
        if (sharedTelegraphCenter != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(sharedTelegraphCenter, 1f);
        }
    }

    #endregion
}
