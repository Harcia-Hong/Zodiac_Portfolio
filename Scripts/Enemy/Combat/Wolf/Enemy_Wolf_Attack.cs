// Enemy_Wolf_Attack.cs — 전체 재작성

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 늑대 돌진 물기 공격 (달리면서 끊김 없이 공격)
/// 
/// 핵심: NavMesh 추적과 공격이 끊기지 않는 연속 동작
/// 
/// 흐름:
///   1. 달리는 상태 그대로 NavMesh 끄기 (멈추지 않음)
///   2. 현재 속도 유지하면서 JumpBite_RM 재생 (Root Motion)
///   3. 물기 타이밍에 데미지 판정
///   4. 착지 즉시 Run 상태로 감속 슬라이드
///   5. 슬라이드 중 플레이어 방향으로 턴 (RunTurnL/R_RM)
///   6. 턴 완료 → NavMesh 복구 (현재 위치 기준)
/// 
/// 주의: EnemyAttackState가 Enter()에서 멈추는 걸 무시하기 위해
///       StartAttack() 내부에서 직접 이동 상태를 이어받음
/// </summary>
public class Enemy_Wolf_Attack : MonoBehaviour, IEnemyAttack
{
    [Header("공격 설정")]
    [SerializeField, Tooltip("공격 사거리 (FSM 전환 기준)")]
    private float attackRange = 5f;

    [SerializeField, Tooltip("공격 쿨타임")]
    private float attackCooldown = 2.5f;

    [SerializeField, Tooltip("공격 데미지")]
    private int attackDamage = 12;

    [Header("판정 설정")]
    [SerializeField, Tooltip("물기 데미지 판정 범위")]
    private float hitRadius = 1.8f;

    [SerializeField, Tooltip("플레이어 레이어")]
    private LayerMask playerLayer;

    [Header("JumpBite 타이밍")]
    [SerializeField, Tooltip("데미지 판정 시작 (초)")]
    private float damageStart = 0.3f;

    [SerializeField, Tooltip("데미지 판정 종료 (초)")]
    private float damageEnd = 0.6f;

    [SerializeField, Tooltip("JumpBite 애니메이션 길이 (초)")]
    private float jumpBiteLength = 0.8f;

    [Header("착지 슬라이드")]
    [SerializeField, Tooltip("슬라이드 시작 속도 (NavMesh 이동속도와 비슷하게)")]
    private float slideStartSpeed = 7f;

    [SerializeField, Tooltip("슬라이드 지속 시간 (초)")]
    private float slideDuration = 0.35f;

    [Header("턴 설정")]
    [SerializeField, Tooltip("턴 애니메이션 길이 (초)")]
    private float turnDuration = 0.45f;

    [Header("벽 감지")]
    [SerializeField, Tooltip("벽 레이어")]
    private LayerMask wallLayer;

    [SerializeField, Tooltip("벽 감지 거리")]
    private float wallCheckDistance = 1f;

    // 컴포넌트 참조
    private Transform player;
    private NavMeshAgent nav;
    private Animator anim;

    // 공격 시작 시점의 이동 방향/속도 (끊김 방지용)
    private Vector3 rushDirection;
    private float rushSpeed;

    // 상태
    private bool isAttacking = false;
    private bool hasHit = false;

    private void Awake()
    {
        nav = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    #region IEnemyAttack 구현

    public void StartAttack()
    {
        if (!isAttacking && gameObject.activeInHierarchy)
        {
            // 현재 이동 상태를 캡처 (달리던 방향과 속도를 이어받음)
            CaptureMovementState();
            StartCoroutine(WolfAttackRoutine());
        }
    }

    public void StopAttack()
    {
        if (isAttacking)
        {
            StopAllCoroutines();
            FinishAttack();
        }
    }

    public bool IsAttacking() => isAttacking;
    public float GetAttackRange() => attackRange;
    public float GetCooldownTime() => attackCooldown;

    #endregion

    #region 이동 상태 캡처

    /// <summary>
    /// 공격 시작 직전의 이동 방향/속도를 저장
    /// NavMesh가 꺼져도 이 방향으로 계속 이동하여 끊김 방지
    /// </summary>
    private void CaptureMovementState()
    {
        if (player != null)
        {
            rushDirection = (player.position - transform.position).normalized;
            rushDirection.y = 0f;
        }
        else
        {
            rushDirection = transform.forward;
        }

        // NavMesh의 현재 속도를 이어받음
        rushSpeed = nav != null && nav.enabled ? nav.velocity.magnitude : 5f;
        if (rushSpeed < 3f) rushSpeed = 5f; // 최소 속도 보장
    }

    #endregion

    #region 메인 공격 루틴

    /// <summary>
    /// 늑대 공격 전체 흐름 (끊김 없는 연속 동작)
    /// 달리면서 점프 → 착지 슬라이드 → 턴 → 종료
    /// </summary>
    private IEnumerator WolfAttackRoutine()
    {
        isAttacking = true;
        hasHit = false;

        // 1. 플레이어 방향 회전 (달리면서 자연스럽게)
        transform.rotation = Quaternion.LookRotation(rushDirection);

        // 2. NavMesh 끄기 (이 시점부터 코드가 이동 제어)
        if (nav != null && nav.enabled)
        {
            nav.isStopped = true;
            nav.enabled = false;
        }

        // 3. 달리면서 점프물기 (Root Motion + 보조 전진)
        yield return StartCoroutine(RunIntoJumpBite());

        // 4. 착지 즉시 관성 슬라이드
        yield return StartCoroutine(SlidePhase());

        // 5. 플레이어 방향으로 턴
        yield return StartCoroutine(TurnPhase());

        // 6. 공격 종료 → NavMesh 복구
        FinishAttack();
    }

    #endregion

    #region Phase 1 — 달리면서 점프물기

    /// <summary>
    /// 달리던 속도를 유지하면서 JumpBite_RM 재생
    /// Root Motion이 점프를 처리하고, 코드는 전진 관성을 보조
    /// </summary>
    private IEnumerator RunIntoJumpBite()
    {
        // Root Motion 활성화 + 점프 애니메이션
        if (anim != null)
        {
            anim.applyRootMotion = true;
            anim.SetBool("Walk", false);
            anim.SetTrigger("doJumpAttack");
        }

        float elapsed = 0f;

        while (elapsed < jumpBiteLength)
        {
            elapsed += Time.deltaTime;

            // Root Motion 보조: 초반에 전진 관성 추가 (자연스러운 돌진감)
            float boostT = elapsed / jumpBiteLength;
            if (boostT < 0.4f)
            {
                // 초반 40%는 달리던 관성으로 추가 전진
                float boost = rushSpeed * (1f - boostT / 0.4f) * 0.3f;
                
                if (!IsWallAhead())
                    transform.position += rushDirection * boost * Time.deltaTime;
            }

            // 데미지 판정 구간
            if (elapsed >= damageStart && elapsed <= damageEnd && !hasHit)
                CheckHit();

            yield return null;
        }

        // Root Motion 끄기 (이후는 코드가 이동 제어)
        if (anim != null)
            anim.applyRootMotion = false;
    }

    #endregion

    #region Phase 2 — 착지 관성 슬라이드

    /// <summary>
    /// 착지 직후 Run 상태로 전환하면서 감속 슬라이드
    /// 멈추지 않고 달리는 자세로 미끄러짐
    /// </summary>
    private IEnumerator SlidePhase()
    {
        // 착지 즉시 Run 애니메이션 (달리는 자세로 밀리는 느낌)
        if (anim != null)
            anim.SetBool("Walk", true);

        // 슬라이드 방향 = 현재 바라보는 방향
        Vector3 slideDir = transform.forward;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideDuration;

            // 감속 곡선 (빠름 → 느림, 자연스러운 마찰감)
            float speed = Mathf.Lerp(slideStartSpeed, slideStartSpeed * 0.2f, t);

            if (!IsWallAhead())
                transform.position += slideDir * speed * Time.deltaTime;
            else
                break;

            yield return null;
        }
    }

    #endregion

    #region Phase 3 — 턴 (플레이어 방향으로)

    /// <summary>
    /// 플레이어 방향 기준 좌/우 턴
    /// Root Motion 턴으로 자연스럽게 방향 전환
    /// 턴 중에도 약간 전진 (완전히 멈추지 않음)
    /// </summary>
    private IEnumerator TurnPhase()
    {
        if (player == null || anim == null)
        {
            // 플레이어 없으면 턴 스킵
            if (anim != null) anim.SetBool("Walk", false);
            yield break;
        }

        // 플레이어가 내 좌/우 어디에 있는지 판단
        Vector3 toPlayer = (player.position - transform.position).normalized;
        toPlayer.y = 0f;
        float dot = Vector3.Dot(transform.right, toPlayer);

        // Root Motion 턴 애니메이션 재생
        anim.applyRootMotion = true;

        if (dot >= 0f)
            anim.SetTrigger("doTurnR");
        else
            anim.SetTrigger("doTurnL");

        // 턴 애니메이션 대기
        float elapsed = 0f;
        while (elapsed < turnDuration)
        {
            elapsed += Time.deltaTime;

            // 턴 중에도 약간 전진 (멈추지 않는 느낌)
            float moveSpeed = slideStartSpeed * 0.3f;
            if (!IsWallAhead())
                transform.position += transform.forward * moveSpeed * Time.deltaTime;

            yield return null;
        }

        // Root Motion 끄기
        anim.applyRootMotion = false;
        anim.SetBool("Walk", false);
    }

    #endregion

    #region 데미지 판정

    /// <summary>
    /// 범위 내 플레이어 히트 체크
    /// 늑대 앞쪽에 판정 (몸통 + 전방)
    /// </summary>
    private void CheckHit()
    {
        // 판정 중심을 늑대 앞쪽으로 (물기니까)
        Vector3 hitCenter = transform.position + transform.forward * 1f;

        Collider[] hits = Physics.OverlapSphere(hitCenter, hitRadius, playerLayer);

        foreach (var hit in hits)
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                hasHit = true;
                ph.TakeDamage(attackDamage);
                Debug.Log($"[Wolf] 돌진 물기 적중! 데미지: {attackDamage}");
                return;
            }
        }
    }

    #endregion

    #region 유틸리티

    /// <summary>
    /// 전방 벽 감지
    /// </summary>
    private bool IsWallAhead()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.5f, 
            transform.forward, wallCheckDistance, wallLayer);
    }

    /// <summary>
    /// 공격 종료 — NavMesh를 현재 위치에서 복구
    /// </summary>
    private void FinishAttack()
    {
        isAttacking = false;
        hasHit = false;

        // 애니메이션 정리
        if (anim != null)
        {
            anim.applyRootMotion = false;
            anim.SetBool("Walk", false);
        }

        // NavMesh 복구 — 반드시 현재 위치 기준
        if (nav != null && !nav.enabled)
        {
            // 현재 위치를 NavMesh 위의 가장 가까운 유효 지점으로 보정
            NavMeshHit navHit;
            Vector3 restorePos = transform.position;

            if (NavMesh.SamplePosition(transform.position, out navHit, 3f, NavMesh.AllAreas))
            {
                restorePos = navHit.position;
            }

            nav.enabled = true;
            nav.Warp(restorePos);
            nav.isStopped = false;

            Debug.Log($"[Wolf] NavMesh 복구 위치: {restorePos}");
        }
    }

    #endregion

    #region 디버그

    private void OnDrawGizmosSelected()
    {
        // 공격 사거리
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 물기 판정 범위 (전방)
        Gizmos.color = Color.yellow;
        Vector3 hitCenter = transform.position + transform.forward * 1f;
        Gizmos.DrawWireSphere(hitCenter, hitRadius);

        // 벽 감지 레이
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, 
            transform.forward * wallCheckDistance);

        // 돌진 방향 (실행 중)
        if (Application.isPlaying && isAttacking)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, rushDirection * 3f);
        }
    }

    #endregion
}