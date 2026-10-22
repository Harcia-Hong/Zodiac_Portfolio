// Enemy_Spider_Melee_Attack.cs — 거미형 근접 전용 공격 (Spiderling 같은 잡몹용)

using System.Collections;
using UnityEngine;

/// <summary>
/// 거미형 적의 근접 전용 공격 컴포넌트
///
/// [패턴]
/// - Stab Attack(찌르기) 1종만 사용
/// - 거리 판단 없음 → 사거리 안에 들어오면 즉시 공격
/// - 빠르고 민첩한 잡몹(Spiderling)에 적합
///
/// [Hybrid 버전과의 차이]
/// - 원거리/투사체 관련 로직 전부 제거
/// - 코드가 단순하고 가벼움 → 잡몹용으로 최적
///
/// [의존성]
/// - EnemyAttackBase 상속 (player/nav/anim/enemy 자동 캐싱, NavMesh 제어 등)
/// </summary>
public class Enemy_Spider_Melee_Attack : EnemyAttackBase
{
    // =========================================================
    // 공격 설정
    // =========================================================

    [Header("공격 설정")]
    [SerializeField, Tooltip("근접 공격 사거리 (이 거리 안에서 공격 시작)")]
    private float meleeRange = 2.5f;

    [SerializeField, Tooltip("공격 쿨타임 (다음 공격까지 대기 시간)")]
    private float attackCooldown = 1.5f;

    // =========================================================
    // Stab Attack 세부
    // =========================================================

    [Header("Stab Attack 세부")]
    [SerializeField, Tooltip("근접 공격 데미지")]
    private int meleeDamage = 10;

    [SerializeField, Tooltip("데미지 판정 범위 (자기 위치 기준 구체)")]
    private float meleeHitRadius = 1.5f;

    [SerializeField, Tooltip("근접 애니메이션 트리거 이름 (Animator와 일치 필수)")]
    private string meleeAnimTrigger = "doStabAttack";

    [SerializeField, Tooltip("애니메이션 시작 후 데미지 판정 시작 시점 (초)")]
    private float meleeDamageStart = 0.25f;

    [SerializeField, Tooltip("데미지 판정 종료 시점 (초)")]
    private float meleeDamageEnd = 0.5f;

    [SerializeField, Tooltip("근접 애니메이션 전체 길이 (초)")]
    private float meleeAnimLength = 0.8f;

    // =========================================================
    // 레이어
    // =========================================================

    [Header("레이어")]
    [SerializeField, Tooltip("플레이어 레이어 (히트 판정 대상)")]
    private LayerMask playerLayer;

    // =========================================================
    // 내부 상태
    // =========================================================

    /// <summary>이번 공격에서 이미 히트했는지 여부 (다중 히트 방지)</summary>
    private bool hasHit = false;

    // =========================================================
    // IEnemyAttack — abstract 오버라이드
    // =========================================================

    /// <summary>
    /// 공격 시작 — FSM의 AttackState에서 호출
    /// 항상 근접 공격(Stab Attack)을 수행
    /// </summary>
    public override void StartAttack()
    {
        // 이미 공격 중이거나 비활성 상태면 무시
        if (isAttacking || !gameObject.activeInHierarchy) return;

        // 플레이어 유효성 확인 (죽은 플레이어한테 헛스윙 방지)
        if (!IsPlayerValid()) return;

        StartCoroutine(MeleeAttackRoutine());
    }

    /// <summary>근접 공격 사거리 반환</summary>
    public override float GetAttackRange() => meleeRange;

    /// <summary>공격 쿨타임 반환</summary>
    public override float GetCooldownTime() => attackCooldown;

    // =========================================================
    // 공격 루틴
    // =========================================================

    /// <summary>
    /// Stab Attack 실행 코루틴
    /// 1. 플레이어 조준 + NavMesh 끄기 (미끄러짐 방지)
    /// 2. Stab 애니메이션 트리거
    /// 3. 데미지 윈도우 동안 매 프레임 히트 체크
    /// 4. 애니 종료 후 NavMesh 복구
    /// </summary>
    private IEnumerator MeleeAttackRoutine()
    {
        isAttacking = true;
        hasHit = false;

        // 1. 플레이어 조준 + NavMesh 끄기
        LookAtPlayer();
        DisableNavMesh();

        // 2. Stab 애니메이션 재생
        if (anim != null)
            anim.SetTrigger(meleeAnimTrigger);

        // 3. 데미지 판정 시작 시점까지 대기
        yield return new WaitForSeconds(meleeDamageStart);

        // 4. 데미지 윈도우 동안 매 프레임 히트 체크 (hasHit으로 다중 히트 차단)
        float elapsed = 0f;
        float window = meleeDamageEnd - meleeDamageStart;

        while (elapsed < window)
        {
            if (!hasHit)
                CheckMeleeHit();

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 5. 애니메이션 잔여 시간 대기 (모션 자연스럽게 마무리)
        float remaining = meleeAnimLength - meleeDamageEnd;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        // 6. NavMesh 복구 + 종료
        RestoreNavMesh();
        isAttacking = false;
    }

    /// <summary>
    /// 자기 위치 기준 OverlapSphere로 플레이어 감지 → 데미지 적용
    /// hasHit 플래그로 한 공격당 1회만 데미지 들어가도록 보장
    /// </summary>
    private void CheckMeleeHit()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, meleeHitRadius, playerLayer);

        foreach (var hit in hits)
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph != null && !ph.isDead)
            {
                hasHit = true;
                ph.TakeDamage(meleeDamage);
                return; // 첫 히트 후 즉시 종료
            }
        }
    }

    // =========================================================
    // 디버그 (씬 뷰 시각화)
    // =========================================================

    /// <summary>
    /// 씬 뷰에서 사거리/판정 범위 시각화
    /// 빨강: 근접 사거리 / 주황: 히트 판정 범위
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 근접 사거리
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);

        // 히트 판정 범위
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); // 주황
        Gizmos.DrawWireSphere(transform.position, meleeHitRadius);
    }
}
