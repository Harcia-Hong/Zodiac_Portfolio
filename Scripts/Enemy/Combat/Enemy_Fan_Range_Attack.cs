// Enemy_Fan_Range_Attack.cs — 확산 부채꼴 원거리 공격

using System.Collections;
using UnityEngine;

/// <summary>
/// 부채꼴 모양으로 여러 발의 투사체를 동시 발사하는 원거리 공격
///
/// [패턴]
/// - StartAttack 호출 → 정지 + 조준 + 애니메이션
/// - windupTime 대기 (텔레그래프 = 애니메이션 자체)
/// - 발사 직전 방향 잠금 (플레이어 현재 위치 기준)
/// - projectileCount 발의 투사체를 fanAngle 각도로 펼쳐서 동시 발사
/// - recoveryTime 대기 후 종료
///
/// [밸런스 의도]
/// - 직선 투사체와 차별화: 옆걸음 회피가 어렵고 포지셔닝 강제
/// - 단, 부채꼴 뒤·옆으로 빠지면 회피 가능 (공정함 유지)
/// - 윈드업 중 플레이어 이동 → 발사 시 그 위치로 → 회피 기회 확보
///
/// [의존성]
/// - EnemyAttackBase 상속
/// - Projectile.cs (Initialize 메서드 사용)
/// </summary>
public class Enemy_Fan_Range_Attack : EnemyAttackBase
{
    // =========================================================
    // 공격 설정
    // =========================================================

    [Header("공격 설정")]
    [SerializeField, Tooltip("공격 시작 사거리")]
    private float attackRange = 12f;

    [SerializeField, Tooltip("공격 쿨타임 (공격 사이 텀)")]
    private float attackCooldown = 2.5f;

    [SerializeField, Tooltip("투사체 1발당 데미지 (다발이므로 낮게 설정)")]
    private int projectileDamage = 12;

    // =========================================================
    // 투사체 세팅
    // =========================================================

    [Header("투사체 세팅")]
    [SerializeField, Tooltip("투사체 프리팹 (Projectile 컴포넌트 부착 필수)")]
    private GameObject projectilePrefab;

    [SerializeField, Tooltip("발사 위치 (비워두면 본체 위치)")]
    private Transform firePoint;

    [SerializeField, Tooltip("투사체 속도")]
    private float projectileSpeed = 12f;

    [SerializeField, Tooltip("투사체 수명 (이 시간 후 자동 소멸)")]
    private float projectileLifetime = 4f;

    // =========================================================
    // 부채꼴 설정
    // =========================================================

    [Header("부채꼴 설정")]
    [SerializeField, Tooltip("발사할 투사체 개수 (홀수 추천 — 중앙 발 존재)"), Range(2, 11)]
    private int projectileCount = 5;

    [SerializeField, Tooltip("부채꼴 전체 각도 (도)"), Range(15f, 180f)]
    private float fanAngle = 60f;

    // =========================================================
    // 타이밍
    // =========================================================

    [Header("타이밍")]
    [SerializeField, Tooltip("윈드업 시간 (조준 모션 시간 = 텔레그래프 역할)")]
    private float windupTime = 1.0f;

    [SerializeField, Tooltip("발사 후 후딜 시간")]
    private float recoveryTime = 0.5f;

    // =========================================================
    // 애니메이션
    // =========================================================

    [Header("애니메이션")]
    [SerializeField, Tooltip("Fan Attack 애니메이션 트리거 이름 (Animator와 일치 필수)")]
    private string attackAnimTrigger = "doFanAttack";

    // =========================================================
    // IEnemyAttack — abstract 오버라이드
    // =========================================================

    /// <summary>
    /// 공격 시작 — FSM의 AttackState에서 호출
    /// </summary>
    public override void StartAttack()
    {
        if (isAttacking || !gameObject.activeInHierarchy) return;
        if (!IsPlayerValid()) return;

        StartCoroutine(FanAttackRoutine());
    }

    /// <summary>공격 시작 사거리 반환</summary>
    public override float GetAttackRange() => attackRange;

    /// <summary>공격 쿨타임 반환</summary>
    public override float GetCooldownTime() => attackCooldown;

    /// <summary>
    /// 외부 중단 처리 — 피격/사망/상태전환 시 NavMesh 복구 보장
    /// </summary>
    public override void StopAttack()
    {
        if (!isAttacking) return;

        StopAllCoroutines();
        isAttacking = false;
        RestoreNavMesh();
    }

    // =========================================================
    // 공격 루틴
    // =========================================================

    /// <summary>
    /// 확산 부채꼴 공격 실행 코루틴
    /// 1. 플레이어 조준 + NavMesh 정지
    /// 2. ⭐ 방향 잠금 (공격 시작 시점 — 윈드업 중 플레이어 추적 X)
    /// 3. 애니메이션 트리거 + 윈드업
    /// 4. 잠긴 방향으로 부채꼴 발사
    /// 5. 후딜 후 종료
    /// </summary>
    private IEnumerator FanAttackRoutine()
    {
        isAttacking = true;

        // 1. 조준 + 정지
        LookAtPlayer();
        DisableNavMesh();

        // 2. ⭐ 방향 잠금 — 공격 시작 시점의 플레이어 위치 기준
        //    윈드업 중에 플레이어가 이동해도 발사 방향은 안 바뀜
        //    시각적 일관성: 적이 향한 방향 = 발사 방향
        Transform spawnPoint = GetFirePoint();
        Vector3 lockedDirection = Vector3.zero;

        if (IsPlayerValid())
        {
            lockedDirection = (player.position - spawnPoint.position);
            lockedDirection.y = 0f;
            lockedDirection.Normalize();
        }

        // 3. 애니메이션
        if (anim != null)
            anim.SetTrigger(attackAnimTrigger);

        // 4. 윈드업 (이 시간 동안 플레이어는 회피 가능)
        yield return new WaitForSeconds(windupTime);

        // 5. 잠긴 방향으로 부채꼴 발사 (현재 플레이어 위치는 무시)
        if (lockedDirection != Vector3.zero)
        {
            FireFanProjectiles(spawnPoint, lockedDirection);
        }

        // 6. 후딜
        yield return new WaitForSeconds(recoveryTime);

        // 7. NavMesh 복구 + 종료
        RestoreNavMesh();
        isAttacking = false;
    }

    // =========================================================
    // 내부 유틸리티
    // =========================================================

    /// <summary>
    /// 발사 위치 반환 (firePoint 미설정 시 본체 transform)
    /// </summary>
    private Transform GetFirePoint()
    {
        return firePoint != null ? firePoint : transform;
    }

    /// <summary>
    /// 중심 방향을 기준으로 부채꼴 모양으로 N발 발사
    /// 각 발사체는 fanAngle / (N-1) 간격으로 균등 배치
    /// </summary>
    /// <param name="spawnPoint">발사 위치 Transform</param>
    /// <param name="centerDirection">정규화된 중심 방향 벡터</param>
    private void FireFanProjectiles(Transform spawnPoint, Vector3 centerDirection)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[Fan_Range_Attack] projectilePrefab이 비어있음");
            return;
        }

        // 부채꼴 각도 계산
        // - startAngle: 가장 왼쪽 각도 (예: -30°)
        // - angleStep: 발사체 간 간격 (예: 15°)
        float startAngle = -fanAngle / 2f;
        float angleStep = (projectileCount > 1) ? fanAngle / (projectileCount - 1) : 0f;

        for (int i = 0; i < projectileCount; i++)
        {
            // 이 발사체의 각도 (중심 방향 기준 +/- 회전)
            float angle = startAngle + (angleStep * i);

            // Y축 기준 회전 (XZ 평면이므로 Up이 회전축)
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 projectileDirection = rotation * centerDirection;

            // 투사체 생성 (방향에 맞춰 회전)
            GameObject proj = Instantiate(
                projectilePrefab,
                spawnPoint.position,
                Quaternion.LookRotation(projectileDirection)
            );

            // Projectile 컴포넌트 초기화
            Projectile projectile = proj.GetComponent<Projectile>();
            if (projectile == null)
                projectile = proj.AddComponent<Projectile>();

            projectile.Initialize(projectileDirection, projectileSpeed, projectileDamage, projectileLifetime);
        }
    }

    // =========================================================
    // 디버그 (씬 뷰 시각화)
    // =========================================================

    /// <summary>
    /// 씬 뷰에서 사거리 + 부채꼴 발사 패턴 시각화
    /// 노랑: 공격 시작 사거리 / 시안: 부채꼴 방향 미리보기 (forward 기준)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 공격 시작 사거리
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 부채꼴 미리보기 (forward 기준)
        Gizmos.color = Color.cyan;
        Transform spawnPoint = (firePoint != null) ? firePoint : transform;
        Vector3 forward = transform.forward;

        float startAngle = -fanAngle / 2f;
        float angleStep = (projectileCount > 1) ? fanAngle / (projectileCount - 1) : 0f;

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = startAngle + (angleStep * i);
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 direction = rotation * forward;

            Gizmos.DrawRay(spawnPoint.position, direction * attackRange);
        }
    }
}
