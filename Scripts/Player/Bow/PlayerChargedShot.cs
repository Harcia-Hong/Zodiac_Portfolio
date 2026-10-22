using System.Collections;
using UnityEngine;

/// <summary>
/// ChargedShot 스킬 — 활(Ranged) 우클릭 강사격.
/// 
/// 동작 (Skill_03 애니메이션, 2.5초 / 150 frame @ 60fps 기준):
///   0~15 frame  (0~0.25s) : 자세 잡기 시작
///   15~60 frame (0.25~1.0s) : 시위 당기기 (차징)
///   60 frame    (1.0s)     : 발사
///   60~90 frame (1.0~1.5s) : 반동 (뒤로 밀림)
///   90~150 frame(1.5~2.5s) : 자세 복귀
///   전체 모션 동안 이동 잠금 (isSkillCasting = true).
/// 
/// 화살 자원:
///   UseSkill 시점에 ConsumeArrows(arrowsPerShot) 호출. 부족 시 사용 차단.
///   기본 2발 소모 (좌클릭의 2배 — 강사격 비용).
/// 
/// 발사 메커니즘:
///   PlayerArrowAttack과 동일 — ArrowBeam(Hitscan) 우선, 없으면 LinearProjectile(Projectile) 폴백.
///   데미지는 별도 인스펙터 값 + 무기 보너스 합산.
/// 
/// 회전 정책:
///   진입 시점에 1회 회전. 차징 도중 마우스 따라가지 않음 (차징 본질 보존).
/// 
/// 활 모드 차단:
///   이 컴포넌트는 활 캐릭터 GameObject에 부착. RangedCharacter.Activate/Deactivate에서 enabled 토글되어야 함.
/// 
/// 강제 캔슬 (태그 전환):
///   CancelSkill() 호출 시 메인 코루틴 중단 + isSkillCasting 해제 + 쿨다운은 페널티로 적용.
/// </summary>
public class PlayerChargedShot : MonoBehaviour
{
    #region Serialized Fields
    [Header("타이밍 (Skill_03 애니메이션 기준)")]
    [SerializeField, Tooltip("선딜 — 자세 잡고 시위 당기는 시간 (0~60 frame = 1.0초).")]
    private float fireDelay = 1.0f;

    [SerializeField, Tooltip("전체 동작 지속 시간 — 자세 + 차징 + 발사 + 반동 + 복귀 (전체 150 frame = 2.5초).")]
    private float totalDuration = 2.5f;

    [SerializeField, Tooltip("스킬 쿨다운 (초). 강사격이라 길게 — 좌클릭 0.1초와 대비.")]
    private float cooldownTime = 3.0f;

    [Header("데미지 설정")]
    [SerializeField, Tooltip("차징 사격 기본 데미지. weapon.damage가 추가로 합산됨.")]
    private int damage = 80;

    [Header("화살 자원")]
    [SerializeField, Tooltip("1회 차징 사격 시 소모할 화살 수. 기본 2 (좌클릭의 2배 — 강사격 비용).")]
    private int arrowsPerShot = 2;

    [Header("발사체 설정")]
    [SerializeField, Tooltip("화살 프리팹 (LinearProjectile 부착). 빔 모드 시 무시됨.")]
    private GameObject arrowPrefab;

    [SerializeField, Tooltip("발사 위치 (활 끝). 비우면 캐릭터 + 1m.")]
    private Transform firePoint;

    [SerializeField, Tooltip("화살 비행 속도 (빔 모드 시 무시됨). 좌클릭(25)보다 빠르게.")]
    private float arrowSpeed = 35f;

    [SerializeField, Tooltip("최대 사거리 — 빔/화살 공통. 좌클릭(15)보다 길게.")]
    private float arrowRange = 20f;

    [SerializeField, Tooltip("적중 시 히트 이펙트 — 빔/화살 공통.")]
    private HitEffectType arrowHitEffect = HitEffectType.Slashing_Flash;

    [Header("애니메이션")]
    [SerializeField, Tooltip("차징 사격 애니메이션 트리거 이름. Animator에 동일 이름 트리거 필요.")]
    private string animTrigger = "doChargedShot";

    [Header("VFX (선택)")]
    [SerializeField, Tooltip("차징 시작 시 firePoint에 부착될 이펙트 (시위 빛남 등). 자동 정리.")]
    private GameObject chargeEffectPrefab;

    [SerializeField, Tooltip("발사 순간 firePoint에 생성될 이펙트.")]
    private GameObject fireEffectPrefab;

    [Header("References")]
    [SerializeField] private Weapon weapon;

    [SerializeField, Tooltip("ArrowBeam 컴포넌트. firePoint에 붙어있다면 자동 연결.")]
    private ArrowBeam arrowBeam;

    [SerializeField, Tooltip("Archer GameObject의 Animator를 직접 지정. 비우면 GetComponentInChildren로 자동 탐색.")]
    private Animator anim;
    #endregion

    #region Component References
    private PlayerController playerController;
    private PlayerLockOn playerLockOn;
    private RangedCharacter rangedCharacter;
    #endregion

    #region Skill State
    /// <summary>현재 차징 사격 진행 중인지 (선딜+발사+후딜 포함).</summary>
    private bool isSkillActive = false;

    /// <summary>쿨다운 끝나서 다시 사용 가능한지.</summary>
    private bool isSkillReady = true;

    /// <summary>
    /// 진행 중인 메인 코루틴 핸들 (강제 캔슬용).
    /// UseSkill에서 저장 → CancelSkill에서 StopCoroutine → 종료 시 null 정리.
    /// </summary>
    private Coroutine activeRoutine;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        InitializeComponents();
    }
    #endregion

    #region Initialization
    /// <summary>컴포넌트 참조 자동 초기화. 인스펙터에서 명시 안 한 경우 폴백 탐색.</summary>
    private void InitializeComponents()
    {
        playerController = GetComponent<PlayerController>();
        playerLockOn = GetComponent<PlayerLockOn>();
        rangedCharacter = GetComponent<RangedCharacter>();

        if (weapon == null)
            weapon = GetComponentInChildren<Weapon>();

        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        // ArrowBeam 자동 탐색 — firePoint에 붙어있다면 자동 연결
        if (arrowBeam == null && firePoint != null)
            arrowBeam = firePoint.GetComponent<ArrowBeam>();

        if (anim == null)
            Debug.LogError("[PlayerChargedShot] Animator를 찾을 수 없습니다!");

        if (playerController == null)
            Debug.LogError("[PlayerChargedShot] PlayerController를 찾을 수 없습니다!");

        // 빔도 화살도 없으면 발사 불가
        if (arrowBeam == null && arrowPrefab == null)
            Debug.LogError("[PlayerChargedShot] ArrowBeam과 arrowPrefab 둘 다 없습니다! 하나는 설정 필요");
    }
    #endregion

    #region Public Interface
    /// <summary>
    /// 외부 호출 — 차징 사격 사용.
    /// 흐름:
    ///   1. 사용 가능 상태 체크
    ///   2. 화살 자원 차감 시도 (실패 시 사용 차단)
    ///   3. ChargedShotRoutine 코루틴 실행
    /// </summary>
    public void UseSkill()
    {
        if (!CanUseSkill())
        {
            Debug.Log("[PlayerChargedShot] 스킬 사용 불가");
            return;
        }

        // 화살 자원 체크 + 차감 — 부족하면 모션도 안 나감
        if (StanceGaugeSystem.Instance == null
            || !StanceGaugeSystem.Instance.ConsumeArrows(arrowsPerShot))
        {
            Debug.Log("[PlayerChargedShot] 화살 부족 — 사용 차단 (Tab으로 검 전환 + 태그기 가능)");
            return;
        }

        activeRoutine = StartCoroutine(ChargedShotRoutine());
    }

    /// <summary>사용 가능 여부 (자원 체크는 UseSkill에서 분리 처리).</summary>
    public bool CanUseSkill()
    {
        return isSkillReady
            && !isSkillActive
            && playerController != null
            && !playerController.isDodging;
    }

    /// <summary>현재 진행 중인지 — 외부에서 IsBusy 판정 시 참조 가능.</summary>
    public bool IsActive() => isSkillActive;

    /// <summary>
    /// 진행 중인 ChargedShot을 즉시 강제 중단.
    /// 태그 전환 시 어택캔슬로 호출됨 (RangedCharacter.CancelCurrentAction 경로).
    /// 
    /// 처리:
    ///   1) 메인 코루틴 강제 중지 — 자연 종료/쿨다운 흐름 끊김
    ///   2) isSkillActive 해제
    ///   3) playerController.isSkillCasting 해제 (이동 잠금 해제 — 핵심)
    ///   4) 별도 쿨다운 코루틴 시작 — 어택캔슬해도 쿨다운은 페널티로 적용
    /// 
    /// 진행 중이 아니면 no-op.
    /// </summary>
    public void CancelSkill()
    {
        if (!isSkillActive) return;

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        isSkillActive = false;

        if (playerController != null)
            playerController.isSkillCasting = false;

        // 어택캔슬해도 쿨다운은 그대로 — 캔슬 남발 방지
        StartCoroutine(CooldownAfterCancelRoutine());

        Debug.Log("[PlayerChargedShot] 스킬 강제 중단 — 쿨다운 시작");
    }
    #endregion

    #region Skill Execution
    /// <summary>
    /// ChargedShot 메인 루틴.
    /// 흐름:
    ///   1) StartSkill — 이동 잠금 + UI 쿨다운 시작
    ///   2) 진입 시점 회전 + 애니메이션 트리거 + 차징 이펙트
    ///   3) fireDelay 대기 (자세 잡기 + 시위 당기기, 1.0초)
    ///   4) FireArrow — 실제 발사 + 발사 이펙트
    ///   5) 후딜 대기 (반동 + 자세 복귀, 1.5초)
    ///   6) EndSkill — 이동 잠금 해제
    ///   7) 쿨다운 대기 → isSkillReady = true
    /// </summary>
    private IEnumerator ChargedShotRoutine()
    {
        StartSkill();

        // 진입 시점 1회 회전 (차징 도중 마우스 따라가지 않음)
        RotateToTarget();

        // 애니메이션 트리거
        if (anim != null) anim.SetTrigger(animTrigger);

        // 차징 시각 이펙트 (시위 빛남 등)
        SpawnChargeEffect();

        // 1) 선딜 대기 — 0~60 frame (자세 잡기 + 시위 당기기)
        yield return new WaitForSeconds(fireDelay);

        // 2) 발사
        FireArrow();
        SpawnFireEffect();

        // 3) 후딜 대기 — 60~150 frame (반동 + 자세 복귀)
        float postFireDuration = totalDuration - fireDelay;
        yield return new WaitForSeconds(postFireDuration);

        // 4) 스킬 종료 — 이동 잠금 해제
        EndSkill();

        // 5) 쿨다운
        yield return new WaitForSeconds(cooldownTime);
        isSkillReady = true;

        Debug.Log("[PlayerChargedShot] 쿨다운 종료, 스킬 준비 완료");
    }

    /// <summary>스킬 시작 처리 — 상태 플래그 설정 + 이동 잠금 + UI 쿨다운.</summary>
    private void StartSkill()
    {
        isSkillActive = true;
        isSkillReady = false;

        // 전체 모션 동안 이동 잠금
        if (playerController != null)
            playerController.isSkillCasting = true;

        // 우클릭 슬롯 쿨다운 UI 시작
        CombatUIManager.Instance?.StartSkillCoolDown(KeyCode.Mouse1, cooldownTime);

        Debug.Log("[PlayerChargedShot] 스킬 시작 — 이동 잠금");
    }

    /// <summary>스킬 종료 처리 — 상태 해제 + 이동 잠금 해제.</summary>
    private void EndSkill()
    {
        isSkillActive = false;

        if (playerController != null)
            playerController.isSkillCasting = false;

        Debug.Log("[PlayerChargedShot] 스킬 종료 — 이동 잠금 해제");
    }

    /// <summary>
    /// 강제 캔슬 시 별도 쿨다운 코루틴.
    /// 메인 ChargedShotRoutine이 StopCoroutine으로 끊겼으므로 자연 쿨다운 흐름이 사라짐 → 별도 처리.
    /// </summary>
    private IEnumerator CooldownAfterCancelRoutine()
    {
        yield return new WaitForSeconds(cooldownTime);
        isSkillReady = true;
        Debug.Log("[PlayerChargedShot] 쿨다운 종료 (강제 중단 후), 스킬 준비 완료");
    }
    #endregion

    #region Fire
    /// <summary>
    /// 실제 발사 — ArrowBeam 우선, 없으면 LinearProjectile로 폴백.
    /// 차징 사격 데미지(damage) + 무기 보너스(weapon.damage) 합산.
    /// </summary>
    private void FireArrow()
    {
        Vector3 fireDir = GetFireDirection();

        // 차징 사격 데미지 = 인스펙터 기본값 + 무기 데미지
        int finalDamage = damage;
        if (weapon != null) finalDamage += weapon.damage;

        // ---- 빔 모드 (Hitscan) ----
        if (arrowBeam != null)
        {
            arrowBeam.Fire(fireDir, finalDamage, arrowRange, arrowHitEffect);
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
            projectile.Initialize(arrowSpeed, finalDamage, arrowRange, arrowHitEffect);
        }
        else
        {
            Debug.LogWarning("[PlayerChargedShot] arrowPrefab에 LinearProjectile 없음");
        }
    }
    #endregion

    #region Direction & Rotation
    /// <summary>발사 방향 계산 — 락온 > 마우스 > 전방 순서.</summary>
    private Vector3 GetFireDirection()
    {
        if (playerLockOn != null && playerLockOn.isLockOn && playerLockOn.currentTarget != null)
        {
            Vector3 toTarget = (playerLockOn.currentTarget.position - transform.position).normalized;
            toTarget.y = 0;
            return toTarget;
        }

        if (playerController != null)
            return playerController.GetMouseDirection();

        return transform.forward;
    }

    /// <summary>타겟 방향으로 캐릭터 회전 (Y축 고정).</summary>
    private void RotateToTarget()
    {
        Vector3 dir = GetFireDirection();
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);
    }
    #endregion

    #region VFX Helpers
    /// <summary>차징 이펙트 생성 — firePoint 자식으로 부착해 활 움직임을 따라가게 함.</summary>
    private void SpawnChargeEffect()
    {
        if (chargeEffectPrefab == null) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        Transform parent = firePoint != null ? firePoint : transform;
        GameObject effect = Instantiate(chargeEffectPrefab, spawnPos, transform.rotation, parent);

        // 차징 끝나면 사라지도록 (선딜 + 약간 여유)
        Destroy(effect, fireDelay + 0.5f);
    }

    /// <summary>발사 이펙트 생성 — 부착 없이 월드 공간에 1회성 생성.</summary>
    private void SpawnFireEffect()
    {
        if (fireEffectPrefab == null) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject effect = Instantiate(fireEffectPrefab, spawnPos, transform.rotation);
        Destroy(effect, 2f);
    }
    #endregion

    #region Debug Visualization
    /// <summary>발사 방향 시각화 (Scene 뷰, 차징 중에만).</summary>
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !isSkillActive) return;

        Gizmos.color = Color.yellow;
        Vector3 direction = GetFireDirection();
        Gizmos.DrawLine(transform.position, transform.position + direction * arrowRange);
    }
    #endregion
}
