using System.Collections;
using UnityEngine;

/// <summary>
/// PlayerBackstepShot — 원거리 캐릭터 시그니처 스킬 (우클릭).
/// 후방으로 빠르게 점프하면서, 정면(타겟 방향)으로 부채꼴 다발 화살 발사.
/// 
/// 디자인 의도 (Risk of Rain 2 헌트리스의 Phase Blink 모티브):
///   - 근접 캐릭의 PlayerFlashSlash가 "전진 + 광역 베기"라면,
///     이건 "후퇴 + 광역 부채꼴 사격" — 정확히 대칭.
///   - 활 캐릭의 거리 유지 메카닉 (카이팅 무브).
///   - 부채꼴 분산이라 정면에 적이 모이면 다단 히트 (큰 적 또는 밀집 군중에 강함).
/// 
/// PlayerFlashSlash와 동일한 구조:
///   - 코루틴 기반, isSkillActive / isSkillReady 플래그
///   - playerController.isSkillCasting 으로 일반 이동 잠금
///   - CancelSkill로 외부 강제 중단 가능 (어택캔슬용)
///   - 쿨다운은 캔슬해도 그대로 적용 (밸런스 페널티)
/// 
/// 다단 히트 메카닉:
///   - 각 화살은 독립된 LinearProjectile 인스턴스
///   - 적 한 명에 N발 맞으면 N번 데미지 (몸집 큰 적 = 자연 다단 히트)
///   - 화살 1개당 OnTriggerEnter → ApplySkillDamage → 자기만 Destroy
/// </summary>
public class PlayerBackstepShot : MonoBehaviour
{
    // ============================================================
    // Serialized Fields - 백스텝 동작
    // ============================================================
    [Header("백스텝 동작")]
    [SerializeField, Tooltip("후방 점프 거리")]
    private float backstepDistance = 4f;

    [SerializeField, Tooltip("백스텝 동작 지속 시간 (이 시간 동안 isSkillActive=true)")]
    private float backstepDuration = 0.6f;

    [SerializeField, Tooltip("스킬 쿨다운 (강제 캔슬해도 그대로 적용 — 밸런스 페널티)")]
    private float cooldownTime = 5f;

    [SerializeField, Tooltip("스킬 애니메이션 트리거 이름 (아처 에셋의 스킬 모션에 맞춰 설정)")]
    private string skillTrigger = "doBackstepShot";

    // ============================================================
    // Serialized Fields - 부채꼴 사격
    // ============================================================
    [Header("부채꼴 사격")]
    [SerializeField, Tooltip("동시 발사할 화살 개수 (부채꼴로 펼쳐짐)")]
    private int arrowCount = 5;

    [SerializeField, Tooltip("부채꼴 전체 각도 (예: 40 → 좌우 20도씩 펼쳐짐). 0이면 모든 화살 정면.")]
    [Range(0f, 180f)]
    private float fanAngle = 40f;

    [SerializeField, Tooltip("백스텝 시작 후 부채꼴 발사까지의 딜레이 (모션 타이밍 맞추기용)")]
    private float firstShotDelay = 0.05f;

    // ============================================================
    // Serialized Fields - 화살 (PlayerArrowAttack과 동일)
    // ============================================================
    [Header("화살 설정")]
    [SerializeField, Tooltip("발사할 화살 프리팹 (LinearProjectile 부착돼있어야 함)")]
    private GameObject arrowPrefab;

    [SerializeField, Tooltip("화살 발사 위치. 비우면 캐릭터 위치 + 1m")]
    private Transform firePoint;

    [SerializeField, Tooltip("화살 비행 속도")]
    private float arrowSpeed = 25f;

    [SerializeField, Tooltip("화살 최대 사거리")]
    private float arrowRange = 15f;

    [SerializeField, Tooltip("화살 적중 시 표시할 히트 이펙트")]
    private HitEffectType arrowHitEffect = HitEffectType.Slashing_Normal;

    // ============================================================
    // Serialized Fields - 데미지
    // ============================================================
    [Header("데미지 보정")]
    [SerializeField, Tooltip("기본 스킬 데미지 (화살 1발당. weapon 데미지에 합산됨)")]
    private int baseDamage = 30;

    // ============================================================
    // Serialized Fields - 환경
    // ============================================================
    [Header("환경")]
    [SerializeField, Tooltip("장애물 레이어 (벽, 펜스 등) — 백스텝 시 관통 불가")]
    private LayerMask obstacleLayer = -1;

    [Header("VFX")]
    [SerializeField, Tooltip("백스텝 시작 시 캐릭터 위치에 생성할 이펙트 (선택)")]
    private GameObject backstepEffectPrefab;

    [Header("References")]
    [SerializeField] private Weapon weapon;

    // ============================================================
    // Component References
    // ============================================================
    private PlayerController playerController;
    private PlayerLockOn playerLockOn;
    private Rigidbody rigid;

    [Header("Animator (필수 — 활 캐릭터 GameObject의 Animator를 직접 지정)")]
    [SerializeField, Tooltip(
    "이 캐릭터의 Animator. 활 캐릭이면 Archer GameObject의 Animator를 직접 드래그.\n" +
    "비워두면 자동 탐색하지만 GetComponentInChildren은 Player 본체의 검 Animator를 " +
    "먼저 잡기 때문에 자식 캐릭의 Animator를 못 받음. 반드시 직접 지정 권장.")]
    private Animator anim;

    // ============================================================
    // Skill State (PlayerFlashSlash와 동일)
    // ============================================================
    private bool isSkillActive = false;
    private bool isSkillReady = true;
    private Coroutine activeRoutine;

    // ============================================================
    // Unity Lifecycle
    // ============================================================
    private void Awake()
    {
        InitializeComponents();
    }

    /// <summary>인스펙터에서 비어있는 참조 자동 보충</summary>
    private void InitializeComponents()
    {
        playerController = GetComponent<PlayerController>();
        playerLockOn = GetComponent<PlayerLockOn>();
        rigid = GetComponent<Rigidbody>();

        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        if (weapon == null)
            weapon = GetComponentInChildren<Weapon>();

        if (anim == null) Debug.LogError("[PlayerBackstepShot] Animator 없음");
        if (playerController == null) Debug.LogError("[PlayerBackstepShot] PlayerController 없음");
        if (arrowPrefab == null) Debug.LogError("[PlayerBackstepShot] arrowPrefab 미설정");

        Debug.Log("[PlayerBackstepShot] 초기화 완료");
    }

    // ============================================================
    // Public Interface
    // ============================================================

    /// <summary>외부 호출 — 스킬 사용 (RangedCharacter 입력 라우팅에서 호출)</summary>
    public void UseSkill()
    {
        if (!CanUseSkill())
        {
            Debug.Log("[PlayerBackstepShot] 스킬 사용 불가");
            return;
        }

        activeRoutine = StartCoroutine(BackstepRoutine());
    }

    /// <summary>스킬 사용 가능 여부</summary>
    public bool CanUseSkill()
    {
        return isSkillReady
            && !isSkillActive
            && playerController != null
            && !playerController.isDodging;
    }

    /// <summary>현재 스킬 진행 중인지 (RangedCharacter.IsBusy 위임 시 사용)</summary>
    public bool IsActive() => isSkillActive;

    /// <summary>
    /// 진행 중인 백스텝 사격 즉시 강제 중단 (PlayerFlashSlash.CancelSkill과 동일 패턴).
    /// 태그 전환 시 어택캔슬로 RangedCharacter.CancelCurrentAction에서 호출.
    /// 진행 중이 아니면 no-op.
    /// </summary>
    public void CancelSkill()
    {
        if (!isSkillActive) return;

        // 1) 메인 코루틴 강제 중지
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        // 2) 진행 플래그 해제
        isSkillActive = false;

        // 3) 이동 잠금 해제 (필수 — 안 풀면 새 활성 캐릭이 이동 못 함)
        if (playerController != null)
        {
            playerController.isSkillCasting = false;
        }

        // 4) 쿨다운은 그대로 적용 (밸런스 페널티)
        StartCoroutine(CooldownAfterCancelRoutine());

        Debug.Log("[PlayerBackstepShot] 스킬 강제 중단 — 쿨다운 시작");
    }

    // ============================================================
    // Skill Execution
    // ============================================================

    /// <summary>
    /// 메인 스킬 코루틴.
    /// 흐름: StartSkill → 방향 계산 → 회전(정면) → 애니/VFX → 백스텝 시작 → firstShotDelay 대기 → 부채꼴 발사 → 백스텝 완료 → EndSkill → 쿨다운
    /// </summary>
    private IEnumerator BackstepRoutine()
    {
        // 1. 스킬 시작 (플래그 + 이동 잠금)
        StartSkill();

        // 2. 사격 방향 (락온/마우스 우선) + 백스텝 방향 (그 반대)
        Vector3 fireDirection = GetFireDirection();
        Vector3 backstepDir = -fireDirection;
        Vector3 startPos = transform.position;
        Vector3 targetPos = CalculateBackstepTargetPosition(startPos, backstepDir);

        // 3. 캐릭터 회전 — 정면(사격 방향) 고정. 뒤로 점프하면서 적 응시.
        if (fireDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(fireDirection);
        }

        // 4. 애니메이션 트리거
        if (anim != null)
        {
            anim.SetTrigger(skillTrigger);
        }

        // 5. VFX (선택)
        if (backstepEffectPrefab != null)
        {
            Instantiate(backstepEffectPrefab, startPos, Quaternion.LookRotation(fireDirection));
        }

        // 6. 백스텝 이동 시작 (병렬 — 사격과 동시 진행)
        Coroutine moveCoroutine = StartCoroutine(BackstepMovement(startPos, targetPos));

        // 7. 첫 발사까지 딜레이 (백스텝 모션 타이밍 맞추기)
        if (firstShotDelay > 0f)
            yield return new WaitForSeconds(firstShotDelay);

        // 8. 부채꼴 동시 발사 (한 프레임에 N발 모두 생성)
        FireFanShots(fireDirection);

        // 9. 백스텝 이동 완료 대기
        yield return moveCoroutine;

        // 10. 스킬 종료 (플래그 + 이동 잠금 해제)
        EndSkill();

        // 11. 쿨다운 (자연 종료 시)
        yield return new WaitForSeconds(cooldownTime);
        isSkillReady = true;

        Debug.Log("[PlayerBackstepShot] 쿨다운 종료, 스킬 준비 완료");
    }

    /// <summary>스킬 시작 처리 — 플래그 + 이동 잠금 + UI 쿨다운 표시</summary>
    private void StartSkill()
    {
        isSkillActive = true;
        isSkillReady = false;

        if (playerController != null)
        {
            playerController.isSkillCasting = true;
        }

        CombatUIManager.Instance?.StartSkillCoolDown(KeyCode.Mouse1, cooldownTime);

        Debug.Log("[PlayerBackstepShot] 스킬 시작!");
    }

    /// <summary>스킬 종료 처리 — 플래그 + 이동 잠금 해제</summary>
    private void EndSkill()
    {
        isSkillActive = false;

        if (playerController != null)
        {
            playerController.isSkillCasting = false;
        }

        Debug.Log("[PlayerBackstepShot] 스킬 종료");
    }

    // ============================================================
    // Direction & Position
    // ============================================================

    /// <summary>사격 방향 (락온 우선, 마우스 차선)</summary>
    private Vector3 GetFireDirection()
    {
        Vector3 dir;

        if (playerLockOn != null && playerLockOn.isLockOn && playerLockOn.currentTarget != null)
        {
            dir = (playerLockOn.currentTarget.position - transform.position).normalized;
        }
        else if (playerController != null)
        {
            dir = playerController.GetMouseDirection();
        }
        else
        {
            dir = transform.forward;
        }

        dir.y = 0f;
        dir.Normalize();
        return dir;
    }

    /// <summary>백스텝 목표 위치 계산 — 후방 장애물 체크 포함</summary>
    private Vector3 CalculateBackstepTargetPosition(Vector3 startPos, Vector3 backstepDir)
    {
        Vector3 targetPos = startPos + backstepDir * backstepDistance;

        // 후방 장애물 체크 (벽 뚫고 가지 않게)
        if (Physics.Raycast(startPos, backstepDir, out RaycastHit hit, backstepDistance, obstacleLayer))
        {
            targetPos = hit.point - backstepDir * 0.5f;
            Debug.Log($"[PlayerBackstepShot] 후방 장애물: {hit.collider.name}, 거리 단축");
        }

        targetPos.y = startPos.y;
        return targetPos;
    }

    // ============================================================
    // Movement (PlayerFlashSlash.DashMovement와 동일 패턴, 방향만 반대)
    // ============================================================

    /// <summary>백스텝 이동 코루틴 — Ease Out Cubic으로 빠르게 시작 부드럽게 감속</summary>
    private IEnumerator BackstepMovement(Vector3 startPos, Vector3 targetPos)
    {
        float elapsed = 0f;

        while (elapsed < backstepDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / backstepDuration;

            // Ease Out Cubic — 빠르게 시작, 부드럽게 감속
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            Vector3 newPos = Vector3.Lerp(startPos, targetPos, easedT);
            newPos.y = startPos.y;

            if (rigid != null)
            {
                rigid.MovePosition(newPos);
            }
            else
            {
                transform.position = newPos;
            }

            yield return null;
        }

        // 최종 위치 확정
        if (rigid != null)
        {
            rigid.MovePosition(targetPos);
        }
        else
        {
            transform.position = targetPos;
        }
    }

    // ============================================================
    // Fan Shot (부채꼴 동시 발사)
    // ============================================================

    /// <summary>
    /// 부채꼴 동시 발사 — N발의 화살을 fanAngle 범위로 등각 분배해서 한 프레임에 모두 생성.
    /// 각 화살은 독립된 LinearProjectile이라 적 충돌 시 개별 데미지 처리됨
    /// (= 큰 적이 N발 다 맞으면 N번 데미지 → 다단 히트 자연 발생).
    /// </summary>
    /// <param name="centerDirection">부채꼴 중심 방향 (정면)</param>
    private void FireFanShots(Vector3 centerDirection)
    {
        if (arrowCount <= 0) return;

        // 화살 1발이면 정면 단발 (각도 분배 의미 없음)
        if (arrowCount == 1)
        {
            FireArrow(centerDirection);
            return;
        }

        // 등각 분배 — 가장 왼쪽 화살부터 가장 오른쪽 화살까지
        // arrowCount=3, fanAngle=30 → [-15°, 0°, +15°]
        // arrowCount=5, fanAngle=40 → [-20°, -10°, 0°, +10°, +20°]
        float startAngle = -fanAngle * 0.5f;
        float angleStep = fanAngle / (arrowCount - 1);

        for (int i = 0; i < arrowCount; i++)
        {
            float angle = startAngle + angleStep * i;

            // Y축(world up) 기준 회전 — 쿼터뷰라 수평면에서만 펼침
            Vector3 shotDir = Quaternion.AngleAxis(angle, Vector3.up) * centerDirection;
            FireArrow(shotDir);
        }
    }

    /// <summary>
    /// 단발 화살 발사 — 지정된 방향으로 LinearProjectile 인스턴스화.
    /// PlayerArrowAttack.FireArrow와 동일한 데미지 계산 로직 사용.
    /// </summary>
    private void FireArrow(Vector3 fireDirection)
    {
        if (arrowPrefab == null) return;

        Vector3 spawnPos = firePoint != null
            ? firePoint.position
            : transform.position + Vector3.up * 1f;

        GameObject arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.LookRotation(fireDirection));

        LinearProjectile projectile = arrow.GetComponent<LinearProjectile>();
        if (projectile != null)
        {
            int damage = baseDamage;
            if (weapon != null) damage += weapon.damage;
            projectile.Initialize(arrowSpeed, damage, arrowRange, arrowHitEffect);
        }
        else
        {
            Debug.LogWarning("[PlayerBackstepShot] arrowPrefab에 LinearProjectile 없음");
        }
    }

    // ============================================================
    // Cooldown after cancel (PlayerFlashSlash와 동일)
    // ============================================================

    /// <summary>강제 캔슬 시 별도 쿨다운 루틴 — 메인 코루틴이 끊겼으니 별도 처리 필요</summary>
    private IEnumerator CooldownAfterCancelRoutine()
    {
        yield return new WaitForSeconds(cooldownTime);
        isSkillReady = true;
        Debug.Log("[PlayerBackstepShot] 쿨다운 종료 (강제 중단 후)");
    }

    // ============================================================
    // Debug Visualization
    // ============================================================
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        if (isSkillActive)
        {
            Vector3 fireDir = GetFireDirection();

            // 사격 방향 (정면, 청록)
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + fireDir * arrowRange);

            // 부채꼴 양 끝선 (좌우 가장자리, 자홍)
            Gizmos.color = Color.magenta;
            Vector3 leftEdge = Quaternion.AngleAxis(-fanAngle * 0.5f, Vector3.up) * fireDir;
            Vector3 rightEdge = Quaternion.AngleAxis(fanAngle * 0.5f, Vector3.up) * fireDir;
            Gizmos.DrawLine(transform.position, transform.position + leftEdge * arrowRange);
            Gizmos.DrawLine(transform.position, transform.position + rightEdge * arrowRange);

            // 백스텝 방향 (후방, 노랑)
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position - fireDir * backstepDistance);
        }
    }
}
