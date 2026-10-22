using System.Collections;
using UnityEngine;
using UnityEngine.AI; // NavMesh 제어용 추가
using DG.Tweening;


public class Enemy_A_Attack : MonoBehaviour, IEnemyAttack
{
    [Header("Attack Settings")]
    public float attackRange = 2f;
    public float attackDelay = 1.0f;
    public int attackDamage = 10;

    [Header("Animation Timing")]
    [Tooltip("기 모으는 시간 (찌그러짐)")]
    public float prepDuration = 0.3f;

    [Tooltip("공격하는 시간 (늘어남)")]
    public float strikeDuration = 0.1f;

    [Tooltip("복귀하는 시간 (원상복구)")]
    public float recoveryDuration = 0.2f;

    [Header("Movement")]
    public float forwardMoveAmoumt = 1.2f;
    public Transform moveRoot;

    // 컴포넌트 참조
    private Enemy enemy;
    private Transform player;
    private NavMeshAgent nav; // NavMeshAgent 참조 추가
    private AttackTelegraph telegraph;

    private bool isAttacking = false;
    private bool hasHit = false;

    // DOTween용 변수
    private Vector3 originalScale;
    private Sequence attackSequence;

    private void Awake()
    {
        player = GameObject.FindWithTag("Player").transform;
        enemy = GetComponent<Enemy>();
        nav = GetComponent<NavMeshAgent>(); // 가져오기
        telegraph = GetComponent<AttackTelegraph>();
        if (moveRoot == null) moveRoot = transform;

        originalScale = transform.localScale;
    }

    private void OnDisable()
    {
        // 오브젝트 꺼질 때 트윈 정리 필수
        if (transform != null) transform.DOKill();
    }

    #region IEnemyAttack 인터페이스 구현

    public void StartAttack()
    {
        if (!isAttacking)
        {
            isAttacking = true;
            hasHit = false;

            LookAtPlayer();

            // 공격 시 NavMesh 비활성화 (미끄러짐 방지)
            if (nav != null) nav.enabled = false;

            // 텔레그래프가 이미 예고했으면 prep 스킵, 없으면 전체 연출
            if (telegraph != null)
            {
                PlayStrikeOnly();
            }
            else
            {
                PlayFullEffect();
            }

            // 안전장치: Animation Event 누락 시 강제 종료
            StartCoroutine(SafetyBreakRoutine());
        }
    }

    public void StopAttack()
    {
        // 강제 중단 시 초기화
        isAttacking = false;
        hasHit = false;

        // 공격 중단 시 트윈 즉시 종료하고 원래 크기로 복구
        if (transform != null)
        {
            transform.DOKill();
            transform.localScale = originalScale;
        }

        // NavMesh 복구
        if (nav != null && !nav.enabled)
        {
            nav.Warp(transform.position); // 위치 동기화 필수
            nav.enabled = true;
        }
    }

    public bool IsAttacking() => isAttacking;
    public float GetAttackRange() => attackRange;
    public float GetCooldownTime() => attackDelay;

    #endregion

    #region DOTween Effect

    /// <summary>
    /// 전체 연출 (텔레그래프 없을 때) - prep + strike + recovery
    /// </summary>
    private void PlayFullEffect()
    {
        transform.DOKill();
        attackSequence = DOTween.Sequence();

        // 1. 준비 (Squash)
        attackSequence.Append(
            transform.DOScale(new Vector3(originalScale.x * 1.3f, originalScale.y * 0.6f, originalScale.z * 1.3f), prepDuration)
            .SetEase(Ease.OutQuad)
        );

        // 2. 타격 (Stretch)
        attackSequence.Append(
            transform.DOScale(new Vector3(originalScale.x * 0.8f, originalScale.y * 1.2f, originalScale.z * 0.8f), strikeDuration)
            .SetEase(Ease.OutBack)
        );

        // 3. 복귀 (Recovery)
        attackSequence.Append(
            transform.DOScale(originalScale, recoveryDuration)
            .SetEase(Ease.OutElastic)
        );
    }

    /// <summary>
    /// strike만 실행 (텔레그래프가 prep 역할을 대신했을 때)
    /// </summary>
    private void PlayStrikeOnly()
    {
        transform.DOKill();
        attackSequence = DOTween.Sequence();

        // 바로 타격 (Stretch)
        attackSequence.Append(
            transform.DOScale(new Vector3(originalScale.x * 0.8f, originalScale.y * 1.2f, originalScale.z * 0.8f), strikeDuration)
            .SetEase(Ease.OutBack)
        );

        // 복귀 (Recovery)
        attackSequence.Append(
            transform.DOScale(originalScale, recoveryDuration)
            .SetEase(Ease.OutElastic)
        );
    }

    #endregion
        
    #region Animation Events

    public void OnAnimationHit()
    {
        if (!isAttacking || hasHit) return;

        MoveForward();

        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.position);
            if (distance <= attackRange + 0.5f)
            {
                HitPlayer();
            }
        }
    }

    // 이벤트: 애니메이션 끝
    public void OnAnimationEnd()
    {
        FinishAttack();
    }

    #endregion

    #region Helper Methods

    // 공격 종료 처리 통합 메서드
    private void FinishAttack()
    {
        if (!isAttacking) return;

        isAttacking = false;

        // 공격 끝날 때도 크기 안전하게 원상복구
        transform.DOKill();
        transform.localScale = originalScale;

        // [수정] 공격 끝나면 NavMesh 다시 켜기
        if (nav != null)
        {
            nav.Warp(transform.position); // 현재 위치로 네비메시 강제 이동
            nav.enabled = true;
        }
    }

    // [핵심] 안전장치: 이벤트가 안 불려도 일정 시간 후 강제 종료
    private IEnumerator SafetyBreakRoutine()
    {
        // 애니메이션 길이(약 0.8초)보다 넉넉하게 기다림
        yield return new WaitForSeconds(attackDelay + 0.2f);

        if (isAttacking)
        {
            Debug.LogWarning("[Enemy_A_Attack] 애니메이션 이벤트 누락됨 -> 강제 종료");
            FinishAttack();
        }
    }

    private void LookAtPlayer()
    {
        if (player == null) return;
        Vector3 dir = (player.position - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    private void HitPlayer()
    {
        hasHit = true;
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(attackDamage);
            Debug.Log($"[Enemy_A_Attack] Hit! Damage: {attackDamage}");
        }
    }

    private void MoveForward()
    {
        if (moveRoot != null)
        {
            moveRoot.position += transform.forward * forwardMoveAmoumt;
        }
    }

    #endregion
}
