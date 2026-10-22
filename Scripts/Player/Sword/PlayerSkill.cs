/*using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// 플레이어 스킬 시스템 - 쿼터뷰 지상 전용
/// Y축 관련 기능 제거
/// </summary>
public class PlayerSkill : MonoBehaviour
{
    [Header("Q Skill Settings")]
    public float skillCoolDown = 10f;
    float coolDownTimer = 0f;
    public bool isSkillReady = true;

    [Header("E Skill Settings")]
    public float eSkillCoolDown = 5f;
    float eSkillCoolDownTimer = 0f;
    public bool isESkillReady = true;
    public float eSkillRadius = 4f;                    // E스킬 범위
    public int eSkillDamage = 40;                      // E스킬 데미지
    public float eSkillCastTime = 1.5f;                // 캐스팅 시간
    public GameObject eSkillEffect;                    // E스킬 이펙트

    [Header("Skill Range")]
    public float skillRadius = 5f;
    public float skillAngle = 60f;
    public LayerMask enemyLayer;

    [Header("VFX Graph System")]
    public VisualEffect slashVFXGraph;
    public Transform vfxSpawnPoint;

    [Header("검기 투사체 설정")]
    public float slashProjectileRange = 10f;
    public float slashSpeed = 20f;
    public int slashProjectileDamage = 20;
    public LayerMask projectileEnemyLayer;
    public float slashProjectileRadius = 0.6f;

    [Header("Effects")]
    public GameObject skillSlashEffect;

    [Header("References")]
    public Animator anim;
    public Transform skillOrigin;
    public int skillDamage = 30;

    [Header("Indicator System")]
    [SerializeField, Tooltip("스킬 인디케이터 시스템 (Ability 컴포넌트)")]
    private Ability abilityIndicator;

    [SerializeField, Tooltip("스킬 1 인디케이터 표시 시간")]
    private float skill1IndicatorDuration = 0.2f;

    public enum ESkillState
    {
        Ready,
        ShowingIndicator,
        Casting,
        Cooldown
    };

    ESkillState eSkillState = ESkillState.Ready;
    Vector3 eSkillTargetPosition; // E 스킬 타겟 위치
    Coroutine eSkillCastCoroutine; // E 스킬 캐스팅 코루틴

    // 컴포넌트 참조
    PlayerController playerController;
    PlayerLockOn playerLockOn;

    bool qDown;
    bool eDown;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        playerLockOn = GetComponent<PlayerLockOn>();

        // Ability 컴포넌트 자동 찾기
        if (abilityIndicator == null)
        {
            abilityIndicator = GetComponent<Ability>();
            if (abilityIndicator == null)
            {
                abilityIndicator = FindFirstObjectByType<Ability>();
            }
        }
    }

    private void Update()
    {
        GetInput();
        HandleCoolDown();
        HandleESkillInput();

        // Q 스킬
        if (qDown && isSkillReady)
            UseSkill();
    }

    void GetInput()
    {
        qDown = Input.GetButtonDown("Skill_Q");
        eDown = Input.GetKeyDown(KeyCode.E);
    }

    void HandleCoolDown()
    {
        // Q 스킬 쿨다운 적용
        if (!isSkillReady)
        {
            coolDownTimer += Time.deltaTime;

            // 추가 스탯으로 얻은 쿨다운 감소 적용
            float cooldown = skillCoolDown;
            if (PlayerStatsManager.Instance != null)
            {
                float reduction = PlayerStatsManager.Instance.GetCurrentStatValue(StatType.SkillCooldown);
                cooldown = skillCoolDown * (1f - (reduction / 100f));
            }

            if (coolDownTimer >= cooldown)
            {
                isSkillReady = true;
                coolDownTimer = 0f;
            }
        }

        // E 스킬 쿨다운 적용
        if (!isESkillReady)
        {
            eSkillCoolDownTimer += Time.deltaTime;

            float eCooldown = eSkillCoolDown;
            if (PlayerStatsManager.Instance != null)
            {
                float reduction = PlayerStatsManager.Instance.GetCurrentStatValue(StatType.SkillCooldown);
                eCooldown = eSkillCoolDown * (1f - (reduction / 100f));
            }

            if (eSkillCoolDownTimer >= eCooldown)
            {
                isESkillReady = true;
                eSkillCoolDownTimer = 0f;
                eSkillState = ESkillState.Ready;
            }
        }
    }

    // =============================================================================
    // E스킬 시스템
    // =============================================================================

    void HandleESkillInput()
    {
        switch (eSkillState)
        {
            case ESkillState.Ready:
                if (eDown && CanUseESkill())
                {
                    StartESkillTargeting();
                }
                break;

            case ESkillState.ShowingIndicator:
                // E 다시 누르거나 좌클릭으로 발동
                if (eDown || Input.GetMouseButtonDown(0))
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

    bool CanUseESkill()
    {
        if (!isESkillReady)
        {
            Debug.Log("[E스킬] 쿨다운 중입니다!");
            return false;
        }

        if (playerController.isDodging)
        {
            Debug.Log("[E스킬] 닷지 중에는 사용할 수 없습니다!");
            return false;
        }

        if (playerController.isSkillCasting)
        {
            Debug.Log("[E스킬] 다른 스킬 사용 중입니다!");
            return false;
        }

        return true;
    }

    void StartESkillTargeting()
    {
        Debug.Log("[E스킬] 타겟팅 시작");

        eSkillState = ESkillState.ShowingIndicator;

        // 타겟 위치 결정
        DetermineESkillTargetPosition();

        // Ability에게 E스킬 인디케이터 표시 요청
        if (abilityIndicator != null)
        {
            abilityIndicator.ShowESkillIndicator(eSkillTargetPosition);
        }
    }

    void DetermineESkillTargetPosition()
    {
        // 락온 타겟이 있으면 그 위치로
        if (playerLockOn != null && playerLockOn.isLockOn && playerLockOn.currentTarget != null)
        {
            eSkillTargetPosition = playerLockOn.currentTarget.position;
            Debug.Log($"[E스킬] 락온 타겟 위치로 설정: {playerLockOn.currentTarget.name}");
        }
        else
        {
            // 마우스 위치로 (Ability가 자동으로 추적함)
            eSkillTargetPosition = playerController.GetMouseWorldPosition();
            Debug.Log("[E스킬] 마우스 위치로 설정");
        }
    }

    void ConfirmESkill()
    {
        Debug.Log("[E스킬] 스킬 확정 - 캐스팅 시작");

        // 인디케이터 숨기기
        if (abilityIndicator != null)
        {
            abilityIndicator.HideESkillIndicator();
        }

        // 최종 타겟 위치 갱신 (마우스 추적 모드였다면)
        if (playerLockOn == null || !playerLockOn.isLockOn)
        {
            eSkillTargetPosition = playerController.GetMouseWorldPosition();
        }

        // 캐스팅 시작
        StartESkillCasting();
    }

    void CancelESkillTargeting()
    {
        Debug.Log("[E스킬] 타겟팅 취소");

        eSkillState = ESkillState.Ready;

        // 인디케이터 숨기기
        if (abilityIndicator != null)
        {
            abilityIndicator.HideESkillIndicator();
        }
    }

    void StartESkillCasting()
    {
        eSkillState = ESkillState.Casting;

        // 이동 제한 활성화
        playerController.isSkillCasting = true;

        // 타겟 방향으로 회전
        Vector3 directionToTarget = (eSkillTargetPosition - transform.position).normalized;
        directionToTarget.y = 0;
        if (directionToTarget != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(directionToTarget);
        }

        // 애니메이션 시작
        anim.SetTrigger("Skill_E");

        // 캐스팅 코루틴 시작
        eSkillCastCoroutine = StartCoroutine(ESkillCastingRoutine());

        Debug.Log($"[E스킬] 캐스팅 시작 - 타겟: {eSkillTargetPosition}");
    }

    IEnumerator ESkillCastingRoutine()
    {
        // 캐스팅 시간 대기
        yield return new WaitForSeconds(eSkillCastTime);

        // 스킬 실행
        ExecuteESkill();
    }

    // 애니메이션 이벤트에서 호출될 E스킬 실행 메서드 추가
    public void TriggerESkillSmash()
    {
        ExecuteESkill();
    }

    void ExecuteESkill()
    {
        Debug.Log("[E스킬] 실행!");

        // 타겟 위치에 범위 공격
        ApplyESkillDamage();

        // 이펙트 생성
        if (eSkillEffect != null)
        {
            Instantiate(eSkillEffect, eSkillTargetPosition, Quaternion.identity);
        }

        // 스킬 완료 처리
        CompleteESkill();
    }

    void ApplyESkillDamage()
    {
        Collider[] targets = Physics.OverlapSphere(eSkillTargetPosition, eSkillRadius, enemyLayer);

        foreach (Collider col in targets)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.ApplySkillDamage(eSkillDamage);
                Debug.Log($"[E스킬] {enemy.name}에게 {eSkillDamage} 피해");
            }
        }

        Debug.Log($"[E스킬] 범위 공격 완료 - {targets.Length}명 적중");
    }

    void CompleteESkill()
    {
        // 쿨다운 시작
        isESkillReady = false;
        eSkillCoolDownTimer = 0f;
        eSkillState = ESkillState.Cooldown;

        // 이동 제한 해제
        playerController.isSkillCasting = false;

        // UI 쿨다운 시작
        CombatUIManager.Instance?.StartSkillCoolDown(1, eSkillCoolDown);

        Debug.Log("[E스킬] 완료 - 쿨다운 시작");
    }

    void CancelESkillCasting()
    {
        Debug.Log("[E스킬] 캐스팅 중 닷지로 캔슬!");

        // 코루틴 중단
        if (eSkillCastCoroutine != null)
        {
            StopCoroutine(eSkillCastCoroutine);
            eSkillCastCoroutine = null;
        }

        // 쿨다운 강제 적용 (5초)
        isESkillReady = false;
        eSkillCoolDownTimer = 0f;
        eSkillState = ESkillState.Cooldown;

        // 이동 제한 해제
        playerController.isSkillCasting = false;

        // UI 쿨다운 시작
        CombatUIManager.Instance?.StartSkillCoolDown(1, eSkillCoolDown);

        Debug.Log("[E스킬] 캔슬 완료 - 쿨다운 적용");
    }

    /// <summary>Q 스킬 사용 - 즉시 발동 + 인디케이터 표시</summary>
    void UseSkill()
    {
        // 쿨다운 시작
        isSkillReady = false;
        coolDownTimer = 0f;
        playerController.isSkillCasting = true;

        // 1. 스킬 방향 결정 (이 순간 고정됨)
        Vector3 finalSkillDirection = DetermineSkillDirection();

        // 2. 플레이어 회전
        if (finalSkillDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(finalSkillDirection);
        }

        // 3. 인디케이터 표시 요청 (Ability에게)
        ShowSkillIndicator(finalSkillDirection);

        // 4. 스킬 즉시 실행
        ExecuteSkillDirectly();

        // 5. UI 쿨다운 시작
        CombatUIManager.Instance?.StartSkillCoolDown(0, skillCoolDown);
    }

    /// <summary>스킬 방향 결정 - 락온 우선, 없으면 마우스</summary>
    Vector3 DetermineSkillDirection()
    {
        Vector3 skillDirection = Vector3.zero;

        // 락온 타겟이 있으면 우선
        if (playerLockOn != null && playerLockOn.isLockOn && playerLockOn.currentTarget != null)
        {
            skillDirection = (playerLockOn.currentTarget.position - transform.position).normalized;
            skillDirection.y = 0; // Y축 제거

            Debug.Log($"[PlayerSkill] 락온 방향으로 스킬 발동: {playerLockOn.currentTarget.name}");
        }
        else
        {
            // 마우스 방향
            skillDirection = playerController.GetMouseDirection();

            Debug.Log("[PlayerSkill] 마우스 방향으로 스킬 발동");
        }

        return skillDirection;
    }

    /// <summary>스킬 인디케이터 표시 - Ability 시스템 활용</summary>
    void ShowSkillIndicator(Vector3 direction)
    {
        if (abilityIndicator != null)
        {
            // Ability에게 Skill1 인디케이터 표시 요청
            abilityIndicator.ShowSkill1Indicator(direction, skill1IndicatorDuration);

            Debug.Log("[PlayerSkill] 스킬 인디케이터 표시 요청");
        }
        else
        {
            Debug.LogWarning("[PlayerSkill] Ability 컴포넌트를 찾을 수 없습니다!");
        }
    }

    /// <summary>스킬 직접 실행 - 지상 범위 공격만</summary>
    void ExecuteSkillDirectly()
    {
        // 애니메이션 트리거
        anim.SetTrigger("Skill_Q");

        Debug.Log("[PlayerSkill] 스킬 실행 - 애니메이션 시작");
    }

    public void TriggerSkillSmash()
    {
        // 메인 VFX Graph 실행
        PlaySlashVFX();

        Vector3 skillDirection = GetSkillDirection();

        if (skillDirection != Vector3.zero)
        {
            skillOrigin.forward = skillDirection;
        }

        // 검기 투사체 피해 (즉시 적용)
        Vector3 origin = (vfxSpawnPoint ? vfxSpawnPoint.position : skillOrigin.position);
        StartCoroutine(SlashProjectileRoutine(origin, skillDirection.normalized));

        // 범위 내 적들에게 데미지
        ApplyRangeDamage();
    }

    // XZ 평면에서 직선으로 이동하며 적 체크
    IEnumerator SlashProjectileRoutine(Vector3 origin, Vector3 dir)
    {
        float travelled = 0f;
        Vector3 prev = origin;
        var hitOnce = new HashSet<Enemy>();

        while (travelled < slashProjectileRange)
        {
            float step = slashSpeed * Time.deltaTime;
            Vector3 next = prev + dir * step;

            // XZ 평면에서 원형 범위로 적 탐지
            Vector3 checkPoint = Vector3.Lerp(prev, next, 0.5f);
            var cols = Physics.OverlapSphere(checkPoint, slashProjectileRadius, projectileEnemyLayer);

            foreach (var c in cols)
            {
                var enemy = c.GetComponentInParent<Enemy>();
                if (enemy != null && !hitOnce.Contains(enemy))
                {
                    hitOnce.Add(enemy);
                    enemy.ApplySkillDamage(slashProjectileDamage);
                }
            }

            Debug.DrawLine(prev, next, Color.cyan, 0.2f);

            prev = next;
            travelled += step;
            yield return null;
        }
    }

    void PlaySlashVFX()
    {
        if (slashVFXGraph != null)
        {
            // VFX 위치와 방향 설정
            Vector3 spawnPos = vfxSpawnPoint != null ? vfxSpawnPoint.position : skillOrigin.position;
            Vector3 skillDirection = GetSkillDirection();

            slashVFXGraph.transform.position = spawnPos;

            if (skillDirection != Vector3.zero)
            {
                slashVFXGraph.transform.rotation = Quaternion.LookRotation(skillDirection);
            }

            // VFX를 앞으로 이동시키는 코루틴 시작
            StartCoroutine(MoveVFXForward(skillDirection));

            // VFX 실행
            slashVFXGraph.Play();

            Debug.Log("[VFX] 완전 검기 이펙트 실행!");
        }
    }

    // VFX를 앞으로 이동시키는 코루틴
    IEnumerator MoveVFXForward(Vector3 direction)
    {
        Vector3 startPos = slashVFXGraph.transform.position;

        float moveDistance = slashProjectileRange;
        float moveDuration = slashProjectileRange / Mathf.Max(0.01f, slashSpeed);
        direction = direction.normalized;

        float elapsedTime = 0f;
        while (elapsedTime < moveDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / moveDuration;

            slashVFXGraph.transform.position = startPos + direction * (moveDistance * progress);
            yield return null;
        }
    }

    // 범위 피해 (기존 로직 유지)
    void ApplyRangeDamage()
    {
        Collider[] targets = Physics.OverlapSphere(skillOrigin.position, skillRadius, enemyLayer);

        foreach (Collider col in targets)
        {
            Vector3 dir = (col.transform.position - skillOrigin.position).normalized;
            float angle = Vector3.Angle(skillOrigin.forward, dir);

            if (angle <= skillAngle * 0.5f)
            {
                Enemy enemy = col.GetComponent<Enemy>();
                if (enemy != null)
                {
                    enemy.ApplySkillDamage(skillDamage);
                }
            }
        }
    }

    Vector3 GetSkillDirection() // 스킬 방향 통합 함수
    {
        if (playerLockOn != null && playerLockOn.isLockOn && playerLockOn.currentTarget != null)
        {
            return (playerLockOn.currentTarget.position - transform.position).normalized;
        }
        else
            return playerController.GetMouseDirection();
    }

    public void EndSkill()
    {
        if (playerController != null)
            playerController.isSkillCasting = false;
    }

    // 디버깅용 - 스킬 범위 표시
    private void OnDrawGizmosSelected()
    {
        if (skillOrigin != null)
        {
            // 스킬 범위 (원형)
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(skillOrigin.position, skillRadius);

            // 검기 투사체 경로 (직선)
            Gizmos.color = Color.red;
            Vector3 projectileEnd = skillOrigin.position + skillOrigin.forward * slashProjectileRange;
            Gizmos.DrawLine(skillOrigin.position, projectileEnd);
            Gizmos.DrawWireCube(projectileEnd, Vector3.one * 0.5f);

            // 스킬 각도 범위
            Vector3 skillDirection = skillOrigin.forward;
            Vector3 leftBoundary = Quaternion.Euler(0, -skillAngle * 0.5f, 0) * skillDirection * skillRadius;
            Vector3 rightBoundary = Quaternion.Euler(0, skillAngle * 0.5f, 0) * skillDirection * skillRadius;

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(skillOrigin.position, leftBoundary);
            Gizmos.DrawRay(skillOrigin.position, rightBoundary);
        }

        // E스킬 범위 표시
        if (eSkillState == ESkillState.ShowingIndicator || eSkillState == ESkillState.Casting)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(eSkillTargetPosition, eSkillRadius);
        }
    }
}
*/
