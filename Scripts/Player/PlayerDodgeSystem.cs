using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 회피 시스템 - 백스텝 회피
/// 우클릭 → 뒤로 슈욱 이동
/// 회피 중 좌클릭 → 대시 공격으로 전환 신호
/// </summary>
public class PlayerDodgeSystem : MonoBehaviour
{
    #region Serialized Fields
    [Header("Dodge Settings")]
    [SerializeField, Tooltip("백스텝 이동 거리")]
    private float dodgeDistance = 2.5f;

    [SerializeField, Tooltip("백스텝 지속 시간")]
    private float dodgeDuration = 0.4f;

    [SerializeField, Tooltip("회피 쿨다운")]
    private float dodgeCooldown = 1f;

    [Header("Invincibility Frame")]
    [SerializeField, Tooltip("무적 프레임 사용")]
    private bool useInvincibility = true;

    [SerializeField, Tooltip("무적 지속 시간")]
    private float invincibilityDuration = 0.2f;

    [Header("Collision")]
    [SerializeField, Tooltip("벽 레이어")]
    private LayerMask wallLayer = -1;

    [Header("Cancel to Attack")]
    [SerializeField, Tooltip("회피 중 공격 캔슬 허용")]
    private bool allowAttackCancel = true;

    [SerializeField, Tooltip("캔슬 가능 시작 시간")]
    private float cancelStartTime = 0.1f;
    #endregion

    #region Component References
    private Animator anim;
    private PlayerController playerController;
    private PlayerCombatStateMachine combatStateMachine;
    private Rigidbody rigid;
    #endregion

    #region Dodge State
    // 회피 상태
    private bool isDodging = false;
    private bool isDodgeReady = true;
    private float dodgeStartTime = 0f;

    // 무적 상태
    private bool isInvincible = false;

    // 입력 버퍼
    private bool attackInputBuffered = false;
    #endregion

    #region Public Properties
    /// <summary>현재 회피 중인지</summary>
    public bool IsDodging => isDodging;

    /// <summary>회피 가능 상태인지</summary>
    public bool IsDodgeReady => isDodgeReady;

    /// <summary>현재 무적 상태인지</summary>
    public bool IsInvincible => isInvincible;
    #endregion

    #region Unity Lifecycle
    /// <summary>컴포넌트 초기화</summary>
    private void Awake()
    {
        InitializeComponents();
    }

    /// <summary>매 프레임 업데이트</summary>
    private void Update()
    {
        // 회피 중 입력 감지
        if (isDodging)
        {
            CheckAttackInputDuringDodge();
        }
    }
    #endregion

    #region Initialization
    /// <summary>컴포넌트 참조 초기화</summary>
    private void InitializeComponents()
    {
        anim = GetComponentInChildren<Animator>();
        playerController = GetComponent<PlayerController>();
        combatStateMachine = GetComponent<PlayerCombatStateMachine>();
        rigid = GetComponent<Rigidbody>();

        if (anim == null)
            Debug.LogError("[PlayerDodgeSystem] Animator를 찾을 수 없습니다!");

        if (combatStateMachine == null)
            Debug.LogWarning("[PlayerDodgeSystem] PlayerCombatStateMachine이 없습니다.");

        Debug.Log("[PlayerDodgeSystem] 초기화 완료");
    }
    #endregion

    #region Public Interface
    /// <summary>외부에서 호출: 백스텝 회피 시작</summary>
    public void StartDodge()
    {
        if (!CanDodge())
        {
            Debug.Log("[PlayerDodgeSystem] 회피 불가능 상태");
            return;
        }

        // 회피 시작
        isDodging = true;
        isDodgeReady = false;
        dodgeStartTime = Time.time;
        attackInputBuffered = false;

        // 애니메이터 트리거
        if (anim != null)
        {
            ResetTriggers();
            anim.SetTrigger("BackStep");
        }

        // 백스텝 이동 실행
        PerformBackstep();

        // 무적 프레임 시작
        if (useInvincibility)
        {
            StartCoroutine(InvincibilityRoutine());
        }   

        // CombatStateMachine에 알림
        if (combatStateMachine != null)
        {
            //combatStateMachine.ForceChangeState(PlayerCombatStateMachine.CombatState.Dodging);
        }

        // 회피 종료 타이머
        StartCoroutine(DodgeRoutine());

        Debug.Log("[PlayerDodgeSystem] 백스텝 회피 시작!");
    }

    /// <summary>회피 가능 여부 확인</summary>
    public bool CanDodge()
    {
        if (playerController == null)
            return false;

        return !isDodging &&
               isDodgeReady &&
               !playerController.isSkillCasting;
    }

    /// <summary>회피 강제 취소</summary>
    public void CancelDodge()
    {
        if (!isDodging) return;

        EndDodge();
        Debug.Log("[PlayerDodgeSystem] 회피 강제 취소");
    }
    #endregion

    #region Dodge Execution
    /// <summary>백스텝 이동 실행</summary>
    private void PerformBackstep()
    {
        // 뒤 방향 계산
        Vector3 backDirection = -transform.forward;
        backDirection.y = 0;
        backDirection.Normalize();

        // 목표 위치 계산
        Vector3 targetPosition = transform.position + backDirection * dodgeDistance;
        targetPosition.y = transform.position.y; // Y축 고정

        // 벽 충돌 체크
        CheckWallCollision(ref targetPosition, backDirection);

        // Rigidbody를 직접 사용한 이동 (PlayerController와 충돌 방지)
        if (rigid != null)
        {
            StartCoroutine(BackstepMovementRoutine(targetPosition));
        }

        Debug.Log($"[PlayerDodgeSystem] 백스텝: {transform.position} → {targetPosition}");
    }

    /// <summary>백스텝 이동 루틴 (Rigidbody 기반)</summary>
    private IEnumerator BackstepMovementRoutine(Vector3 targetPosition)
    {
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < dodgeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / dodgeDuration;

            // Ease.OutCubic 곡선 직접 구현
            t = 1f - Mathf.Pow(1f - t, 3f);

            // 위치 보간
            Vector3 newPosition = Vector3.Lerp(startPosition, targetPosition, t);
            newPosition.y = transform.position.y; // Y축 고정 유지

            // Rigidbody를 사용한 이동
            rigid.MovePosition(newPosition);

            yield return null;
        }

        // 최종 위치 확정
        rigid.MovePosition(targetPosition);

        Debug.Log("[PlayerDodgeSystem] 백스텝 이동 완료");
    }

    /// <summary>벽 충돌 체크 및 보정</summary>
    private void CheckWallCollision(ref Vector3 targetPosition, Vector3 direction)
    {
        float distance = dodgeDistance;

        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, distance, wallLayer))
        {
            // 벽 앞에서 정지하도록 보정
            targetPosition = hit.point - direction * 0.2f;
            Debug.Log("[PlayerDodgeSystem] 벽 감지 - 이동 거리 보정");
        }
    }

    /// <summary>회피 루틴</summary>
    private IEnumerator DodgeRoutine()
    {
        // 회피 지속 시간 대기
        yield return new WaitForSeconds(dodgeDuration);

        // 회피 종료
        EndDodge();
    }

    /// <summary>회피 종료</summary>
    private void EndDodge()
    {
        isDodging = false;
        attackInputBuffered = false;

        // CombatStateMachine에 알림
        if (combatStateMachine != null)
        {
            combatStateMachine.ReturnToIdle();
        }

        // 쿨다운 후 다시 회피 가능
        StartCoroutine(DodgeCooldownRoutine());

        Debug.Log("[PlayerDodgeSystem] 회피 종료");
    }

    /// <summary>회피 쿨다운</summary>
    private IEnumerator DodgeCooldownRoutine()
    {
        isDodgeReady = false;
        yield return new WaitForSeconds(dodgeCooldown);
        isDodgeReady = true;

        Debug.Log("[PlayerDodgeSystem] 회피 준비 완료");
    }
    #endregion

    #region Invincibility
    /// <summary>무적 프레임 루틴</summary>
    private IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        Debug.Log("[PlayerDodgeSystem] 무적 시작");

        yield return new WaitForSeconds(invincibilityDuration);

        isInvincible = false;
        Debug.Log("[PlayerDodgeSystem] 무적 종료");
    }
    #endregion

    #region Attack Cancel Detection
    /// <summary>회피 중 공격 입력 감지</summary>
    private void CheckAttackInputDuringDodge()
    {
        if (!allowAttackCancel)
            return;

        // 캔슬 가능 시간 체크
        float elapsedTime = Time.time - dodgeStartTime;
        if (elapsedTime < cancelStartTime)
            return;

        // 좌클릭 감지
        if (Input.GetMouseButtonDown(0))
        {
            attackInputBuffered = true;
            Debug.Log("[PlayerDodgeSystem] 회피 중 공격 입력 감지!");

            // TODO: 대시 공격으로 전환 (Step 4에서 구현)
            // 지금은 입력만 감지하고 버퍼에 저장
        }
    }

    /// <summary>버퍼된 공격 입력 확인 (외부에서 호출 가능)</summary>
    public bool HasBufferedAttackInput()
    {
        return attackInputBuffered;
    }

    /// <summary>버퍼된 공격 입력 소비</summary>
    public void ConsumeBufferedAttackInput()
    {
        attackInputBuffered = false;
    }
    #endregion

    #region Utility
    /// <summary>애니메이터 트리거 리셋</summary>
    private void ResetTriggers()
    {
        if (anim == null)
            return;

        anim.ResetTrigger("Attack1");
        anim.ResetTrigger("Attack2");
        anim.ResetTrigger("Attack3");
        anim.ResetTrigger("Dodge");
        anim.ResetTrigger("BackStep");
        anim.ResetTrigger("Skill_Q");
        anim.ResetTrigger("Skill_E");
    }
    #endregion

    #region Debug Visualization
    /// <summary>회피 방향 시각화</summary>
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
            return;

        // 백스텝 방향 표시
        Gizmos.color = Color.cyan;
        Vector3 backDir = -transform.forward * dodgeDistance;
        Gizmos.DrawLine(transform.position, transform.position + backDir);

        // 회피 중이면 초록색
        if (isDodging)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }

        // 무적 중이면 노란색
        if (isInvincible)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.3f);
        }
    }

    /// <summary>디버그 정보 표시</summary>
    private void OnGUI()
    {
        if (!Application.isPlaying)
            return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 18;
        style.normal.textColor = Color.cyan;

        int yOffset = 130; // CombatStateMachine 아래에 표시

        if (isDodging)
        {
            GUI.Label(new Rect(10, yOffset, 300, 30), "Dodging...", style);
            yOffset += 30;
        }

        if (isInvincible)
        {
            style.normal.textColor = Color.yellow;
            GUI.Label(new Rect(10, yOffset, 300, 30), "Invincible!", style);
            yOffset += 30;
        }

        if (attackInputBuffered)
        {
            style.normal.textColor = Color.red;
            GUI.Label(new Rect(10, yOffset, 300, 30), "Attack Input Buffered", style);
        }
    }
    #endregion
}
