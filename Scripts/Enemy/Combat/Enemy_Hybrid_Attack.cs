using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 혼합형 공격 - 거리에 따라 근거리/원거리 전환
/// EnemySO의 hybridSwitchDistance 기준으로 공격 방식 결정
/// </summary>
public class Enemy_Hybrid_Attack : MonoBehaviour, IEnemyAttack
{
    [Header("투사체 설정")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    [Header("근거리 공격 설정")]
    [SerializeField] private Transform meleeHitbox;
    [SerializeField] private float meleeHitboxRadius = 1.5f;
    [SerializeField] private LayerMask playerLayer;

    // 컴포넌트 참조
    private Enemy enemy;
    private EnemySO data;
    private Transform player;
    private Animator anim;
    private NavMeshAgent nav;

    // 상태
    private bool isAttacking = false;
    private bool isRangedAttack = false;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        anim = GetComponentInChildren<Animator>();
        nav = GetComponent<NavMeshAgent>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        if (firePoint == null)
            firePoint = transform;

        if (meleeHitbox == null)
            meleeHitbox = transform;
    }

    private void Start()
    {
        if (enemy != null && enemy.Data != null)
            data = enemy.Data;
    }

    /// <summary>
    /// 현재 거리에 따른 공격 타입 결정
    /// </summary>
    private bool ShouldUseRangedAttack()
    {
        if (player == null || data == null) return false;

        float distance = Vector3.Distance(transform.position, player.position);
        return distance > data.hybridSwitchDistance;
    }

    /// <summary>
    /// 공격 루틴 - 거리에 따라 근거리/원거리 분기
    /// </summary>
    private IEnumerator HybridAttackRoutine()
    {
        isAttacking = true;
        isRangedAttack = ShouldUseRangedAttack();

        // 공격 전 플레이어 방향으로 회전
        LookAtPlayerInstant();

        if (isRangedAttack)
        {
            yield return StartCoroutine(RangedAttackRoutine());
        }
        else
        {
            yield return StartCoroutine(MeleeAttackRoutine());
        }

        isAttacking = false;
    }

    /// <summary>
    /// 근거리 공격 루틴
    /// </summary>
    private IEnumerator MeleeAttackRoutine()
    {
        Debug.Log($"[Hybrid] 근거리 공격 시작");

        // 공격 준비 시간
        yield return new WaitForSeconds(data.forceTransitionTime);

        // 취소 체크
        if (!isAttacking) yield break;

        // 데미지 판정
        Collider[] hits = Physics.OverlapSphere(meleeHitbox.position, meleeHitboxRadius, playerLayer);
        foreach (var hit in hits)
        {
            PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(data.meleeDamage);
                Debug.Log($"[Hybrid] 근거리 데미지: {data.meleeDamage}");
            }
        }

        // 공격 후 딜레이
        yield return new WaitForSeconds(data.meleeAttackCooldown - data.forceTransitionTime);
    }

    /// <summary>
    /// 원거리 공격 루틴
    /// </summary>
    private IEnumerator RangedAttackRoutine()
    {
        Debug.Log($"[Hybrid] 원거리 공격 시작");

        // 조준 시간
        yield return new WaitForSeconds(1.0f);

        // 취소 체크
        if (!isAttacking) yield break;

        // 투사체 발사
        FireProjectile();

        // 공격 후 딜레이
        yield return new WaitForSeconds(data.rangedAttackCooldown - 1.0f);
    }

    /// <summary>
    /// 투사체 발사
    /// </summary>
    private void FireProjectile()
    {
        if (projectilePrefab == null || player == null) return;

        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        Projectile projectileScript = projectile.GetComponent<Projectile>();
        if (projectileScript == null)
            projectileScript = projectile.AddComponent<Projectile>();

        Vector3 direction = (player.position - firePoint.position).normalized;
        direction.y = 0;

        float speed = data != null ? data.projectileSpeed : 10f;
        int damage = data != null ? data.rangedDamage : 8;

        projectileScript.Initialize(direction, speed, damage, 5f);

        Debug.Log($"[Hybrid] 투사체 발사");
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
            transform.rotation = Quaternion.LookRotation(direction.normalized);
        }
    }

    #region IEnemyAttack 구현

    public bool IsAttacking()
    {
        return isAttacking;
    }

    public void StartAttack()
    {
        if (!isAttacking && gameObject.activeInHierarchy)
        {
            StartCoroutine(HybridAttackRoutine());
        }
    }

    public void StopAttack()
    {
        if (isAttacking)
        {
            StopAllCoroutines();
            isAttacking = false;
            Debug.Log($"[Hybrid] 공격 중단");
        }
    }

    public float GetAttackRange()
    {
        if (data == null) return 2f;

        // 현재 거리에 따라 적절한 사거리 반환
        if (ShouldUseRangedAttack())
            return data.rangedAttackRange;
        else
            return data.meleeAttackRange;
    }

    public float GetCooldownTime()
    {
        if (data == null) return 1.5f;

        if (isRangedAttack)
            return data.rangedAttackCooldown;
        else
            return data.meleeAttackCooldown;
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        // 근거리 공격 범위
        Gizmos.color = Color.red;
        if (meleeHitbox != null)
            Gizmos.DrawWireSphere(meleeHitbox.position, meleeHitboxRadius);

        // 전환 거리 표시
        if (enemy != null && enemy.Data != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, enemy.Data.hybridSwitchDistance);
        }
    }
}
