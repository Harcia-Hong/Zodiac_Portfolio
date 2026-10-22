// Enemy_LineExplosion_Attack.cs — 땅 찍기 + 직선 연쇄 원형 폭발 공격

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 땅을 찍고 직선 방향으로 원형 폭발이 순차적으로 터지는 공격
///
/// [패턴]
/// - StartAttack 호출 → 플레이어 방향 잠금 + Hit Ground 애니메이션
/// - 직선상에 폭발 N개의 위치 계산 (간격 = explosionSpacing)
/// - 모든 위치에 텔레그래프 동시 생성 → telegraphDuration 대기
/// - 가까운 폭발부터 explosionInterval 주기로 순차 발동
/// - 각 폭발마다 OverlapSphere로 데미지 판정
///
/// [밸런스 의도]
/// - 직선 라인이라 옆으로 빠지면 회피 가능 (공정한 패턴)
/// - 라인 안에 있으면 여러 발 맞을 수 있어 위험
/// - 보라 로봇의 "넓은 공간 압박" 컨셉
///
/// [의존성]
/// - EnemyAttackBase 상속
/// - 텔레그래프 프리팹: 기존 circleIndicatorPrefab 재사용 가능 (납작한 원형 인디케이터)
/// - 폭발 VFX 프리팹: 선택사항 (없어도 동작)
/// </summary>
public class Enemy_LineExplosion_Attack : EnemyAttackBase
{
    // =========================================================
    // 공격 설정
    // =========================================================

    [Header("공격 설정")]
    [SerializeField, Tooltip("공격 시작 사거리")]
    private float attackRange = 10f;

    [SerializeField, Tooltip("공격 쿨타임 (공격 사이 텀)")]
    private float attackCooldown = 4f;

    // =========================================================
    // Line Explosion 세부
    // =========================================================

    [Header("Line Explosion 세부")]
    [SerializeField, Tooltip("연쇄 폭발 개수")]
    private int explosionCount = 5;

    [SerializeField, Tooltip("각 폭발 사이의 거리 (m)")]
    private float explosionSpacing = 3f;

    [SerializeField, Tooltip("각 폭발의 반경 (m)")]
    private float explosionRadius = 1.5f;

    [SerializeField, Tooltip("폭발 1회당 데미지")]
    private int explosionDamage = 15;

    [SerializeField, Tooltip("폭발 사이 시간 간격 (초)")]
    private float explosionInterval = 0.2f;

    // =========================================================
    // 타이밍
    // =========================================================

    [Header("타이밍")]
    [SerializeField, Tooltip("애니 시작 → 텔레그래프 등장까지 (펀치 모션 시간)")]
    private float animLeadIn = 0.3f;

    [SerializeField, Tooltip("텔레그래프 표시 시간 (플레이어 회피 시간)")]
    private float telegraphDuration = 0.7f;

    [SerializeField, Tooltip("전체 애니메이션 길이 (후딜 보정용)")]
    private float animTotalLength = 1.5f;

    // =========================================================
    // 애니메이션 & 이펙트
    // =========================================================

    [Header("애니메이션 & 이펙트")]
    [SerializeField, Tooltip("Hit Ground 애니메이션 트리거 이름")]
    private string hitGroundAnimTrigger = "doHitGround";

    [SerializeField, Tooltip("위험 표시용 텔레그래프 프리팹 (납작한 원형 인디케이터)")]
    private GameObject telegraphPrefab;

    [SerializeField, Tooltip("폭발 VFX 프리팹 (선택, 없어도 동작)")]
    private GameObject explosionVFXPrefab;

    // =========================================================
    // 레이어
    // =========================================================

    [Header("레이어")]
    [SerializeField, Tooltip("플레이어 레이어 (히트 판정 대상)")]
    private LayerMask playerLayer;

    [SerializeField, Tooltip("지면 레이어 (Raycast로 폭발 위치를 지면에 붙이기)")]
    private LayerMask groundLayer;

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

        StartCoroutine(LineExplosionRoutine());
    }

    /// <summary>공격 시작 사거리 반환</summary>
    public override float GetAttackRange() => attackRange;

    /// <summary>공격 쿨타임 반환</summary>
    public override float GetCooldownTime() => attackCooldown;

    // =========================================================
    // 공격 루틴
    // =========================================================

    /// <summary>
    /// 직선 연쇄 폭발 실행 코루틴
    /// 1. 방향 잠금 + NavMesh 끄기
    /// 2. 폭발 위치 5개 계산 (지면 Raycast 포함)
    /// 3. Hit Ground 애니 트리거 후 펀치 모션 대기
    /// 4. 모든 텔레그래프 동시 생성
    /// 5. 텔레그래프 시간 대기 (회피 가능)
    /// 6. 순차 폭발 (가까운 거부터)
    /// 7. 후딜 + NavMesh 복구
    /// </summary>
    private IEnumerator LineExplosionRoutine()
    {
        isAttacking = true;

        // 1. 플레이어 조준 + NavMesh 정지 (애니 중 미끄러짐 방지)
        LookAtPlayer();
        DisableNavMesh();

        // 2. 공격 방향 잠금 (시작 시점의 플레이어 위치 기준)
        Vector3 attackDirection = (player.position - transform.position);
        attackDirection.y = 0f;
        attackDirection.Normalize();

        // 3. 폭발 위치 5개 계산 (몬스터 앞쪽에서 시작, 간격 explosionSpacing)
        List<Vector3> positions = CalculateExplosionPositions(attackDirection);

        // 4. Hit Ground 애니메이션 트리거
        if (anim != null)
            anim.SetTrigger(hitGroundAnimTrigger);

        // 5. 펀치 모션 대기 (땅을 찍기 직전까지)
        yield return new WaitForSeconds(animLeadIn);

        // 6. 모든 텔레그래프 동시 생성 (라인 전체 가독성 ↑)
        List<GameObject> telegraphs = SpawnTelegraphs(positions);

        // 7. 텔레그래프 표시 시간 대기 (플레이어 회피 시간)
        yield return new WaitForSeconds(telegraphDuration);

        // 8. 순차 폭발 (가까운 위치부터)
        for (int i = 0; i < positions.Count; i++)
        {
            // 해당 위치의 텔레그래프 제거
            if (i < telegraphs.Count && telegraphs[i] != null)
                Destroy(telegraphs[i]);

            // 폭발 VFX 생성
            if (explosionVFXPrefab != null)
                Instantiate(explosionVFXPrefab, positions[i], Quaternion.identity);

            // 데미지 판정
            DealExplosionDamage(positions[i]);

            // 다음 폭발까지 대기 (마지막 폭발 후엔 대기 X)
            if (i < positions.Count - 1)
                yield return new WaitForSeconds(explosionInterval);
        }

        // 9. 애니메이션 후딜 잔여 시간 대기
        float elapsedTime = animLeadIn + telegraphDuration + (explosionCount - 1) * explosionInterval;
        float remaining = animTotalLength - elapsedTime;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        // 10. NavMesh 복구 + 종료
        RestoreNavMesh();
        isAttacking = false;
    }

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
    // 내부 유틸리티
    // =========================================================

    /// <summary>
    /// 공격 방향을 기준으로 폭발 위치 N개 계산
    /// 각 위치는 Raycast로 지면에 붙여서 경사면에도 대응
    /// </summary>
    /// <param name="direction">정규화된 공격 방향 벡터</param>
    /// <returns>월드 좌표 리스트</returns>
    private List<Vector3> CalculateExplosionPositions(Vector3 direction)
    {
        List<Vector3> positions = new List<Vector3>();

        for (int i = 0; i < explosionCount; i++)
        {
            // 몬스터 위치 + 방향 * 간격 * (인덱스+1)
            // i=0이면 explosionSpacing 거리, i=1이면 2 * spacing ...
            Vector3 pos = transform.position + direction * explosionSpacing * (i + 1);

            // 지면 Raycast로 Y 좌표 보정
            Vector3 rayStart = pos + Vector3.up * 5f;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 20f, groundLayer))
            {
                pos = hit.point;
            }

            positions.Add(pos);
        }

        return positions;
    }

    /// <summary>
    /// 주어진 위치들에 텔레그래프 프리팹 동시 인스턴스화
    /// 모든 텔레그래프는 폭발 반경에 맞춰 스케일 조정
    /// </summary>
    /// <returns>생성된 텔레그래프 오브젝트 리스트 (나중에 Destroy용)</returns>
    private List<GameObject> SpawnTelegraphs(List<Vector3> positions)
    {
        List<GameObject> telegraphs = new List<GameObject>();

        if (telegraphPrefab == null) return telegraphs;

        // 텔레그래프 회전: 바닥에 누워있도록 (X축 90도)
        Quaternion flatRotation = Quaternion.Euler(90f, 0f, 0f);

        foreach (var pos in positions)
        {
            // 지면에서 살짝 띄움 (Z-fight 방지)
            Vector3 spawnPos = pos + Vector3.up * 0.05f;

            GameObject tel = Instantiate(telegraphPrefab, spawnPos, flatRotation);

            // 폭발 반경에 맞춰 스케일 (지름 = radius * 2)
            tel.transform.localScale = Vector3.one * explosionRadius * 2f;

            telegraphs.Add(tel);
        }

        return telegraphs;
    }

    /// <summary>
    /// 지정 위치 기준 OverlapSphere로 플레이어 감지 → 데미지 적용
    /// 한 폭발에서 한 번만 데미지 (플레이어 1명 가정)
    /// </summary>
    private void DealExplosionDamage(Vector3 center)
    {
        Collider[] hits = Physics.OverlapSphere(center, explosionRadius, playerLayer);

        foreach (var hit in hits)
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph != null && !ph.isDead)
            {
                ph.TakeDamage(explosionDamage);
                return; // 1번 폭발당 1번 데미지
            }
        }
    }

    // =========================================================
    // 디버그 (씬 뷰 시각화)
    // =========================================================

    /// <summary>
    /// 씬 뷰에서 사거리 + 폭발 라인 시각화
    /// 노랑: 공격 시작 사거리 / 빨강: 폭발 위치 (forward 기준 미리보기)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 공격 시작 사거리
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 폭발 라인 미리보기 (몬스터의 forward 기준)
        Gizmos.color = Color.red;
        Vector3 forward = transform.forward;
        for (int i = 0; i < explosionCount; i++)
        {
            Vector3 pos = transform.position + forward * explosionSpacing * (i + 1);
            Gizmos.DrawWireSphere(pos, explosionRadius);
        }
    }
}
