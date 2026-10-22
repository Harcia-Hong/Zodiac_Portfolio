/*using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 플레이어 돌진 공격 - 쿼터뷰 직선 돌진 전용
/// Y축 관련 기능 및 대각선 돌진 제거
/// </summary>
public class PlayerRushAttack : MonoBehaviour
{
    [Header("Rush Attack Settings")]
    [SerializeField] private float rushDuration = 0.3f;        // 돌진 지속 시간
    [SerializeField] private float rushCoolDown = 2.5f;        // 돌진 쿨다운
    [SerializeField] private float maxRushDistance = 15f;      // 최대 돌진 거리
    [SerializeField] private float minRushDistance = 2f;       // 최소 돌진 거리

    [Header("Safety Systems")]
    [SerializeField] private LayerMask wallLayer = -1;         // 벽 레이어
    [SerializeField] private float wallCheckDistance = 1f;     // 벽 체크 거리
    [SerializeField] private float targetValidCheckInterval = 0.1f;  // 타겟 유효성 체크 간격

    [Header("VFX & Audio")]
    [SerializeField] private GameObject rushStartEffect;       // 돌진 시작 이펙트
    [SerializeField] private GameObject rushEndEffect;         // 돌진 종료 이펙트
    [SerializeField] private TrailRenderer rushTrail;          // 돌진 궤적

    [Header("References")]
    [SerializeField] private Weapon weapon;

    // 컴포넌트 참조
    private PlayerController playerController;
    private PlayerAttackManager attackManager;
    private PlayerLockOn playerLockOn;
    private Rigidbody rigid;
    private Animator anim;

    // 돌진 상태 관리
    public bool isRushing { get; private set; } = false;
    private bool rushAttackReady = true;
    private float rushCooldownTimer = 0f;

    // 돌진 데이터
    private Transform currentTarget;
    private Vector3 rushStartPos;
    private Vector3 rushTargetPos;

    // 안전장치
    private Coroutine targetValidationCoroutine;
    private Coroutine rushExecutionCoroutine;

    /// <summary>컴포넌트 초기화</summary>
    private void Awake()
    {
        InitializeComponents();
    }

    /// <summary>컴포넌트 참조 설정</summary>
    private void InitializeComponents()
    {
        playerController = GetComponent<PlayerController>();
        attackManager = GetComponent<PlayerAttackManager>();
        playerLockOn = GetComponent<PlayerLockOn>();
        rigid = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();

        // 트레일 초기 비활성화
        if (rushTrail != null)
            rushTrail.enabled = false;

        if (weapon == null)
            weapon = GetComponentInChildren<Weapon>();
    }

    private void Update()
    {
        UpdateCooldown();
        HandleCancelInput();
    }

    /// <summary>쿨다운 타이머 업데이트</summary>
    private void UpdateCooldown()
    {
        if (!rushAttackReady)
        {
            rushCooldownTimer += Time.deltaTime;
            if (rushCooldownTimer >= rushCoolDown)
            {
                rushAttackReady = true;
                rushCooldownTimer = 0f;
            }
        }
    }

    /// <summary>돌진 캔슬 입력 처리 (Shift키)</summary>
    private void HandleCancelInput()
    {
        if (isRushing && Input.GetKeyDown(KeyCode.LeftShift))
        {
            CancelRushAttack();
            TriggerDodgeAfterCancel();
        }
    }

    /// <summary>돌진 공격 시작</summary>
    public void StartRushAttack(Transform target)
    {
        if (!CanStartRushAttack(target)) return;

        currentTarget = target;
        ExecuteRushAttack();
    }

    /// <summary>돌진 가능 여부 확인</summary>
    public bool CanRushAttack() => rushAttackReady && !isRushing;

    /// <summary>돌진 강제 취소</summary>
    public void CancelRushAttack()
    {
        if (!isRushing) return;

        StopAllRushCoroutines();
        DOTween.Kill(transform);

        isRushing = false;
        currentTarget = null;

        weapon?.DisableHitBox();
        DisableRushEffects();
        RestorePhysics();

        Debug.Log("돌진 공격 취소됨");
    }

    /// <summary>현재 돌진 중인지 확인</summary>
    public bool IsRushing() => isRushing;

    /// <summary>돌진 공격 실행 가능 여부 확인</summary>
    private bool CanStartRushAttack(Transform target)
    {
        if (!rushAttackReady)
        {
            Debug.Log("돌진 쿨다운 중");
            return false;
        }

        if (isRushing)
        {
            Debug.Log("이미 돌진 중");
            return false;
        }

        if (target == null)
        {
            Debug.Log("타겟이 null");
            return false;
        }

        if (!IsTargetValid(target))
        {
            Debug.Log("유효하지 않은 타겟");
            return false;
        }

        float distance = Vector3.Distance(transform.position, target.position);
        if (distance < minRushDistance)
        {
            Debug.Log("타겟이 너무 가까움");
            return false;
        }

        if (distance > maxRushDistance)
        {
            Debug.Log("타겟이 너무 멀음");
            return false;
        }

        return true;
    }

    /// <summary>타겟 유효성 확인</summary>
    private bool IsTargetValid(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        Enemy enemy = target.GetComponent<Enemy>();
        if (enemy != null)
        {
            if (enemy.isDead)
               return false;

            if (enemy.gameObject.layer == 10)
                return false;
        }

        return true;
    }

    /// <summary>돌진 공격 실행</summary>
    private void ExecuteRushAttack()
    {
        isRushing = true;
        rushAttackReady = false;
        rushCooldownTimer = 0f;

        rushStartPos = transform.position;
        CalculateRushPath();
        AdjustPathForWallCollision();
        RotateTowardsTarget();
        StartRushEffects();
        StartPhysicsControl();

        rushExecutionCoroutine = StartCoroutine(ExecuteRushMovement());
        targetValidationCoroutine = StartCoroutine(ValidateTargetDuringRush());

        Debug.Log($"돌진 시작: {currentTarget.name}");
    }

    /// <summary>돌진 경로 계산 - 직선만</summary>
    private void CalculateRushPath()
    {
        // 시작/타겟 위치 확보
        Vector3 start = rushStartPos;                    // 돌진 시작 지점
        Vector3 targetPos = (currentTarget != null)
            ? currentTarget.position
            : start + transform.forward * maxRushDistance;

        // 쿼터뷰: 같은 Y(높이)에서만 이동하도록 평면 스냅
        float fixedY = transform.position.y;
        start.y = fixedY;
        targetPos.y = fixedY;

        // RushDistance(%) 스탯 적용
        float finalMax = maxRushDistance;
        if (PlayerStatsManager.Instance != null)
        {
            float bonusPct = PlayerStatsManager.Instance.GetCurrentStatValue(RewardData.StatType.RushDistance);
            finalMax = maxRushDistance * (1f + (bonusPct / 100f));
        }

        // XZ 평면 방향/거리 계산
        Vector3 dir = targetPos - start;
        dir.y = 0f;                                      // 평면화
        float dist = dir.magnitude;

        if (dist > 0.0001f) dir /= dist;                 // 정규화
        float moveDist = Mathf.Min(dist, finalMax);      // 최대거리 제한

        // 최종 목적지 (Y 고정)
        rushTargetPos = start + dir * moveDist;          // rushTargetPos.y == fixedY
    }

    /// <summary>벽 충돌 체크 및 경로 보정</summary>
    private void AdjustPathForWallCollision()
    {
        // 방향/거리 계산 (XZ 평면)
        Vector3 start = rushStartPos;
        float fixedY = transform.position.y;
        start.y = fixedY;

        Vector3 dir = rushTargetPos - start;
        dir.y = 0f;                               // 평면화
        float dist = dir.magnitude;
        if (dist < 0.0001f) return;
        dir /= dist;

        // 벽과 부딪히면, 살짝 뒤쪽으로 물린 안전 지점으로 조정
        if (Physics.Raycast(start, dir, out RaycastHit hit, dist, wallLayer))
        {
            Vector3 safe = hit.point - dir * wallCheckDistance; // 벽에서 살짝 떨어진 지점
            safe.y = fixedY;                                     // Y 고정
            rushTargetPos = safe;
            Debug.Log("벽 감지: 경로 조정됨 (XZ 스냅)");
        }
        else
        {
            // 혹시라도 Y가 틀어졌다면 고정
            rushTargetPos = new Vector3(rushTargetPos.x, fixedY, rushTargetPos.z);
        }
    }

    /// <summary>타겟 방향으로 회전</summary>
    private void RotateTowardsTarget()
    {
        Vector3 lookDirection = (currentTarget.position - transform.position).normalized;
        lookDirection.y = 0;

        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }

    /// <summary>돌진 이펙트 시작</summary>
    private void StartRushEffects()
    {
        if (rushStartEffect != null)
        {
            Instantiate(rushStartEffect, transform.position, transform.rotation);
        }

        if (rushTrail != null)
        {
            rushTrail.enabled = true;
            rushTrail.Clear();
        }

        if (weapon != null)
        {
            Vector3 dir = (rushTargetPos - rushStartPos);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                weapon.EnableHitBox(SlashVFXController.Mode.DirectionalBurst, dir);
            else
                weapon.EnableHitBox(SlashVFXController.Mode.DirectionalBurst, transform.forward);
        }

        if (anim != null)
        {
            anim.SetTrigger("doRushAttack");
        }
    }

    /// <summary>물리 제어 시작</summary>
    private void StartPhysicsControl()
    {
        if (rigid != null)
        {
            rigid.linearVelocity = Vector3.zero;
            rigid.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>돌진 이동 실행 코루틴</summary>
    private IEnumerator ExecuteRushMovement()
    {
        float elapsedTime = 0f;
        float fixedY = rushStartPos.y;                    // 시작 높이로 고정

        while (elapsedTime < rushDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / rushDuration;

            Vector3 pos = Vector3.Lerp(rushStartPos, rushTargetPos, t); // 선형 보간
            pos.y = fixedY;                                             // Y 고정
            transform.position = pos;

            yield return null;
        }

        // 종착점 스냅 & 종료 처리
        transform.position = new Vector3(rushTargetPos.x, fixedY, rushTargetPos.z);
        OnRushCompleted();
    }

    /// <summary>타겟 유효성 지속 체크 코루틴</summary>
    private IEnumerator ValidateTargetDuringRush()
    {
        while (isRushing)
        {
            if (!IsTargetValid(currentTarget))
            {
                Debug.Log("돌진 중 타겟 무효화됨 - 마지막 위치까지 돌진 계속");
                currentTarget = null;
                break;
            }

            yield return new WaitForSeconds(targetValidCheckInterval);
        }
    }

    /// <summary>돌진 완료 처리</summary>
    private void OnRushCompleted()
    {
        EndRushEffects();
        weapon?.DisableHitBox();
        RestorePhysics();

        isRushing = false;
        currentTarget = null;

        StopAllRushCoroutines();

        Debug.Log("돌진 완료");
    }

    /// <summary>돌진 이펙트 종료</summary>
    private void EndRushEffects()
    {
        if (rushEndEffect != null)
        {
            Instantiate(rushEndEffect, transform.position, transform.rotation);
        }

        if (rushTrail != null)
        {
            rushTrail.enabled = false;
        }
    }

    /// <summary>물리 상태 복구</summary>
    private void RestorePhysics()
    {
        if (rigid != null)
        {
            rigid.linearVelocity = Vector3.zero;
            rigid.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>돌진 이펙트 비활성화</summary>
    private void DisableRushEffects()
    {
        if (rushTrail != null)
        {
            rushTrail.enabled = false;
        }
    }

    /// <summary>모든 돌진 관련 코루틴 정지</summary>
    private void StopAllRushCoroutines()
    {
        if (targetValidationCoroutine != null)
        {
            StopCoroutine(targetValidationCoroutine);
            targetValidationCoroutine = null;
        }

        if (rushExecutionCoroutine != null)
        {
            StopCoroutine(rushExecutionCoroutine);
            rushExecutionCoroutine = null;
        }
    }

    /// <summary>돌진 취소 후 회피 실행</summary>
    private void TriggerDodgeAfterCancel()
    {
        if (playerController != null)
        {
            Debug.Log("돌진 취소 후 회피 실행");
        }
    }

    /// <summary>디버그 기즈모</summary>
    private void OnDrawGizmosSelected()
    {
        // 돌진 범위 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxRushDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, minRushDistance);

        // 돌진 경로 표시
        if (isRushing)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(rushStartPos, rushTargetPos);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(rushTargetPos, 0.5f);
        }

        // 벽 체크 레이 표시
        if (currentTarget != null)
        {
            Vector3 direction = (currentTarget.position - transform.position).normalized;
            float distance = Vector3.Distance(transform.position, currentTarget.position);

            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(transform.position, direction * distance);
        }
    }
}
*/
