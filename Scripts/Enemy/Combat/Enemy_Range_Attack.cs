using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Enemy_Range_Attack : MonoBehaviour, IEnemyAttack
{
    [Header("원거리 공격 세팅")]
    public float attackRange = 8f;          // 공격 사거리
    public float attackDelay = 2f;          // 공격 쿨타임
    public int projectileDamage = 15;       // 투사체 데미지

    [Header("투사체 세팅")]
    public GameObject projectilePrefab;     // 투사체 프리팹
    public Transform firePoint;             // 발사 위치
    public float projectileSpeed = 10f;     // 투사체 속도
    public float projectileLifetime = 5f;   // 투사체 수명

    // 컴포넌트 참조
    Enemy enemy;
    Transform player;
    Animator anim;
    NavMeshAgent nav;

    // 상태 관리
    bool isAttacking = false;

    private void Awake()
    {
        player = GameObject.FindWithTag("Player").transform;
        anim = GetComponentInChildren<Animator>();
        enemy = GetComponent<Enemy>();
        nav = GetComponent<NavMeshAgent>();

        if (firePoint == null)
            firePoint = transform;
    }

    private void Update()
    {
        /*if (player == null || (enemy != null && (enemy.isHit || enemy.isDead))) return;

        var hitReactionSystem = enemy.GetComponent<EnemyHitReactionSystem>();
        if (hitReactionSystem != null && hitReactionSystem.IsInGroggy()) return;

        // XZ 에서 거리 계산
        Vector3 playerPosXZ = new Vector3(player.position.x, 0, player.position.z);
        Vector3 myPosXZ = new Vector3(transform.position.x, 0, transform.position.z);
        float distance = Vector3.Distance(myPosXZ, playerPosXZ);

        // 플레이어 바라보기
        LookAtPlayer();

        // 거리에 따른 패턴 결정
        if (distance < minRange && CanRetreat())
        {
            // 후퇴가 가능하면 후퇴하기
            Retreat();
        }
        else if(distance <= attackRange && CanAttack())
        {
            // 공격 범위 내에 있고 공격이 가능하면 공격
            StopMovement();
            StartCoroutine(RangeAttackRoutine());
        }
        else if(distance > attackRange)
        {
            // 너무 멀면 천천히 다시 접근
            ApproachSlowly();
        }
        else
        {
            // 제자리 대기
            StopMovement();
        }*/
    }

    /// <summary>
    /// 원거리 공격 루틴 - FSM 전용으로 단순화
    /// 정지 → 조준(애니메이션) → 발사 → 쿨타임
    /// </summary>
    IEnumerator RangeAttackRoutine()
    {
        isAttacking = true;

        // 공격 시작 시 정확한 회전
        LookAtPlayerInstant();

        // FSM이 애니메이션을 제어하므로 Trigger 호출 제거
        // 대신 FSM의 @Attack, BaseAttack Bool 파라미터가 애니메이션 제어

        Debug.Log($"[Enemy_Range_Attack] 공격 시작 - FSM이 애니메이션 제어");

        // 조준 모션 시간 (윈드업)
        yield return new WaitForSeconds(1.2f);

        // 투사체 발사 직전 취소 여부
        if (!isAttacking)
        {
            Debug.Log($"[Enemy_Range_Attack] 공격 취소됨 - 투사체 발사 안 함");
            yield break; // 코루틴 즉시 종료
        }

        // 투사체 발사
        FireProjectile();

        // 공격 후 쿨타임
        yield return new WaitForSeconds(attackDelay - 0.5f);

        isAttacking = false;

        Debug.Log($"[Enemy_Range_Attack] 공격 완료");
    }

    /// <summary>
    /// 즉시 플레이어 방향으로 회전
    /// </summary>
    private void LookAtPlayerInstant()
    {
        if (player == null) return;

        Vector3 direction = (player.position - transform.position);
        direction.y = 0;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = targetRotation; // 즉시 회전
        }
    }

    /// <summary>
    /// 투사체 발사
    /// </summary>
    void FireProjectile()
    {
        if (projectilePrefab == null || player == null) return;

        // 투사체 생성
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        // 투사체 설정
        Projectile projectileScript = projectile.GetComponent<Projectile>();
        if (projectileScript == null)
        {
            projectileScript = projectile.AddComponent<Projectile>();
        }

        // 플레이어 방향으로 발사
        Vector3 direction = (player.position - firePoint.position).normalized;
        direction.y = 0;

        projectileScript.Initialize(direction, projectileSpeed, projectileDamage, projectileLifetime);

        Debug.Log($"[Enemy_Range_Attack] 투사체 발사: {projectile.name}");
    }

    #region IEnemyAttack 구현

    /// <summary>
    /// 현재 공격 중인지 확인
    /// FSM에서 상태 전환 조건 체크에 사용
    /// </summary>
    public bool IsAttacking()
    {
        return isAttacking;
    }

    /// <summary>
    /// 공격 시작 (FSM에서 호출)
    /// </summary>
    public void StartAttack()
    {
        if (!isAttacking && gameObject.activeInHierarchy)
        {
            StartCoroutine(RangeAttackRoutine());
        }
    }

    /// <summary>
    /// 공격 중단/취소
    /// </summary>
    public void StopAttack()
    {
        if (isAttacking)
        {
            StopAllCoroutines();
            isAttacking = false;
            Debug.Log($"[Enemy_Range_Attack] 원거리 공격 중단");
        }
    }

    /// <summary>
    /// 이 공격의 사거리 반환
    /// </summary>
    public float GetAttackRange()
    {
        return attackRange;
    }

    /// <summary>
    /// 공격 쿨타임 반환
    /// </summary>
    public float GetCooldownTime()
    {
        return attackDelay;
    }

    #endregion

    /// <summary>
    /// 디버그 기즈모 - 공격 범위 시각화
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 공격 범위
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 발사 방향
        if (player != null && Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Vector3 direction = (player.position - transform.position).normalized;
            direction.y = 0;
            Gizmos.DrawRay(transform.position, direction * attackRange);
        }
    }
}
