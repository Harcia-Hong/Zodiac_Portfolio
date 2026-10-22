using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 원거리 공격 시스템 — 활(Ranged) 좌클릭 단발 사격.
/// 
/// 동작:
///   좌클릭 1회 = 1발 사격 (콤보 없음).
///   회전 → 트리거 → 시위 당기기(fireDelay) → 발사 → 후딜(attackDuration - fireDelay) → 종료.
/// 
/// 화살 자원 연동:
///   StartRangedAttack 시점에 StanceGaugeSystem.ConsumeArrows(arrowsPerShot) 시도.
///   화살 부족 시 사격 차단 (모션도 안 나감 — 빈 활시위 표현은 추후 폴리싱).
///   화살 0발 + Tab → StanceGaugeSystem에서 자동으로 태그 공격 발동 가능 상태.
/// 
/// 발사 모드:
///   - ArrowBeam 연결 시 → Hitscan 빔 (즉시 타격)
///   - 비어있으면 → LinearProjectile 화살 (날아가서 타격)
/// 
/// 검 모드 차단:
///   이 컴포넌트는 활 캐릭터 GameObject에 부착. RangedCharacter.Activate/Deactivate에서 enabled 토글되어야 함.
///   (PlayerGroundAttack은 외부 호출 의존이지만 이 클래스는 Update에서 직접 입력 받음 — 활성 상태 제어 필수.)
/// </summary>
public class PlayerArrowAttack : MonoBehaviour
{
    // ============================================================
    // Serialized Fields - 사격 설정
    // ============================================================
    [Header("사격 설정")]
    [SerializeField, Tooltip("발사 애니메이션 트리거 이름")]
    private string attackTrigger = "Attack1";

    [SerializeField, Tooltip("동작 전체 지속 시간 (초). 발사 모션 + 후딜 합산.")]
    private float attackDuration = 0.89f;

    [SerializeField, Tooltip("시위 당기기 시간 (선딜). 이 시간 후 실제 발사.")]
    private float fireDelay = 0.28f;

    // ============================================================
    // Serialized Fields - 발사체 설정
    // ============================================================
    [Header("발사체 설정")]
    [SerializeField, Tooltip("화살 프리팹 (LinearProjectile 부착). 빔 모드에서는 무시됨")]
    private GameObject arrowPrefab;

    [SerializeField, Tooltip("발사 위치 (활 끝). 비우면 캐릭터 + 1m")]
    private Transform firePoint;

    [SerializeField, Tooltip("화살 비행 속도 (빔 모드에서는 무시됨)")]
    private float arrowSpeed = 25f;

    [SerializeField, Tooltip("최대 사거리 — 빔/화살 공통")]
    private float arrowRange = 15f;

    [SerializeField, Tooltip("적중 시 히트 이펙트 — 빔/화살 공통")]
    private HitEffectType arrowHitEffect = HitEffectType.Slashing_Normal;

    // ============================================================
    // Serialized Fields - 화살 자원
    // ============================================================
    [Header("화살 자원")]
    [SerializeField, Tooltip("1회 좌클릭 사격 시 소모할 화살 수. 기본 1.")]
    private int arrowsPerShot = 1;

    // ============================================================
    // Serialized Fields - 쿨다운
    // ============================================================
    [Header("쿨다운")]
    [SerializeField, Tooltip("사격 종료 후 다음 사격까지 쿨다운 (초)")]
    private float attackCooldown = 0.1f;

    // ============================================================
    // References
    // ============================================================
    [Header("References")]
    [SerializeField] private Weapon weapon;

    [Tooltip("RangedCharacter — 태그 윈도우 동기화용. 비워두면 자동 탐색")]
    [SerializeField] private RangedCharacter rangedCharacter;

    [Header("빔 발사 (선택) — 연결되면 화살 대신 빔으로 발사")]
    [SerializeField, Tooltip("ArrowBeam 컴포넌트. 보통 firePoint에 붙어있음. 비우면 자동 탐색")]
    private ArrowBeam arrowBeam;

    [Header("Animator (필수 — 활 캐릭터 GameObject의 Animator를 직접 지정)")]
    [SerializeField, Tooltip("Archer GameObject의 Animator를 직접 드래그. 비워두면 Player 본체 Animator가 잘못 잡힘.")]
    private Animator anim;

    private PlayerController playerController;
    private PlayerLockOn lockOn;

    // ============================================================
    // Combat State
    // ============================================================
    private Coroutine attackCoroutine;

    /// <summary>현재 사격 모션 진행 중인지</summary>
    public bool isRangedAttacking { get; private set; } = false;

    /// <summary>다음 사격 시작 가능한지 (쿨다운 끝났는지)</summary>
    public bool isRangedAttackReady { get; private set; } = true;

    /// <summary>호환성 — RangedCharacter.IsBusy 위임용</summary>
    public bool isAttacking => isRangedAttacking;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    private void Awake()
    {
        InitializeComponents();
    }

    /// <summary>좌클릭 입력 — 활 모드 활성 시에만 (enabled=true) Update 돔</summary>
    private void Update()
    {
        HandleInput();
    }

    private void InitializeComponents()
    {
        playerController = GetComponent<PlayerController>();
        lockOn = GetComponent<PlayerLockOn>();

        if (weapon == null)
            weapon = GetComponentInChildren<Weapon>();

        if (rangedCharacter == null)
            rangedCharacter = GetComponent<RangedCharacter>();

        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        // ArrowBeam 자동 탐색 — firePoint에 붙어있다면 자동 연결
        if (arrowBeam == null && firePoint != null)
            arrowBeam = firePoint.GetComponent<ArrowBeam>();

        if (anim == null)
            Debug.LogError("[PlayerArrowAttack] Animator를 찾을 수 없습니다!");

        // 빔도 화살도 없으면 발사 불가 — 둘 중 하나는 있어야 함
        if (arrowBeam == null && arrowPrefab == null)
            Debug.LogError("[PlayerArrowAttack] ArrowBeam과 arrowPrefab 둘 다 없습니다! 하나는 설정 필요");
    }

    // ============================================================
    // Input Handling
    // ============================================================

    /// <summary>좌클릭 = 단발 사격 (콤보 없음). 사격 중 입력은 무시.</summary>
    private void HandleInput()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (isRangedAttacking) return;          // 사격 모션 중에는 추가 입력 무시
        if (!CanRangedAttack()) return;

        StartRangedAttack();
    }

    // ============================================================
    // Public Interface
    // ============================================================

    /// <summary>
    /// 외부 호출 — 단발 사격 시작.
    /// 흐름:
    ///   1. 상태 체크 (CanRangedAttack)
    ///   2. 화살 자원 차감 시도 (ConsumeArrows) → 실패면 사격 차단
    ///   3. FireRoutine 코루틴 실행
    /// </summary>
    public void StartRangedAttack()
    {
        if (!CanRangedAttack())
        {
            Debug.Log("[PlayerArrowAttack] 사격 불가 상태");
            return;
        }

        // 화살 자원 체크 + 차감 — 부족하면 사격 자체를 차단 (모션도 안 나감)
        // 화살 0발 + Tab → 검 전환 + 태그기(검 회오리) 발동 유도
        if (StanceGaugeSystem.Instance == null
            || !StanceGaugeSystem.Instance.ConsumeArrows(arrowsPerShot))
        {
            Debug.Log("[PlayerArrowAttack] 화살 부족 — 사격 차단 (Tab으로 검 전환 + 태그기 사용 가능)");
            return;
        }

        attackCoroutine = StartCoroutine(FireRoutine());
    }

    /// <summary>사격 시작 가능 상태 여부 (자원 체크는 StartRangedAttack에서 분리 처리)</summary>
    public bool CanRangedAttack()
    {
        if (playerController == null) return false;

        return !isRangedAttacking
            && isRangedAttackReady
            && !playerController.isDodging
            && !playerController.isSkillCasting;
    }

    /// <summary>사격 강제 취소 — 어택캔슬/태그 전환 시 호출</summary>
    public void CancelRangedAttack()
    {
        if (!isRangedAttacking) return;
        EndRangedAttack();
    }

    // ============================================================
    // Fire Routine — 단발 사격
    // ============================================================

    /// <summary>
    /// 단발 사격 메인 루틴.
    /// 흐름: 회전 → 트리거 → 시위 당기기(fireDelay) → 발사 → 후딜 → 종료.
    /// 자원 차감은 이미 StartRangedAttack에서 완료된 상태로 진입.
    /// </summary>
    private IEnumerator FireRoutine()
    {
        isRangedAttacking = true;
        isRangedAttackReady = false;

        // 공격 속도 보정
        float attackSpeed = 1.0f;
        if (PlayerStats.Instance != null)
            attackSpeed = PlayerStats.Instance.AttackSpeed;
        if (anim != null) anim.speed = attackSpeed;

        // 사격 시작 — 태그 윈도우 닫힘 (RangedCharacter.IsBusy=true 의미)
        if (rangedCharacter != null) rangedCharacter.OnTagWindowClose();

        // 1) 회전 + 애니메이션 트리거
        RotateToTarget();
        if (anim != null) anim.SetTrigger(attackTrigger);

        // 2) 시위 당기기 (선딜) 대기
        yield return new WaitForSeconds(fireDelay / attackSpeed);

        // 3) 발사 (빔 또는 화살 — FireArrow 내부에서 자동 분기)
        FireArrow();

        // 4) 후딜 — 발사 직후 ~ 클립 끝
        //    이 윈도우 동안 태그 가능 (윈도우 열림) + 이동 입력 시 즉시 캔슬
        if (rangedCharacter != null) rangedCharacter.OnTagWindowOpen();

        float postFireDuration = (attackDuration - fireDelay) / attackSpeed;
        float elapsed = 0f;

        while (elapsed < postFireDuration)
        {
            elapsed += Time.deltaTime;

            // 이동 입력 시 즉시 캔슬 (빠른 이동 복귀)
            if (HasMovementInput())
            {
                break;
            }

            yield return null;
        }

        // 종료
        EndRangedAttack();
    }

    /// <summary>
    /// 실제 발사 — ArrowBeam 우선, 없으면 LinearProjectile 화살로 폴백.
    /// 데미지는 weapon.GetDamage(true)로 계산 (두 모드 공통).
    /// </summary>
    private void FireArrow()
    {
        Vector3 fireDir = GetFireDirection();
        int damage = weapon != null ? weapon.GetDamage(true) : 10;

        // ---- 빔 모드 (Hitscan) ----
        if (arrowBeam != null)
        {
            arrowBeam.Fire(fireDir, damage, arrowRange, arrowHitEffect);
            return;
        }

        // ---- 화살 모드 (Projectile) ----
        if (arrowPrefab == null) return;

        Vector3 spawnPos = firePoint != null
            ? firePoint.position
            : transform.position + Vector3.up * 1f;

        GameObject arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.LookRotation(fireDir));

        LinearProjectile projectile = arrow.GetComponent<LinearProjectile>();
        if (projectile != null)
        {
            projectile.Initialize(arrowSpeed, damage, arrowRange, arrowHitEffect);
        }
        else
        {
            Debug.LogWarning("[PlayerArrowAttack] arrowPrefab에 LinearProjectile 없음");
        }
    }

    // ============================================================
    // End / Cooldown
    // ============================================================

    /// <summary>사격 종료 — 모든 상태 초기화 + 트리거 잔류 방지</summary>
    private void EndRangedAttack()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        // 상태 초기화
        isRangedAttacking = false;

        // 애니메이션 속도 복원
        if (anim != null) anim.speed = 1.0f;

        // 트리거 잔류 방지
        if (anim != null) anim.ResetTrigger(attackTrigger);

        // 태그 윈도우 닫힘 보장
        if (rangedCharacter != null)
            rangedCharacter.OnTagWindowClose();

        // 공격 종료 — 잔여 회전 잠금 시작 (마우스 방향 0.4초 유지 후 이동 방향 회전)
        if (playerController != null)
            playerController.StartPostAttackTurnLock();

        // 쿨다운 시작
        StartCoroutine(AttackCooldownRoutine());
    }

    private IEnumerator AttackCooldownRoutine()
    {
        isRangedAttackReady = false;
        yield return new WaitForSeconds(attackCooldown);
        isRangedAttackReady = true;
    }

    // ============================================================
    // Direction & Rotation
    // ============================================================

    private Vector3 GetFireDirection()
    {
        if (lockOn != null && lockOn.isLockOn && lockOn.currentTarget != null)
        {
            Vector3 toTarget = (lockOn.currentTarget.position - transform.position).normalized;
            toTarget.y = 0;
            return toTarget;
        }

        if (playerController != null)
            return playerController.GetMouseDirection();

        return transform.forward;
    }

    private void RotateToTarget()
    {
        Vector3 dir = GetFireDirection();
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    /// <summary>이동 입력(WASD)이 있는지 — 후딜 캔슬 판정용</summary>
    private bool HasMovementInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        return Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;
    }
}
