// Enemy_Golem_Attack.cs

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 골렘 공격 시스템 (근접 + Roar 광역 틱데미지)
/// 
/// 근접: 내려찍기/휘두르기 랜덤 (텔레그래프 없음)
/// Roar: 피격 시 발동 (10초 쿨타임)
///   - Walk + 바닥 원형 Fill (골렘 따라다님)
///   - Fill 완료 → 상체 Roar + 하체 느린 Walk
///   - 범위 내 틱데미지
///   - Roar 중 근접 공격 불가
/// 
/// Animator Layer 구조:
///   Base Layer (전신): Idle, Walk, Smash, Swipe, Die
///   Upper Body Layer (상체): Roar (Avatar Mask 필요)
/// </summary>
public class Enemy_Golem_Attack : MonoBehaviour, IEnemyAttack
{
    [Header("근접 공격 설정")]
    [SerializeField, Tooltip("근접 공격 사거리")]
    private float meleeRange = 3.5f;

    [SerializeField, Tooltip("근접 공격 쿨타임")]
    private float meleeCooldown = 2.5f;

    [SerializeField, Tooltip("내려찍기 데미지")]
    private int smashDamage = 25;

    [SerializeField, Tooltip("휘두르기 데미지")]
    private int swipeDamage = 18;

    [SerializeField, Tooltip("근접 판정 범위")]
    private float meleeHitRadius = 2.5f;

    [SerializeField, Tooltip("플레이어 레이어")]
    private LayerMask playerLayer;

    [Header("근접 애니메이션 타이밍")]
    [SerializeField, Tooltip("내려찍기 판정 시점 (초)")]
    private float smashHitTime = 0.6f;

    [SerializeField, Tooltip("내려찍기 전체 길이 (초)")]
    private float smashLength = 1.2f;

    [SerializeField, Tooltip("휘두르기 판정 시점 (초)")]
    private float swipeHitTime = 0.4f;

    [SerializeField, Tooltip("휘두르기 전체 길이 (초)")]
    private float swipeLength = 0.9f;

    [Header("Roar 설정")]
    [SerializeField, Tooltip("Roar 쿨타임")]
    private float roarCooldown = 10f;

    [SerializeField, Tooltip("바닥 원형 Fill 시간 (준비 시간)")]
    private float roarChargeDuration = 2.0f;

    [SerializeField, Tooltip("Roar 지속 시간")]
    private float roarDuration = 3.0f;

    [SerializeField, Tooltip("Roar 데미지 범위")]
    private float roarRadius = 6f;

    [SerializeField, Tooltip("틱데미지 간격 (초)")]
    private float tickInterval = 0.5f;

    [SerializeField, Tooltip("틱당 데미지")]
    private int tickDamage = 8;

    [SerializeField, Tooltip("Roar 중 이동속도 배율 (원래의 몇 %)")]
    private float roarMoveSpeedMultiplier = 0.4f;

    [Header("Roar 이펙트")]
    [SerializeField, Tooltip("바닥 원형 Fill 프리팹 (골렘 발밑에 생성)")]
    private GameObject roarCirclePrefab;

    [SerializeField, Tooltip("Roar 발동 시 VFX 프리팹")]
    private GameObject roarVFXPrefab;

    [Header("Animator Layer")]
    [SerializeField, Tooltip("Upper Body Layer 인덱스 (보통 1)")]
    private int upperBodyLayerIndex = 1;

    // 컴포넌트 참조
    private Transform player;
    private NavMeshAgent nav;
    private Animator anim;
    private EnemyHealth health;

    // 상태
    private bool isAttacking = false;
    private bool isMeleeAttacking = false;
    private bool isRoaring = false;
    private bool roarPending = false; // 근접 공격 끝난 후 Roar 예약

    // Roar 쿨타임
    private bool isRoarReady = true;
    private float roarCooldownTimer = 0f;
    private bool hasEverBeenHit = false; // 최초 피격 감지용

    // 체력 추적 (피격 감지용)
    private int lastKnownHP;

    // Roar 원형 인스턴스
    private GameObject activeRoarCircle;

    // 원래 NavMesh 속도 저장
    private float originalNavSpeed;

    private void Awake()
    {
        nav = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        health = GetComponent<EnemyHealth>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    private void Start()
    {
        // 체력 초기값 기록
        if (health != null)
            lastKnownHP = health.curHealth;

        // NavMesh 원래 속도 저장
        if (nav != null)
            originalNavSpeed = nav.speed;

        // Upper Body Layer 초기 Weight = 0 (Roar 비활성)
        if (anim != null)
            anim.SetLayerWeight(upperBodyLayerIndex, 0f);
    }

    private void Update()
    {
        // 피격 감지 (체력 감소 체크)
        CheckDamageTaken();

        // Roar 쿨타임 관리
        UpdateRoarCooldown();
    }

    #region IEnemyAttack 구현

    public void StartAttack()
    {
        if (!isAttacking && !isRoaring && gameObject.activeInHierarchy)
        {
            StartCoroutine(MeleeAttackRoutine());
        }
    }

    public void StopAttack()
    {
        if (isMeleeAttacking)
        {
            StopAllCoroutines();
            isMeleeAttacking = false;
            isAttacking = false;

            // Roar 예약이 있었으면 발동
            if (roarPending)
            {
                roarPending = false;
                StartCoroutine(RoarSequence());
            }
        }
    }

    public bool IsAttacking() => isAttacking || isRoaring;
    public float GetAttackRange() => meleeRange;
    public float GetCooldownTime() => meleeCooldown;

    #endregion

    #region 피격 감지

    /// <summary>
    /// 매 프레임 체력 변화를 감시하여 피격 감지
    /// 피격 시 Roar 조건 충족하면 예약 or 즉시 발동
    /// </summary>
    private void CheckDamageTaken()
    {
        if (health == null) return;

        int currentHP = health.curHealth;

        // 체력이 줄었으면 피격 발생
        if (currentHP < lastKnownHP)
        {
            hasEverBeenHit = true;
            OnDamageTaken();
        }

        lastKnownHP = currentHP;
    }

    /// <summary>
    /// 피격 시 Roar 발동 판단
    /// </summary>
    private void OnDamageTaken()
    {
        // Roar 쿨타임 중이거나 이미 Roar 중이면 무시
        if (!isRoarReady || isRoaring) return;

        if (isMeleeAttacking)
        {
            // 근접 공격 중이면 끝난 후 Roar 예약
            roarPending = true;
            Debug.Log("[Golem] Roar 예약 (근접 공격 완료 후 발동)");
        }
        else
        {
            // 즉시 Roar 발동
            StartCoroutine(RoarSequence());
        }
    }

    #endregion

    #region Roar 쿨타임

    /// <summary>
    /// Roar 쿨타임 타이머 관리
    /// </summary>
    private void UpdateRoarCooldown()
    {
        if (!isRoarReady && !isRoaring)
        {
            roarCooldownTimer += Time.deltaTime;
            if (roarCooldownTimer >= roarCooldown)
            {
                isRoarReady = true;
                roarCooldownTimer = 0f;
            }
        }
    }

    #endregion

    #region 근접 공격

    /// <summary>
    /// 근접 공격 — 내려찍기/휘두르기 랜덤 선택
    /// </summary>
    private IEnumerator MeleeAttackRoutine()
    {
        isAttacking = true;
        isMeleeAttacking = true;

        // 플레이어 방향 조준
        LookAtPlayer();

        // 랜덤 공격 선택
        bool useSmash = Random.value > 0.5f;

        if (useSmash)
            yield return StartCoroutine(SmashAttack());
        else
            yield return StartCoroutine(SwipeAttack());

        isMeleeAttacking = false;
        isAttacking = false;

        // Roar 예약이 있으면 즉시 발동
        if (roarPending)
        {
            roarPending = false;
            StartCoroutine(RoarSequence());
        }
    }

    /// <summary>
    /// 내려찍기 — 느리지만 고데미지, 전방 범위
    /// </summary>
    private IEnumerator SmashAttack()
    {
        if (anim != null)
            anim.SetTrigger("doSmash");

        Debug.Log("[Golem] 내려찍기!");

        // 판정 시점까지 대기
        yield return new WaitForSeconds(smashHitTime);

        // 데미지 판정 (전방 범위)
        DealMeleeDamage(smashDamage);

        // 나머지 애니메이션 대기
        yield return new WaitForSeconds(smashLength - smashHitTime);
    }

    /// <summary>
    /// 휘두르기 — 빠르지만 낮은 데미지, 전방 범위
    /// </summary>
    private IEnumerator SwipeAttack()
    {
        if (anim != null)
            anim.SetTrigger("doSwipe");

        Debug.Log("[Golem] 휘두르기!");

        // 판정 시점까지 대기
        yield return new WaitForSeconds(swipeHitTime);

        // 데미지 판정
        DealMeleeDamage(swipeDamage);

        // 나머지 애니메이션 대기
        yield return new WaitForSeconds(swipeLength - swipeHitTime);
    }

    /// <summary>
    /// 근접 데미지 판정 — 전방 범위
    /// </summary>
    private void DealMeleeDamage(int damage)
    {
        Vector3 hitCenter = transform.position + transform.forward * 1.5f;
        Collider[] hits = Physics.OverlapSphere(hitCenter, meleeHitRadius, playerLayer);

        foreach (var hit in hits)
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(damage);
                Debug.Log($"[Golem] 근접 적중! 데미지: {damage}");
                return;
            }
        }
    }

    #endregion

    #region Roar 시퀀스

    /// <summary>
    /// Roar 전체 시퀀스
    /// Walk + 바닥 원형 Fill → Roar 발동 (틱데미지) → 종료
    /// </summary>
    private IEnumerator RoarSequence()
    {
        isRoaring = true;
        isAttacking = true;
        isRoarReady = false;

        Debug.Log("[Golem] === Roar 시퀀스 시작 ===");

        // Phase 1: 준비 — Walk + 바닥 원형 Fill
        yield return StartCoroutine(RoarChargePhase());

        // Phase 2: 발동 — Roar + 느린 Walk + 틱데미지
        yield return StartCoroutine(RoarActivePhase());

        // Phase 3: 종료
        EndRoar();

        Debug.Log("[Golem] === Roar 시퀀스 종료 ===");
    }

    /// <summary>
    /// Roar 준비 — 걸으면서 바닥 원형 Fill
    /// NavMesh는 켜진 상태로 추적하면서 원형이 차오름
    /// </summary>
    private IEnumerator RoarChargePhase()
    {
        Debug.Log("[Golem] Roar 준비 시작 (Walk + Fill)");

        // Walk 애니메이션 유지 (추적 상태 그대로)
        if (anim != null)
            anim.SetBool("Walk", true);

        // 바닥 원형 생성 (골렘 자식으로 — 따라다님)
        SpawnRoarCircle();

        // Fill 연출
        float elapsed = 0f;
        while (elapsed < roarChargeDuration)
        {
            elapsed += Time.deltaTime;
            float fillRatio = elapsed / roarChargeDuration;

            // 원형 Scale로 Fill 표현 (0 → 최종 크기)
            UpdateRoarCircleScale(fillRatio);

            // 이동 중에도 플레이어를 향해 추적
            if (nav != null && nav.enabled && player != null)
                nav.SetDestination(player.position);

            yield return null;
        }

        // Fill 완료 — 원형 제거 (Roar VFX로 교체)
        DestroyRoarCircle();
    }

    /// <summary>
    /// Roar 발동 — 상체 Roar + 하체 느린 Walk + 틱데미지
    /// </summary>
    private IEnumerator RoarActivePhase()
    {
        Debug.Log("[Golem] Roar 발동!");

        // Upper Body Layer 활성화 → Roar 모션 재생
        if (anim != null)
        {
            anim.SetLayerWeight(upperBodyLayerIndex, 1f);
            anim.SetTrigger("doRoar");
            anim.SetBool("Walk", true);
        }

        // 이동 속도 감소 (느리게 걸으면서 Roar)
        if (nav != null)
            nav.speed = originalNavSpeed * roarMoveSpeedMultiplier;

        // Roar 발동 VFX
        if (roarVFXPrefab != null)
        {
            GameObject vfx = Instantiate(roarVFXPrefab, transform.position, Quaternion.identity, transform);
            Destroy(vfx, roarDuration + 1f);
        }

        // 틱데미지 루프
        float elapsed = 0f;
        float lastTickTime = 0f;

        while (elapsed < roarDuration)
        {
            elapsed += Time.deltaTime;

            // 플레이어 추적 (느리게)
            if (nav != null && nav.enabled && player != null)
                nav.SetDestination(player.position);

            // 틱데미지
            if (elapsed - lastTickTime >= tickInterval)
            {
                DealRoarTickDamage();
                lastTickTime = elapsed;
            }

            yield return null;
        }
    }

    /// <summary>
    /// Roar 범위 내 플레이어에게 틱데미지
    /// </summary>
    private void DealRoarTickDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, roarRadius, playerLayer);

        foreach (var hit in hits)
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(tickDamage);
                Debug.Log($"[Golem] Roar 틱데미지: {tickDamage}");
                return;
            }
        }
    }

    /// <summary>
    /// Roar 종료 — 속도 복구 + Layer Weight 복구
    /// </summary>
    private void EndRoar()
    {
        isRoaring = false;
        isAttacking = false;
        roarCooldownTimer = 0f;

        // Upper Body Layer 비활성화
        if (anim != null)
        {
            anim.SetLayerWeight(upperBodyLayerIndex, 0f);
            anim.SetBool("Walk", false);
        }

        // 이동 속도 복구
        if (nav != null)
            nav.speed = originalNavSpeed;

        // 남은 원형 정리
        DestroyRoarCircle();
    }

    #endregion

    #region Roar 바닥 원형 관리

    /// <summary>
    /// 바닥 원형 생성 — 골렘의 자식으로 (따라다님)
    /// </summary>
    private void SpawnRoarCircle()
    {
        if (roarCirclePrefab == null) return;

        // 골렘 발밑에 생성 (Y는 바닥 + 약간 위)
        Vector3 spawnPos = new Vector3(0f, 0.05f, 0f); // 로컬 좌표
        activeRoarCircle = Instantiate(roarCirclePrefab, transform);
        activeRoarCircle.transform.localPosition = spawnPos;
        activeRoarCircle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        activeRoarCircle.transform.localScale = Vector3.zero; // 0에서 시작
    }

    /// <summary>
    /// 바닥 원형 Scale 업데이트 (Fill 연출)
    /// </summary>
    private void UpdateRoarCircleScale(float fillRatio)
    {
        if (activeRoarCircle == null) return;

        // 최종 크기 = roarRadius * 2 (지름)
        float targetSize = roarRadius * 2f;
        float currentSize = targetSize * fillRatio;
        activeRoarCircle.transform.localScale = Vector3.one * currentSize;
    }

    /// <summary>
    /// 바닥 원형 제거
    /// </summary>
    private void DestroyRoarCircle()
    {
        if (activeRoarCircle != null)
        {
            Destroy(activeRoarCircle);
            activeRoarCircle = null;
        }
    }

    #endregion

    #region 유틸리티

    /// <summary>
    /// 플레이어 방향 즉시 회전
    /// </summary>
    private void LookAtPlayer()
    {
        if (player == null) return;

        Vector3 dir = (player.position - transform.position);
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    #endregion

    #region 디버그

    private void OnDrawGizmosSelected()
    {
        // 근접 범위
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward * 1.5f, meleeHitRadius);

        // Roar 범위
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, roarRadius);

        // 공격 사거리
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
    }

    #endregion
}