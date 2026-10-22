using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Enemy_Charge_Attack : MonoBehaviour, IEnemyAttack
{
    [Header("Attack Settings")]
    public float attackRange = 4f;
    public float attackDelay = 2.0f; // 돌진 후 다음 행동까지 쿨타임
    public int attackDamage = 15;

    [Header("Charge Move")]
    public float chargeDistance = 6f;   // 돌진 거리
    public float chargeDuration = 0.4f; // 돌진하는데 걸리는 시간 (빠를수록 타격감 좋음)
    public Transform moveRoot;

    // 컴포넌트 참조
    private Enemy enemy;
    private Transform player;
    private Animator anim;
    private NavMeshAgent nav; // NavMesh 제어를 위해 추가

    private bool isAttacking = false;
    private bool hasHit = false;

    private void Awake()
    {
        player = GameObject.FindWithTag("Player")?.transform;
        enemy = GetComponent<Enemy>();
        anim = GetComponentInChildren<Animator>();
        nav = GetComponent<NavMeshAgent>();

        if (moveRoot == null) moveRoot = transform;
    }

    // Update 로직 제거 (FSM이 제어함)

    #region IEnemyAttack 구현

    public void StartAttack()
    {
        if (!isAttacking)
        {
            StartCoroutine(ChargeAttackRoutine());
        }
    }

    public void StopAttack()
    {
        if (isAttacking)
        {
            StopAllCoroutines();

            // 공격 중단 시 NavMesh가 꺼져있다면 다시 켜줘야 함 (안전장치)
            if (nav != null && !nav.enabled)
            {
                nav.enabled = true;
            }

            isAttacking = false;
            hasHit = false;
        }
    }

    public bool IsAttacking() => isAttacking;
    public float GetAttackRange() => attackRange;
    public float GetCooldownTime() => attackDelay;

    #endregion

    private IEnumerator ChargeAttackRoutine()
    {
        isAttacking = true;
        hasHit = false;

        // 1. 플레이어 방향 조준
        LookAtPlayerInstant();

        // 2. 공격 시작 (전조 동작)
        // 만약 '기 모으는' 애니메이션이 있다면 여기서 Trigger
        // anim.SetTrigger("ChargeWindup"); 
        // yield return new WaitForSeconds(0.5f); // 전조 시간
        // *현재는 즉발 돌진으로 구현*

        anim.SetTrigger("doChargeAttack"); // 돌진 애니메이션 시작

        // 3. [핵심] NavMesh 끄기 (수동 이동을 위해 필수!)
        if (nav != null) nav.enabled = false;

        // 4. 돌진 목표 지점 계산
        Vector3 startPos = moveRoot.position;
        Vector3 dir = transform.forward;
        Vector3 targetPos = startPos + (dir * chargeDistance);

        // 5. 실제 이동 (Lerp)
        float elapsedTime = 0f;
        while (elapsedTime < chargeDuration)
        {
            // 이동 중 피격 판정 (돌진하는 몸 자체가 히트박스)
            if (!hasHit) CheckCollision();

            // 선형 보간으로 이동
            moveRoot.position = Vector3.Lerp(startPos, targetPos, elapsedTime / chargeDuration);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 이동 끝, 위치 확정
        moveRoot.position = targetPos;
        if (!hasHit) CheckCollision(); // 마지막 체크

        // 6. [핵심] NavMesh 다시 켜기 & 위치 동기화
        if (nav != null)
        {
            // Warp를 해줘야 NavMesh가 "어? 갑자기 여기로 왔네?" 하고 인식함
            // 그냥 enabled = true만 하면 이전 위치로 튕길 수 있음
            nav.enabled = true;
            nav.Warp(moveRoot.position);
        }

        // 7. 후딜레이 (애니메이션 끝날 때까지 대기하거나 쿨타임 적용)
        // 여기서는 상태 해제만 하고, 실제 쿨타임은 FSM이 GetCooldownTime()으로 처리
        isAttacking = false;
    }

    private void CheckCollision()
    {
        if (player == null) return;

        // 간단한 거리 기반 판정 (BoxCollider Trigger 등을 써도 됨)
        // 돌진 중 플레이어와 닿았는지 확인
        float dist = Vector3.Distance(transform.position, player.position);

        // 공격 범위보다 약간 좁게 잡아야 '몸통 박치기' 느낌이 남 (1.5f 정도)
        if (dist <= 1.5f)
        {
            hasHit = true;
            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(attackDamage);
                Debug.Log("돌진 타격 성공!");

                // 타격감: 플레이어 넉백 같은거 추가하면 좋음
            }
        }
    }

    private void LookAtPlayerInstant()
    {
        if (player == null) return;
        Vector3 dir = (player.position - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }
}
