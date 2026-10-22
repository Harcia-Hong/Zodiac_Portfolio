/*using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// 검 전용 스킬 시스템
/// 기존 PlayerSkill.cs에서 검 관련 기능만 분리
/// Q스킬: 검기 발사, E스킬: 범위 공격
/// IWeaponSkill 인터페이스 구현
/// </summary>
public class PlayerSwordSkill : MonoBehaviour
{
    [Header("Q Skill - Sword Slash")]
    public float qSkillCooldown = 5f;
    public float qSkillRange = 10f;
    public int qSkillDamage = 30;
    public float qSkillSpeed = 20f;
    public LayerMask qSkillEnemyLayer;

    [Header("E Skill - Ground Strike")]
    public float eSkillCooldown = 7f;
    public float eSkillRadius = 4f;
    public int eSkillDamage = 40;
    public float eSkillCastTime = 1.5f;

    [Header("VFX & Effects")]
    public VisualEffect slashVFXGraph;
    public Transform vfxSpawnPoint;
    public GameObject eSkillEffect;
    public GameObject skillSlashEffect;

    [Header("UI References")]
    public Transform skillOrigin;

    [Header("Animation")]
    public string qSkillAnimTrigger = "Skill_Q";
    public string eSkillAnimTrigger = "Skill_E";

    // 컴포넌트 참조
    private PlayerController playerController;
    private PlayerLockOn playerLockOn;
    private Animator anim;

    // 스킬 상태 관리
    private bool isQSkillReady = true;
    private bool isESkillReady = true;
    private float qCooldownTimer = 0f;
    private float eCooldownTimer = 0f;

    // E스킬 타겟팅 시스템
    private enum ESkillState { Ready, ShowingIndicator, Casting, Cooldown }
    private ESkillState eSkillState = ESkillState.Ready;
    private Vector3 eSkillTargetPosition;
    private Coroutine eSkillCastCoroutine;
    private SwordAbility swordIndicator; // 인디케이터 시스템 참조

    /// <summary>초기화</summary>
    private void Awake()
    {
        InitializeComponents();
    }

    /// <summary>컴포넌트 참조 설정</summary>
    private void InitializeComponents()
    {
        playerController = GetComponent<PlayerController>();
        playerLockOn = GetComponent<PlayerLockOn>();
        anim = GetComponentInChildren<Animator>();

        // Ability 컴포넌트 참조 설정
        if (swordIndicator == null)
        {
            swordIndicator = GetComponent<SwordAbility>();
            if (swordIndicator == null)
            {
                swordIndicator = FindFirstObjectByType<SwordAbility>();
            }
        }
    }

    /// <summary>매 프레임 업데이트</summary>
    private void Update()
    {
        HandleCooldowns();
        HandleInput();
    }

    /// <summary>쿨다운 처리</summary>
    private void HandleCooldowns()
    {
        // Q스킬 쿨다운 처리
        if (!isQSkillReady)
        {
            qCooldownTimer += Time.deltaTime;
            float adjustedCooldown = GetAdjustedCooldown(qSkillCooldown);

            if (qCooldownTimer >= adjustedCooldown)
            {
                isQSkillReady = true;
                qCooldownTimer = 0f;
            }
        }

        // E스킬 쿨다운 처리
        if (!isESkillReady)
        {
            eCooldownTimer += Time.deltaTime;
            float adjustedCooldown = GetAdjustedCooldown(eSkillCooldown);

            if (eCooldownTimer >= adjustedCooldown)
            {
                isESkillReady = true;
                eCooldownTimer = 0f;
                eSkillState = ESkillState.Ready;
            }
        }
    }

    private float GetAdjustedCooldown(float baseCooldown)
    {
        return baseCooldown;
    }

    /// <summary>입력 처리</summary>
    private void HandleInput()
    {
        // Q스킬 입력
        if (Input.GetButtonDown("Skill_Q") && CanUseQSkill())
        {
            CastSwordSlash();
        }

        // E스킬 입력 처리
        HandleESkillInput();
    }

    /// <summary>E스킬 입력 상태별 처리</summary>
    private void HandleESkillInput()
    {
        switch (eSkillState)
        {
            case ESkillState.Ready:
                if (Input.GetKeyDown(KeyCode.E) && CanUseESkill())
                {
                    StartESkillTargeting();
                }
                break;

            case ESkillState.ShowingIndicator:
                if (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
                    ConfirmESkill();
                else if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                    CancelESkillTargeting();
                break;

            case ESkillState.Casting:
                if (Input.GetButtonDown("Dash"))
                    CancelESkillCasting();
                break;
        }
    }

    // =============================================================================
    // IWeaponSkill 인터페이스 구현
    // =============================================================================

    /// <summary>Q스킬 사용 (인터페이스 구현)</summary>
    public void UseQSkill()
    {
        CastSwordSlash();
    }

    /// <summary>E스킬 사용 (인터페이스 구현)</summary>
    public void UseESkill()
    {
        if (eSkillState == ESkillState.Ready && CanUseESkill())
        {
            StartESkillTargeting();
        }
        else if (eSkillState == ESkillState.ShowingIndicator)
        {
            ConfirmESkill();
        }
    }

    /// <summary>쿨다운 상태 반환 (인터페이스 구현)</summary>
    public object GetCooldownStatus()
    {
        return new
        {
            qReady = isQSkillReady,
            eReady = isESkillReady,
            qTime = qCooldownTimer,
            eTime = eCooldownTimer
        };
    }

    // =============================================================================
    // Q스킬: 검기 발사
    // =============================================================================

    /// <summary>Q스킬 사용 가능 여부 확인</summary>
    public bool CanUseQSkill()
    {
        // 기본 조건 체크
        if (!isQSkillReady || playerController.isDodging || playerController.isSkillCasting)
        {
            return false;
        }

        return true;
    }

    /// <summary>Q스킬: 검기 발사 실행</summary>
    public void CastSwordSlash()
    {
        if (!CanUseQSkill()) return;

        Debug.Log("[PlayerSwordSkill] Q스킬: 검기 발사");

        // 쿨다운 시작
        isQSkillReady = false;
        qCooldownTimer = 0f;
        playerController.isSkillCasting = true;

        // 스킬 방향 결정
        Vector3 skillDirection = DetermineSkillDirection();

        // 플레이어 회전
        if (skillDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(skillDirection);
        }

        // 인디케이터 표시
        ShowQSkillIndicator(skillDirection);

        // 애니메이션 실행
        anim?.SetTrigger(qSkillAnimTrigger);

        // UI 쿨다운 시작
        NotifySkillCooldown(0, qSkillCooldown);
    }

    /// <summary>Q스킬 인디케이터 표시</summary>
    private void ShowQSkillIndicator(Vector3 direction)
    {
        if (swordIndicator != null)
        {
            swordIndicator.ShowQSkillIndicator(direction, 0.2f);
        }
    }

    /// <summary>Q스킬 실제 실행 (애니메이션 이벤트에서 호출)</summary>
    public void ExecuteSwordSlash()
    {
        // VFX 실행
        PlaySlashVFX();

        // 검기 투사체 발사
        Vector3 origin = vfxSpawnPoint != null ? vfxSpawnPoint.position : skillOrigin.position;
        Vector3 direction = GetSkillDirection();
        StartCoroutine(SlashProjectileRoutine(origin, direction.normalized));

        Debug.Log("[PlayerSwordSkill] 검기 투사체 발사");
    }

    /// <summary>검기 VFX 실행</summary>
    private void PlaySlashVFX()
    {
        if (slashVFXGraph != null)
        {
            Vector3 spawnPos = vfxSpawnPoint != null ? vfxSpawnPoint.position : skillOrigin.position;
            Vector3 skillDirection = GetSkillDirection();

            slashVFXGraph.transform.position = spawnPos;
            if (skillDirection != Vector3.zero)
            {
                slashVFXGraph.transform.rotation = Quaternion.LookRotation(skillDirection);
            }

            // VFX 이동
            StartCoroutine(MoveVFXForward(skillDirection));
            slashVFXGraph.Play();
        }
    }

    /// <summary>VFX 전방 이동</summary>
    private IEnumerator MoveVFXForward(Vector3 direction)
    {
        Vector3 startPos = slashVFXGraph.transform.position;
        float moveDuration = qSkillRange / Mathf.Max(0.01f, qSkillSpeed);
        direction = direction.normalized;

        float elapsedTime = 0f;
        while (elapsedTime < moveDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / moveDuration;
            slashVFXGraph.transform.position = startPos + direction * (qSkillRange * progress);
            yield return null;
        }
    }

    /// <summary>검기 투사체 처리</summary>
    private IEnumerator SlashProjectileRoutine(Vector3 origin, Vector3 direction)
    {
        float travelled = 0f;
        Vector3 currentPos = origin;
        var hitTargets = new System.Collections.Generic.HashSet<Enemy>();

        while (travelled < qSkillRange)
        {
            float step = qSkillSpeed * Time.deltaTime;
            Vector3 nextPos = currentPos + direction * step;

            // 적 탐지
            var colliders = Physics.OverlapSphere(currentPos, 0.6f, qSkillEnemyLayer);
            foreach (var col in colliders)
            {
                var enemy = col.GetComponentInParent<Enemy>();
                if (enemy != null && !hitTargets.Contains(enemy))
                {
                    hitTargets.Add(enemy);
                    enemy.ApplySkillDamage(qSkillDamage, HitEffectType.QSkill);
                }
            }

            currentPos = nextPos;
            travelled += step;
            yield return null;
        }

        Debug.Log($"[PlayerSwordSkill] 검기 완료: {hitTargets.Count}명 적중");
    }

    // =============================================================================
    // E스킬: 범위 공격
    // =============================================================================

    /// <summary>E스킬 사용 가능 여부 확인</summary>
    public bool CanUseESkill()
    {
        return isESkillReady && !playerController.isDodging && !playerController.isSkillCasting;
    }

    /// <summary>E스킬 타겟팅 시작</summary>
    private void StartESkillTargeting()
    {
        Debug.Log("[PlayerSwordSkill] E스킬 타겟팅 시작");

        eSkillState = ESkillState.ShowingIndicator;
        DetermineESkillTargetPosition();

        // 인디케이터 표시
        if (swordIndicator != null)
        {
            swordIndicator.ShowESkillIndicator(eSkillTargetPosition);
        }
    }

    /// <summary>E스킬 타겟 위치 결정</summary>
    private void DetermineESkillTargetPosition()
    {
        // 락온 타겟이 있으면 해당 위치로
        if (playerLockOn != null && playerLockOn.isLockOn && playerLockOn.currentTarget != null)
        {
            eSkillTargetPosition = playerLockOn.currentTarget.position;
        }
        else
        {
            // 마우스 위치로
            eSkillTargetPosition = playerController.GetMouseWorldPosition();
        }
    }

    /// <summary>E스킬 확정</summary>
    private void ConfirmESkill()
    {
        Debug.Log("[PlayerSwordSkill] E스킬 확정 - 시전 시작");

        // 인디케이터 숨기기
        if (swordIndicator != null)
        {
            swordIndicator.HideESkillIndicator();
        }

        // 최종 타겟 위치 업데이트
        if (playerLockOn == null || !playerLockOn.isLockOn)
        {
            eSkillTargetPosition = playerController.GetMouseWorldPosition();
        }

        StartESkillCasting();
    }

    /// <summary>E스킬 타겟팅 취소</summary>
    private void CancelESkillTargeting()
    {
        Debug.Log("[PlayerSwordSkill] E스킬 타겟팅 취소");

        eSkillState = ESkillState.Ready;
        if (swordIndicator != null)
        {
            swordIndicator.HideESkillIndicator();
        }
    }

    /// <summary>E스킬 시전 시작</summary>
    private void StartESkillCasting()
    {
        eSkillState = ESkillState.Casting;
        playerController.isSkillCasting = true;

        // 쿨다운 시작
        isESkillReady = false;
        eCooldownTimer = 0f;

        // 타겟 방향으로 회전
        Vector3 directionToTarget = (eSkillTargetPosition - transform.position).normalized;
        directionToTarget.y = 0;
        if (directionToTarget != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(directionToTarget);
        }

        // 애니메이션 시작
        anim?.SetTrigger(eSkillAnimTrigger);

        // 시전 코루틴 시작
        eSkillCastCoroutine = StartCoroutine(ESkillCastingRoutine());

        // UI 쿨다운 시작
        NotifySkillCooldown(1, eSkillCooldown);
    }

    /// <summary>E스킬 시전 루틴</summary>
    private IEnumerator ESkillCastingRoutine()
    {
        yield return new WaitForSeconds(eSkillCastTime);
        ExecuteGroundStrike();
    }

    /// <summary>E스킬 실행 (애니메이션 이벤트에서도 호출 가능)</summary>
    public void ExecuteGroundStrike()
    {
        Debug.Log("[PlayerSwordSkill] E스킬: 범위 공격 실행");

        // 범위 내 적에게 데미지 적용
        ApplyESkillDamage();

        // 이펙트 생성
        if (eSkillEffect != null)
        {
            Instantiate(eSkillEffect, eSkillTargetPosition, Quaternion.identity);
        }

        // 스킬 완료 처리
        CompleteESkill();
    }

    /// <summary>E스킬 데미지 적용</summary>
    private void ApplyESkillDamage()
    {
        Collider[] targets = Physics.OverlapSphere(eSkillTargetPosition, eSkillRadius, qSkillEnemyLayer);

        foreach (Collider col in targets)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.ApplySkillDamage(eSkillDamage, HitEffectType.ESkill);
            }
        }

        Debug.Log($"[PlayerSwordSkill] E스킬 완료: {targets.Length}명 적중");
    }

    /// <summary>E스킬 완료 처리</summary>
    private void CompleteESkill()
    {
        eSkillState = ESkillState.Cooldown;
        playerController.isSkillCasting = false;
    }

    /// <summary>E스킬 시전 취소</summary>
    private void CancelESkillCasting()
    {
        Debug.Log("[PlayerSwordSkill] E스킬 시전 취소");

        if (eSkillCastCoroutine != null)
        {
            StopCoroutine(eSkillCastCoroutine);
            eSkillCastCoroutine = null;
        }

        // 쿨다운 강제 적용
        eSkillState = ESkillState.Cooldown;
        playerController.isSkillCasting = false;
    }

    /// <summary>스킬 종료</summary>
    public void EndSkill()
    {
        if (playerController != null)
        {
            playerController.isSkillCasting = false;
        }
    }

    // =============================================================================
    // 유틸리티 메서드
    // =============================================================================

    /// <summary>스킬 방향 결정</summary>
    private Vector3 DetermineSkillDirection()
    {
        if (playerLockOn != null && playerLockOn.isLockOn && playerLockOn.currentTarget != null)
        {
            Vector3 direction = (playerLockOn.currentTarget.position - transform.position).normalized;
            direction.y = 0;
            return direction;
        }
        else
        {
            return playerController.GetMouseDirection();
        }
    }

    /// <summary>현재 스킬 방향 반환</summary>
    private Vector3 GetSkillDirection()
    {
        return DetermineSkillDirection();
    }

    /// <summary>UI에 쿨다운 알림</summary>
    private void NotifySkillCooldown(int skillIndex, float duration)
    {
        CombatUIManager.Instance?.StartSkillCoolDown(skillIndex, duration);
    }

    // =============================================================================
    // 공개 인터페이스 (WeaponManager에서 사용)
    // =============================================================================

    /// <summary>스킬 활성화</summary>
    public void ActivateSkills()
    {
        enabled = true;
        Debug.Log("[PlayerSwordSkill] 검 스킬 활성화");
    }

    /// <summary>스킬 비활성화</summary>
    public void DeactivateSkills()
    {
        enabled = false;

        // 진행중인 스킬 정리
        if (eSkillState == ESkillState.Casting && eSkillCastCoroutine != null)
        {
            StopCoroutine(eSkillCastCoroutine);
            eSkillCastCoroutine = null;
        }

        if (swordIndicator != null)
        {
            swordIndicator.HideESkillIndicator();
        }

        playerController.isSkillCasting = false;
        Debug.Log("[PlayerSwordSkill] 검 스킬 비활성화");
    }

    /// <summary>현재 쿨다운 상태 반환 (구체적인 반환 타입 - 내부 사용)</summary>
    public (bool qReady, bool eReady, float qTime, float eTime) GetDetailedCooldownStatus()
    {
        return (isQSkillReady, isESkillReady, qCooldownTimer, eCooldownTimer);
    }

    // =============================================================================
    // 디버그 기즈모
    // =============================================================================

    private void OnDrawGizmosSelected()
    {
        if (skillOrigin != null)
        {
            // Q스킬 범위
            Gizmos.color = Color.red;
            Vector3 direction = GetSkillDirection();
            Gizmos.DrawRay(skillOrigin.position, direction * qSkillRange);
            Gizmos.DrawWireCube(skillOrigin.position + direction * qSkillRange, Vector3.one * 0.5f);
        }

        // E스킬 범위
        if (eSkillState == ESkillState.ShowingIndicator || eSkillState == ESkillState.Casting)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(eSkillTargetPosition, eSkillRadius);
        }
    }
}
*/
