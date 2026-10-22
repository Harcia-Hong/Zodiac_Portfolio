// Enemy_Laser_Attack.cs — 직선 레이저 빔 공격 (Windblown Pot 스타일)

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 직선 레이저 빔 발사 공격 (엘리트급 적 전용)
///
/// [패턴 A: 기본 레이저 발사]
/// 1. 거리 체크 — 너무 가까우면 후퇴, 너무 멀면 FSM이 추적
/// 2. 적정 거리에서 멈춤 + 플레이어 조준
/// 3. Telegraph 표시 (직선, 0.8초) — 회피 기회 제공
/// 4. 펄스 빔 발사 (1방, 짧고 강함) — Raycast로 데미지 판정
/// 5. 후딜레이
///
/// [거리 시스템 — 3단계]
/// - 0 ~ minRange: 너무 가까움 → NavMesh로 후퇴 이동 (공격 안 함)
/// - minRange ~ maxRange: 적정 거리 → 공격 가능
/// - maxRange 이상: 너무 멀음 → FSM의 ChasingState가 추적
///
/// [참고 게임]
/// - Windblown - Pot (Rat Village 바이옴)
///
/// [향후 확장]
/// - Phase 2: 폭탄 설치 패턴 추가 예정 (확률적 발동)
///
/// [의존성]
/// - EnemyAttackBase (player/nav/anim 자동 캐싱)
/// - AttackTelegraph (Arrow 타입 사용)
/// - 레이저 빔 prefab (Super Magic FX 등의 파티클 시스템)
/// </summary>
public class Enemy_Laser_Attack : EnemyAttackBase
{
    // =========================================================
    // 거리 설정
    // =========================================================

    [Header("거리 설정")]
    [SerializeField, Tooltip("최소 사거리 — 이 거리 이하면 플레이어로부터 후퇴")]
    private float minRange = 5f;

    [SerializeField, Tooltip("최대 사거리 — 이 거리 이내면 공격 가능 (FSM의 attackRange로 사용)")]
    private float maxRange = 12f;

    [SerializeField, Tooltip("후퇴 시 NavMesh 이동 속도 배율 (평소의 N배 빠르게)")]
    private float retreatSpeedMultiplier = 1.5f;

    [SerializeField, Tooltip("후퇴 시 사용할 이동 애니메이션 Bool 이름 (보통 Walk)")]
    private string retreatAnimBool = "Walk";

    // =========================================================
    // 공격 설정
    // =========================================================

    [Header("공격 설정")]
    [SerializeField, Tooltip("레이저 한 방 데미지 (강력하게)")]
    private int laserDamage = 30;

    [SerializeField, Tooltip("공격 쿨타임 (다음 발사까지 대기 시간)")]
    private float attackCooldown = 2.5f;

    [SerializeField, Tooltip("빔 사거리 — Raycast 최대 거리")]
    private float beamRange = 15f;

    [SerializeField, Tooltip("빔 충돌 반경 — Raycast 대신 SphereCast 사용 시 두께")]
    private float beamRadius = 0.3f;

    // =========================================================
    // Telegraph 설정
    // =========================================================

    [Header("Telegraph (예고 표시)")]
    [SerializeField, Tooltip("Telegraph 표시 시간 (회피 가능 시간)")]
    private float telegraphDuration = 0.8f;

    // =========================================================
    // 빔 비주얼
    // =========================================================

    [Header("빔 비주얼")]
    [SerializeField, Tooltip("레이저 빔 prefab — 파티클 시스템 (Super Magic FX 등)")]
    private GameObject laserBeamPrefab;

    [SerializeField, Tooltip("빔 발사 위치 (없으면 자기 위치에서 발사)")]
    private Transform firePoint;

    [SerializeField, Tooltip("빔 prefab의 자동 파괴까지 시간 (파티클 수명)")]
    private float beamLifetime = 1.0f;

    // =========================================================
    // 애니메이션
    // =========================================================

    [Header("애니메이션")]
    [SerializeField, Tooltip("레이저 발사 애니메이션 트리거 이름")]
    private string laserAnimTrigger = "doLaserAttack";

    // =========================================================
    // 레이어
    // =========================================================

    [Header("레이어")]
    [SerializeField, Tooltip("플레이어 레이어 (Raycast 대상)")]
    private LayerMask playerLayer;

    [SerializeField, Tooltip("벽/장애물 레이어 (빔이 막히는 대상)")]
    private LayerMask obstacleLayer;

    // =========================================================
    // 컴포넌트 캐싱
    // =========================================================

    /// <summary>Telegraph 컴포넌트</summary>
    private AttackTelegraph telegraph;

    /// <summary>NavMesh 기본 속도 (후퇴 후 복원용)</summary>
    private float baseNavSpeed = 0f;

    // =========================================================
    // 내부 상태
    // =========================================================

    /// <summary>현재 후퇴 모드인지 (Update에서 매 프레임 갱신)</summary>
    private bool isRetreating = false;

    // =========================================================
    // 초기화
    // =========================================================

    /// <summary>
    /// 컴포넌트 캐싱
    /// base.Awake()에서 player/nav/anim/enemy 자동 캐싱
    /// 추가로 telegraph, firePoint, baseNavSpeed 처리
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        telegraph = GetComponent<AttackTelegraph>();

        if (firePoint == null)
            firePoint = transform;

        if (telegraph == null)
            Debug.LogWarning($"[{name}] AttackTelegraph 컴포넌트가 없습니다.");

        if (laserBeamPrefab == null)
            Debug.LogWarning($"[{name}] Laser Beam Prefab이 설정되지 않았습니다.");

        // NavMesh 기본 속도 기억 (후퇴 후 복원용)
        if (nav != null)
            baseNavSpeed = nav.speed;
    }

    // =========================================================
    // 매 프레임 거리 모니터링 + 후퇴 처리
    // =========================================================

    /// <summary>
    /// Update — 매 프레임 거리 체크해서 후퇴 모드 자동 토글
    /// 공격 중일 때는 후퇴 모드 비활성화 (공격 중단 방지)
    /// </summary>
    private void Update()
    {
        // 공격 중이거나 비활성 상태면 후퇴 처리 안 함
        if (isAttacking || !gameObject.activeInHierarchy) return;

        // 플레이어 유효성 체크
        if (!IsPlayerValid())
        {
            // 플레이어 죽었으면 후퇴 모드 해제
            if (isRetreating) ExitRetreatMode();
            return;
        }

        float distance = GetDistanceToPlayer();

        // 너무 가까움 → 후퇴
        if (distance < minRange)
        {
            EnterRetreatMode();
        }
        // 적정 거리 또는 멀음 → 정상
        else if (isRetreating)
        {
            ExitRetreatMode();
        }
    }

    /// <summary>
    /// 후퇴 모드 진입 — NavMesh의 destination을 플레이어 반대 방향으로 강제 설정
    /// 매 프레임 호출되지만 EnterRetreatMode가 매번 destination을 갱신해도 NavMesh는 효율적으로 처리함
    /// </summary>
    private void EnterRetreatMode()
    {
        if (nav == null || !nav.enabled || !nav.isOnNavMesh) return;

        // 후퇴 방향 = 플레이어 → 자기 (반대 방향)
        Vector3 dirFromPlayer = (transform.position - player.position);
        dirFromPlayer.y = 0f;

        if (dirFromPlayer.sqrMagnitude < 0.001f) return; // 거의 같은 위치면 무시

        // 후퇴 목표 지점 = 자기 위치 + 후퇴 방향 * 적정 거리
        // minRange보다 살짝 더 멀리 가도록 (margin 1m)
        Vector3 retreatTarget = transform.position + dirFromPlayer.normalized * (minRange + 1f);

        nav.speed = baseNavSpeed * retreatSpeedMultiplier;
        nav.SetDestination(retreatTarget);

        // Walk 애니메이션 켜기 (다리 움직임 — 미끄러짐 방지)
        // 이미 켜져있으면 SetBool은 내부적으로 무시되므로 매 프레임 호출해도 안전
        if (anim != null && !string.IsNullOrEmpty(retreatAnimBool))
            anim.SetBool(retreatAnimBool, true);

        isRetreating = true;
    }

    /// <summary>
    /// 후퇴 모드 해제 — 속도 복원, destination 클리어
    /// 이후엔 FSM의 ChasingState/AttackState가 정상 작동
    /// </summary>
    private void ExitRetreatMode()
    {
        if (nav != null && nav.enabled)
        {
            nav.speed = baseNavSpeed;
            nav.ResetPath();
        }

        // Walk 애니메이션 끄기 (이후 FSM이 자기 상태에 맞는 애니로 전환)
        if (anim != null && !string.IsNullOrEmpty(retreatAnimBool))
            anim.SetBool(retreatAnimBool, false);


        isRetreating = false;
    }

    // =========================================================
    // IEnemyAttack — abstract 오버라이드
    // =========================================================

    /// <summary>
    /// 공격 시작 — FSM에서 호출
    /// 후퇴 중이면 공격 무시 (거리 안 좋음)
    /// </summary>
    public override void StartAttack()
    {
        if (isAttacking || !gameObject.activeInHierarchy) return;
        if (!IsPlayerValid()) return;

        // 너무 가까우면 공격 무시 (후퇴 처리는 Update에서)
        if (GetDistanceToPlayer() < minRange) return;

        StartCoroutine(LaserAttackRoutine());
    }

    /// <summary>
    /// 공격 사거리 반환 — FSM이 attackRange로 사용
    /// 너무 가까울 때는 사거리를 0으로 반환 → FSM이 공격 안 함 (후퇴는 Update가 처리)
    /// </summary>
    public override float GetAttackRange()
    {
        if (player == null) return maxRange;

        float distance = GetDistanceToPlayer();

        // 너무 가까우면 공격 사거리 0 반환 → FSM이 AttackState로 전환 안 함
        if (distance < minRange) return 0f;

        return maxRange;
    }

    /// <summary>공격 쿨타임 반환</summary>
    public override float GetCooldownTime() => attackCooldown;

    /// <summary>
    /// 공격 강제 종료 — 베이스 동작에 추가 정리 없음 (코루틴 정지로 충분)
    /// </summary>
    public override void StopAttack()
    {
        base.StopAttack();
    }

    // =========================================================
    // 레이저 공격 시퀀스
    // =========================================================

    /// <summary>
    /// 레이저 발사 메인 루틴
    ///
    /// [3단계 구성]
    /// Phase 1: 조준 — LookAt + NavMesh 정지 + Telegraph 표시
    /// Phase 2: 발사 — 애니메이션 + Raycast 데미지 + 빔 prefab 표시
    /// Phase 3: 후딜 — 잠시 정지
    /// </summary>
    private IEnumerator LaserAttackRoutine()
    {
        isAttacking = true;

        // ============= Phase 1: 조준 + 차징(지잉) =============
        LookAtPlayer();
        Vector3 fireDirection = GetFireDirection();

        if (nav != null && nav.enabled)
            nav.isStopped = true;

        // 빔 파티클 생성 — 차징 시작
        SpawnBeamVisual(fireDirection);

        // 공격 애니 (차징과 동시에 거미가 자세 잡음)
        if (anim != null)
            anim.SetTrigger(laserAnimTrigger);

        // 차징 시간 동안 대기 (회피 가능)
        yield return new WaitForSeconds(telegraphDuration);

        if (!isAttacking) yield break;

        // ============= Phase 2: 발사("팡") =============
        FireLaserBeam(fireDirection);  // 데미지 판정만

        // ============= Phase 3: 후딜 =============
        if (nav != null && nav.enabled)
            nav.isStopped = false;

        yield return new WaitForSeconds(0.5f);
        isAttacking = false;
    }

    /// <summary>
    /// Raycast로 빔 경로 판정 + 데미지 적용
    /// 벽이나 플레이어와 충돌한 거리까지의 빔 길이 반환 (시각용)
    /// </summary>
    /// <param name="direction">발사 방향 (정규화됨)</param>
    /// <returns>실제 빔 길이 (벽까지 또는 최대 사거리)</returns>
    private float FireLaserBeam(Vector3 direction)
    {
        Vector3 origin = firePoint.position;

        // 플레이어 + 장애물 레이어 모두 체크
        LayerMask combinedMask = playerLayer | obstacleLayer;

        // SphereCast로 약간의 두께 두고 판정 (얇은 빔의 감지 정확도 향상)
        if (Physics.SphereCast(origin, beamRadius, direction, out RaycastHit hit, beamRange, combinedMask))
        {
            // 플레이어한테 맞은 경우만 데미지
            if (((1 << hit.collider.gameObject.layer) & playerLayer) != 0)
            {
                PlayerHealth ph = hit.collider.GetComponent<PlayerHealth>();
                if (ph != null && !ph.isDead)
                    ph.TakeDamage(laserDamage);
            }

            // 빔 길이는 충돌 지점까지
            return hit.distance;
        }

        // 아무것도 안 맞으면 최대 사거리까지
        return beamRange;
    }

    /// <summary>
    /// 레이저 빔 시각 효과 prefab 생성
    /// 빔이 직선이라 +Z 방향으로 길이가 뻗어나간다고 가정
    /// 길이는 Z 스케일로 조정 (파티클 시스템에 따라 작동 안 할 수도 있음)
    /// </summary>
    /// <param name="direction">발사 방향</param>
    /// <param name="length">빔 길이</param>
    private void SpawnBeamVisual(Vector3 direction)
    {
        if (laserBeamPrefab == null) return;

        Quaternion rotation = Quaternion.LookRotation(direction);
        GameObject beam = Instantiate(laserBeamPrefab, firePoint.position, rotation);

        // 일정 시간 후 자동 파괴 (안전장치)
        Destroy(beam, beamLifetime);
    }

    /// <summary>
    /// 자기 위치 → 플레이어 위치 방향 벡터 (XZ 평면, 정규화)
    /// 높이 차이 무시 → 점프 중인 플레이어한테도 정확
    /// </summary>
    private Vector3 GetFireDirection()
    {
        if (player == null) return transform.forward;

        Vector3 dir = player.position - firePoint.position;
        dir.y = 0f;

        return dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
    }

    // =========================================================
    // 디버그 (씬 뷰 시각화)
    // =========================================================

    /// <summary>
    /// 씬 뷰에서 거리 단계 + 빔 경로 시각화
    /// 빨강: 최소 사거리(후퇴) / 노랑: 최대 사거리(공격) / 청록: 빔 사거리
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 후퇴 거리 (이 안 들어오면 도망)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, minRange);

        // 공격 거리 (이 안에서 공격)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxRange);

        // 빔 경로 (앞 방향)
        Gizmos.color = Color.cyan;
        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        Gizmos.DrawLine(origin, origin + transform.forward * beamRange);
    }
}
