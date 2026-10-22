using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FlashSlash(일섬) 스킬 — 검(Melee) 우클릭 순간 돌진 베기.
/// 
/// 동작:
///   마우스/락온 방향으로 빠르게 돌진하며 경로상의 적들에게 광역 데미지.
///   몬스터는 관통, 장애물은 관통 불가 (벽에서 정지).
/// 
/// 영혼게이지 연동:
///   경로 명중 판정에서 적 1마리 이상 명중 시 StanceGaugeSystem.AddSoulGauge(soulGaugeOnHit) 1회 호출.
///   적립량은 좌클릭(1)보다 높게 설정 (기본 2) — 강공격 보상.
///   허공 일섬(명중 0)은 적립 없음.
///   광역 N마리 동시 명중해도 1회만 적립 (정책 일관성).
/// 
/// 활 모드 차단:
///   이 파일은 자체 차단 X. 검 모드일 때만 호출되도록 입력 처리(PlayerCombatStateMachine)에서 분기 책임.
/// 
/// 강제 캔슬(태그 전환 어택캔슬):
///   CancelSkill() 호출 시 메인 코루틴 중단 + isSkillCasting 해제 + 쿨다운은 페널티로 그대로 진행.
/// </summary>
public class PlayerFlashSlash : MonoBehaviour
{
    #region Serialized Fields
    [Header("Dash Settings")]
    [SerializeField, Tooltip("돌진 거리")]
    private float dashDistance = 10f;

    [SerializeField, Tooltip("돌진 지속 시간 (애니메이션 시간)")]
    private float dashDuration = 0.7f;

    [SerializeField, Tooltip("스킬 쿨다운")]
    private float cooldownTime = 5f;

    [Header("Damage Settings")]
    [SerializeField, Tooltip("스킬 데미지")]
    private int damage = 50;

    [SerializeField, Tooltip("공격 범위 너비 (경로 좌우)")]
    private float pathWidth = 3f;

    [SerializeField, Tooltip("적 레이어")]
    private LayerMask enemyLayer = -1;

    [SerializeField, Tooltip("장애물 레이어 (벽, 펜스 등)")]
    private LayerMask obstacleLayer = -1;

    [Header("VFX & Audio")]
    [SerializeField, Tooltip("돌진 이펙트")]
    private GameObject dashEffectPrefab;

    [SerializeField, Tooltip("히트 이펙트")]
    private GameObject hitEffectPrefab;

    [Header("SFX")]
    [SerializeField, Tooltip("Flash Slash 발동 시 재생할 효과음")]
    private AudioClip skillSfx;

    [Header("References")]
    [SerializeField] private Weapon weapon;

    [Header("Arrows")]
    [SerializeField, Tooltip("일섬 명중 시 화살 적립량 (1회 발동당). 좌클릭(1)보다 높게 — 강공격 보상.")]
    private int arrowsOnHit = 2;
    #endregion

    #region Component References
    private PlayerController playerController;
    private PlayerLockOn playerLockOn;
    private Rigidbody rigid;
    private Animator anim;
    #endregion

    #region Skill State
    /// <summary>현재 스킬 실행 중인지</summary>
    private bool isSkillActive = false;

    /// <summary>스킬 사용 가능한지</summary>
    private bool isSkillReady = true;

    /// <summary>히트한 적들 (중복 히트 방지)</summary>
    private HashSet<GameObject> hitTargets = new HashSet<GameObject>();

    /// <summary>
    /// 현재 진행 중인 메인 스킬 코루틴 핸들 (강제 캔슬용).
    /// UseSkill에서 저장, CancelSkill에서 StopCoroutine 호출, 종료 시 null로 정리.
    /// </summary>
    private Coroutine activeRoutine;
    #endregion

    #region Unity Lifecycle
    /// <summary>컴포넌트 초기화</summary>
    private void Awake()
    {
        InitializeComponents();
    }
    #endregion

    #region Initialization
    /// <summary>컴포넌트 참조 초기화</summary>
    private void InitializeComponents()
    {
        playerController = GetComponent<PlayerController>();
        playerLockOn = GetComponent<PlayerLockOn>();
        rigid = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();

        if (anim == null)
            Debug.LogError("[PlayerFlashSlash] Animator를 찾을 수 없습니다!");

        if (playerController == null)
            Debug.LogError("[PlayerFlashSlash] PlayerController를 찾을 수 없습니다!");

        if (weapon == null)
            weapon = GetComponentInChildren<Weapon>();

        Debug.Log("[PlayerFlashSlash] 초기화 완료");
    }
    #endregion

    #region Public Interface
    /// <summary>외부에서 호출: FlashSlash 스킬 사용</summary>
    public void UseSkill()
    {
        if (!CanUseSkill())
        {
            Debug.Log("[PlayerFlashSlash] 스킬 사용 불가");
            return;
        }

        // SFX 재생 (발동 순간 — 사용 가능할 때만)
        if (skillSfx != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(skillSfx);

        // 코루틴 핸들 저장 → CancelSkill에서 중단 가능
        activeRoutine = StartCoroutine(FlashSlashRoutine());
    }

    /// <summary>스킬 사용 가능 여부</summary>
    public bool CanUseSkill()
    {
        return isSkillReady &&
               !isSkillActive &&
               playerController != null &&
               !playerController.isDodging;
    }

    /// <summary>현재 스킬 실행 중인지</summary>
    public bool IsActive()
    {
        return isSkillActive;
    }

    /// <summary>
    /// 진행 중인 FlashSlash를 즉시 강제 중단.
    /// 태그 전환 시 어택캔슬로 호출됨 (MeleeCharacter.CancelCurrentAction에서).
    /// 
    /// 처리 내용:
    ///   1) 메인 코루틴 강제 중지 (돌진 이동 + 자연 쿨다운 흐름 끊음)
    ///   2) isSkillActive 플래그 해제
    ///   3) playerController.isSkillCasting 해제 (이동 잠금 해제 — 핵심)
    ///   4) 별도 쿨다운 코루틴 시작 (어택캔슬해도 쿨다운은 페널티로 그대로 적용)
    /// 
    /// 진행 중이 아니면 no-op.
    /// </summary>
    public void CancelSkill()
    {
        if (!isSkillActive) return; // 이미 끝났거나 시작 안 했으면 무시

        // 1. 메인 코루틴 강제 중지 — 돌진 멈춤, 자연 종료/쿨다운 흐름 끊김
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        // 2. 진행 플래그 해제
        isSkillActive = false;

        // 3. 이동 잠금 해제 (이거 안 풀면 새 활성 캐릭터도 이동 못 함)
        if (playerController != null)
        {
            playerController.isSkillCasting = false;
        }

        // 4. 쿨다운은 그대로 적용 — 어택캔슬에 약간의 페널티 부여 (밸런스용)
        StartCoroutine(CooldownAfterCancelRoutine());

        Debug.Log("[PlayerFlashSlash] 스킬 강제 중단 — 쿨다운 시작");
    }

    #endregion

    #region Skill Execution
    /// <summary>FlashSlash 스킬 실행 루틴</summary>
    private IEnumerator FlashSlashRoutine()
    {
        // 1. 스킬 시작 준비
        StartSkill();

        // 2. 돌진 방향 및 목표 위치 계산
        Vector3 dashDirection = GetDashDirection();
        Vector3 startPos = transform.position;
        Vector3 targetPos = CalculateTargetPosition(startPos, dashDirection);

        // 3. 애니메이션 시작
        if (anim != null)
        {
            anim.SetTrigger("doSpinAtt");  // Spin_Attack 애니메이션 사용
        }

        // 4. 돌진 이동
        yield return StartCoroutine(DashMovement(startPos, targetPos));

        // 5. 경로상의 적들에게 데미지 + 영혼게이지 적립 (한 번에 계산)
        DamageEnemiesInPath(startPos, targetPos, dashDirection);

        // 6. 스킬 종료
        EndSkill();

        // 7. 쿨다운
        yield return new WaitForSeconds(cooldownTime);
        isSkillReady = true;

        Debug.Log("[PlayerFlashSlash] 쿨다운 종료, 스킬 준비 완료");
    }

    /// <summary>스킬 시작 처리</summary>
    private void StartSkill()
    {
        isSkillActive = true;
        isSkillReady = false;
        hitTargets.Clear();

        // 플레이어 이동 금지
        if (playerController != null)
        {
            playerController.isSkillCasting = true;
        }

        CombatUIManager.Instance?.StartSkillCoolDown(KeyCode.Mouse1, cooldownTime);

        Debug.Log("[PlayerFlashSlash] 스킬 시작!");
    }

    /// <summary>스킬 종료 처리</summary>
    private void EndSkill()
    {
        isSkillActive = false;

        // 플레이어 이동 허용
        if (playerController != null)
        {
            playerController.isSkillCasting = false;
        }

        Debug.Log("[PlayerFlashSlash] 스킬 종료");
    }

    /// <summary>
    /// 강제 캔슬 시 별도로 돌아가는 쿨다운 코루틴.
    /// 메인 FlashSlashRoutine이 StopCoroutine으로 끊겼으니 자연 쿨다운 흐름이 사라짐 → 별도 처리 필요.
    /// </summary>
    private IEnumerator CooldownAfterCancelRoutine()
    {
        yield return new WaitForSeconds(cooldownTime);
        isSkillReady = true;
        Debug.Log("[PlayerFlashSlash] 쿨다운 종료 (강제 중단 후), 스킬 준비 완료");
    }

    #endregion

    #region Direction & Position Calculation
    /// <summary>돌진 방향 계산</summary>
    private Vector3 GetDashDirection()
    {
        Vector3 direction;

        // 락온 중이면 락온된 적 방향
        if (playerLockOn != null && playerLockOn.isLockOn && playerLockOn.currentTarget != null)
        {
            direction = (playerLockOn.currentTarget.position - transform.position).normalized;
        }
        // 아니면 마우스 방향
        else if (playerController != null)
        {
            direction = playerController.GetMouseDirection();
        }
        else
        {
            direction = transform.forward;
        }

        // Y축 제거 (XZ 평면에서만 이동)
        direction.y = 0f;
        direction.Normalize();

        return direction;
    }

    /// <summary>목표 위치 계산 (장애물 체크 포함)</summary>
    private Vector3 CalculateTargetPosition(Vector3 startPos, Vector3 direction)
    {
        Vector3 targetPos = startPos + direction * dashDistance;

        // 장애물 체크 (벽, 펜스 등은 관통 불가)
        if (Physics.Raycast(startPos, direction, out RaycastHit hit, dashDistance, obstacleLayer))
        {
            // 장애물 앞에서 멈춤 (약간의 여유 공간)
            targetPos = hit.point - direction * 0.5f;
            Debug.Log($"[PlayerFlashSlash] 장애물 감지: {hit.collider.name}, 거리 조정");
        }

        // Y축 고정
        targetPos.y = startPos.y;

        return targetPos;
    }
    #endregion

    #region Movement
    /// <summary>돌진 이동 코루틴</summary>
    private IEnumerator DashMovement(Vector3 startPos, Vector3 targetPos)
    {
        float elapsed = 0f;

        // 캐릭터가 목표 방향을 바라보도록 회전
        Vector3 lookDirection = (targetPos - startPos).normalized;
        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        // 애니메이션 재생 시간 동안 이동
        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dashDuration;

            // Ease Out Cubic 커브 (처음 빠르게, 끝에 천천히)
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            // 위치 보간
            Vector3 newPos = Vector3.Lerp(startPos, targetPos, easedT);
            newPos.y = startPos.y; // Y축 고정

            // Rigidbody를 사용하여 이동 (물리 충돌 고려)
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

        Debug.Log($"[PlayerFlashSlash] 돌진 완료: {startPos} → {targetPos}");
    }
    #endregion

    #region Damage System
    /// <summary>
    /// 경로상의 적들에게 데미지 + 영혼게이지 적립.
    /// OverlapBox로 한 번에 적 감지. 광역 N마리 동시 명중해도 영혼게이지는 1회만 적립 (정책 일관성).
    /// 적 0마리 명중 시 적립 없음 (허공 일섬은 보상 없음).
    /// </summary>
    private void DamageEnemiesInPath(Vector3 startPos, Vector3 endPos, Vector3 direction)
    {
        // 경로 중심점 계산
        Vector3 pathCenter = (startPos + endPos) / 2f;
        float pathLength = Vector3.Distance(startPos, endPos);

        // BoxCast로 경로상의 모든 적 감지
        // 박스 크기: 길이 x 너비 x 높이
        Vector3 boxHalfExtents = new Vector3(pathWidth / 2f, 1f, pathLength / 2f);

        // 회전 (돌진 방향으로)
        Quaternion boxRotation = Quaternion.LookRotation(direction);

        // OverlapBox로 적 감지 (몬스터만, 장애물 제외)
        Collider[] hits = Physics.OverlapBox(pathCenter, boxHalfExtents, boxRotation, enemyLayer);

        Debug.Log($"[PlayerFlashSlash] 경로상 적 감지: {hits.Length}명");

        bool anyHit = false;

        // 감지된 모든 적에게 데미지
        foreach (var hitCollider in hits)
        {
            // 중복 히트 방지
            if (hitTargets.Contains(hitCollider.gameObject))
                continue;

            IDamageable damageable = hitCollider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                // 데미지 적용
                ApplyDamage(damageable);

                // 히트 이펙트
                SpawnHitEffect(hitCollider.transform.position);

                // 중복 히트 방지용 등록
                hitTargets.Add(hitCollider.gameObject);

                anyHit = true;
            }
        }

        // 화살 적립 — 한 번 발동해서 1마리라도 맞았으면 +arrowsOnHit, 0마리면 적립 없음
        if (anyHit && StanceGaugeSystem.Instance != null)
        {
            StanceGaugeSystem.Instance.AddArrows(arrowsOnHit);
        }
    }

    /// <summary>적에게 데미지 적용 — 기본 스킬 데미지 + 무기 데미지 합산</summary>
    private void ApplyDamage(IDamageable damageable)
    {
        if (damageable == null) return;

        // 기본 데미지 + 무기 보너스 합산
        int finalDamage = damage;
        if (weapon != null)
        {
            finalDamage += weapon.damage;
        }

        // Enemy의 ApplySkillDamage 호출
        damageable.ApplySkillDamage(finalDamage, HitEffectType.Slashing_Flash);
    }

    /// <summary>히트 이펙트 생성</summary>
    private void SpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }
    #endregion

    #region Debug Visualization
    /// <summary>경로 시각화 (Scene 뷰)</summary>
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        if (isSkillActive)
        {
            // 돌진 방향 표시
            Gizmos.color = Color.cyan;
            Vector3 direction = GetDashDirection();
            Gizmos.DrawLine(transform.position, transform.position + direction * dashDistance);

            // 공격 범위 표시
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + direction * (dashDistance / 2f),
                                new Vector3(pathWidth, 2f, dashDistance));
        }
    }
    #endregion
}
