using UnityEngine;

/// <summary>
/// 플레이어 전투 상태 머신 시스템
/// 공격, FlashSlash, 대시공격 상태를 관리하고 캔슬 시스템 제공
/// </summary>
public class PlayerCombatStateMachine : MonoBehaviour
{
    #region Combat States
    /// <summary>전투 상태 열거형</summary>
    public enum CombatState
    {
        Idle,           // 대기 상태
        Attacking,      // 공격 중
        FlashSlashing,  // FlashSlash 스킬 사용 중
        DashAttacking   // 대시 공격 중
    }

    // 현재 전투 상태
    private CombatState currentState = CombatState.Idle;
    public CombatState CurrentState => currentState;
    #endregion

    #region Component References
    [Header("Component References")]
    [SerializeField] private Animator anim;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerGroundAttack groundAttack;
    [SerializeField] private PlayerFlashSlash flashSlash;
    #endregion

    #region State Timers
    [Header("State Duration Settings")]
    [SerializeField] private float attackDuration = 0.3f;      // 공격 지속시간
    [SerializeField] private float flashSlashDuration = 0.7f;  // FlashSlash 지속시간
    [SerializeField] private float dashAttackDuration = 0.4f;  // 대시공격 지속시간

    private float stateTimer = 0f; // 현재 상태 타이머
    #endregion

    #region Cancel System
    [Header("Cancel System")]
    [SerializeField] private bool allowAttackCancel = true;      // 공격 캔슬 허용
    [SerializeField] private bool allowFlashSlashCancel = true;  // FlashSlash 캔슬 허용
    [SerializeField] private float minCancelTime = 0.1f;         // 최소 캔슬 가능 시간

    // 캔슬 입력 버퍼
    private bool flashSlashInputBuffered = false;  // FlashSlash 입력 대기 중
    private bool attackInputBuffered = false;      // 공격 입력 대기 중
    #endregion

    #region Input Tracking
    // 입력 상태 추적
    private bool isLeftClickPressed = false;
    private bool isRightClickPressed = false;
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
        // 입력 처리
        HandleInput();

        // 상태 타이머 업데이트
        UpdateStateTimer();

        // 버퍼된 입력 처리
        ProcessBufferedInputs();
    }
    #endregion

    #region Initialization
    /// <summary>컴포넌트 참조 초기화</summary>
    private void InitializeComponents()
    {
        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (groundAttack == null)
            groundAttack = GetComponent<PlayerGroundAttack>();

        if (flashSlash == null)
            flashSlash = GetComponent<PlayerFlashSlash>();

        Debug.Log("[CombatStateMachine] 초기화 완료");
    }
    #endregion

    #region Input Handling
    /// <summary>입력 감지 및 처리</summary>
    private void HandleInput()
    {
        // UI가 열려있으면 전투 입력 차단
        if (playerController != null && playerController.isSkillCasting)
            return;

        // 좌클릭 입력 (공격)
        if (Input.GetMouseButtonDown(0))
        {
            isLeftClickPressed = true;
            OnAttackInput();
        }

        // 우클릭 입력 (FlashSlash)
        if (Input.GetMouseButtonDown(1))
        {
            isRightClickPressed = true;
            OnFlashSlashInput();
        }

        // 입력 해제
        if (Input.GetMouseButtonUp(0))
            isLeftClickPressed = false;

        if (Input.GetMouseButtonUp(1))
            isRightClickPressed = false;
    }

    /// <summary>공격 입력 처리</summary>
    private void OnAttackInput()
    {
        switch (currentState)
        {
            case CombatState.Idle:
                // 대기 중 → 일반 공격
                StartAttack();
                break;

            case CombatState.Attacking:
                // 공격 중 → 콤보 연결 (기존 시스템 유지)
                break;

            case CombatState.FlashSlashing:
                // FlashSlash 중 → 대시 공격으로 캔슬
                if (CanCancelFlashSlash())
                {
                    StartDashAttack();
                }
                else
                {
                    // 캔슬 불가능하면 입력 버퍼에 저장
                    attackInputBuffered = true;
                }
                break;

            case CombatState.DashAttacking:
                // 대시공격 중에는 입력 무시
                break;
        }
    }

    /// <summary>FlashSlash 입력 처리</summary>
    private void OnFlashSlashInput()
    {
        switch (currentState)
        {
            case CombatState.Idle:
                // 대기 중 → FlashSlash 발동
                StartFlashSlash();
                break;

            case CombatState.Attacking:
                // 공격 중 → FlashSlash로 캔슬
                if (CanCancelAttack())
                {
                    StartFlashSlash();
                }
                else
                {
                    // 캔슬 불가능하면 입력 버퍼에 저장
                    flashSlashInputBuffered = true;
                }
                break;

            case CombatState.FlashSlashing:
                // FlashSlash 중에는 추가 사용 불가
                break;

            case CombatState.DashAttacking:
                // 대시공격 중 → FlashSlash로 연계
                flashSlashInputBuffered = true;
                break;
        }
    }
    #endregion

    #region State Transitions
    /// <summary>일반 공격 시작</summary>
    private void StartAttack()
    {
        ChangeState(CombatState.Attacking);

        // 기존 PlayerGroundAttack 시스템 호출
        if (groundAttack != null && groundAttack.CanGroundAttack())
        {
            groundAttack.StartGroundCombo();
        }

        Debug.Log("[CombatSM] 공격 시작");
    }

    /// <summary>FlashSlash 스킬 시작</summary>
    private void StartFlashSlash()
    {
        ChangeState(CombatState.FlashSlashing);

        // PlayerFlashSlash 스킬 실행
        if (flashSlash != null && flashSlash.CanUseSkill())
        {
            flashSlash.UseSkill();
            Debug.Log("[CombatSM] FlashSlash 스킬 발동");
        }
        else
        {
            Debug.LogWarning("[CombatSM] FlashSlash 사용 불가 - Idle로 복귀");
            ReturnToIdle();
        }
    }

    /// <summary>대시 공격 시작</summary>
    private void StartDashAttack()
    {
        ChangeState(CombatState.DashAttacking);

        // 애니메이터 파라미터: Attack1 또는 전용 대시공격 트리거
        if (anim != null)
        {
            anim.SetTrigger("Attack1");
        }

        Debug.Log("[CombatSM] 대시 공격 시작");
    }

    /// <summary>상태 변경</summary>
    private void ChangeState(CombatState newState)
    {
        // 이전 상태 종료 처리
        OnStateExit(currentState);

        // 새 상태로 전환
        CombatState previousState = currentState;
        currentState = newState;
        stateTimer = 0f;

        // 새 상태 시작 처리
        OnStateEnter(newState);

        Debug.Log($"[CombatSM] 상태 전환: {previousState} → {newState}");
    }

    /// <summary>Idle 상태로 복귀</summary>
    public void ReturnToIdle()
    {
        ChangeState(CombatState.Idle);
    }
    #endregion

    #region State Enter/Exit
    /// <summary>상태 진입 시 처리</summary>
    private void OnStateEnter(CombatState state)
    {
        switch (state)
        {
            case CombatState.Idle:
                // 모든 버퍼 초기화
                ClearInputBuffers();
                break;

            case CombatState.Attacking:
                // 공격 시작 시간 기록
                break;

            case CombatState.FlashSlashing:
                // FlashSlash 시작
                break;

            case CombatState.DashAttacking:
                // 대시 공격 시작
                break;
        }
    }

    /// <summary>상태 종료 시 처리</summary>
    private void OnStateExit(CombatState state)
    {
        switch (state)
        {
            case CombatState.Attacking:
                // 공격 종료 처리
                break;

            case CombatState.FlashSlashing:
                // FlashSlash 종료 처리
                break;

            case CombatState.DashAttacking:
                // 대시공격 종료 처리
                break;
        }
    }
    #endregion

    #region State Timer
    /// <summary>상태 타이머 업데이트 및 자동 복귀</summary>
    private void UpdateStateTimer()
    {
        if (currentState == CombatState.Idle)
            return;

        stateTimer += Time.deltaTime;

        // 상태별 지속시간 체크 후 자동 복귀
        switch (currentState)
        {
            case CombatState.Attacking:
                // 공격은 groundAttack에서 관리하므로 여기서는 체크 안 함
                break;

            case CombatState.FlashSlashing:
                // FlashSlash는 PlayerFlashSlash에서 관리
                // 스킬이 끝나면 외부에서 ReturnToIdle() 호출해야 함
                if (flashSlash != null && !flashSlash.IsActive())
                {
                    ReturnToIdle();
                }
                break;

            case CombatState.DashAttacking:
                if (stateTimer >= dashAttackDuration)
                {
                    ReturnToIdle();
                }
                break;
        }
    }
    #endregion

    #region Cancel System
    /// <summary>공격 캔슬 가능 여부</summary>
    private bool CanCancelAttack()
    {
        if (!allowAttackCancel)
            return false;

        // 최소 시간 이후 캔슬 가능
        return stateTimer >= minCancelTime;
    }

    /// <summary>FlashSlash 캔슬 가능 여부</summary>
    private bool CanCancelFlashSlash()
    {
        if (!allowFlashSlashCancel)
            return false;

        // FlashSlash 시작 후 일정 시간 이후 캔슬 가능
        return stateTimer >= minCancelTime;
    }

    /// <summary>버퍼된 입력 처리</summary>
    private void ProcessBufferedInputs()
    {
        // FlashSlash 입력이 버퍼에 있고, 현재 캔슬 가능하면 실행
        if (flashSlashInputBuffered)
        {
            if (currentState == CombatState.Attacking && CanCancelAttack())
            {
                flashSlashInputBuffered = false;
                StartFlashSlash();
            }
            else if (currentState == CombatState.Idle)
            {
                flashSlashInputBuffered = false;
            }
        }

        // 공격 입력이 버퍼에 있고, 현재 캔슬 가능하면 실행
        if (attackInputBuffered)
        {
            if (currentState == CombatState.FlashSlashing && CanCancelFlashSlash())
            {
                attackInputBuffered = false;
                StartDashAttack();
            }
            else if (currentState == CombatState.Idle)
            {
                attackInputBuffered = false;
            }
        }
    }

    /// <summary>입력 버퍼 초기화</summary>
    private void ClearInputBuffers()
    {
        flashSlashInputBuffered = false;
        attackInputBuffered = false;
    }
    #endregion

    #region Public Interface
    /// <summary>현재 공격 중인지 확인</summary>
    public bool IsAttacking()
    {
        return currentState == CombatState.Attacking;
    }

    /// <summary>현재 FlashSlash 사용 중인지 확인</summary>
    public bool IsFlashSlashing()
    {
        return currentState == CombatState.FlashSlashing;
    }

    /// <summary>현재 대시공격 중인지 확인</summary>
    public bool IsDashAttacking()
    {
        return currentState == CombatState.DashAttacking;
    }

    /// <summary>현재 행동 중인지 (공격/FlashSlash/대시공격) 확인</summary>
    public bool IsInAction()
    {
        return currentState != CombatState.Idle;
    }

    /// <summary>외부에서 강제로 상태 변경 (긴급 상황용)</summary>
    public void ForceChangeState(CombatState newState)
    {
        ChangeState(newState);
    }
    #endregion

    #region Debug
    /// <summary>디버그 정보 표시</summary>
    private void OnGUI()
    {
        if (!Application.isEditor) return;
        if (!Application.isPlaying)
            return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = Color.white;

    
        if (flashSlashInputBuffered)
            GUI.Label(new Rect(10, 70, 300, 30), "FlashSlash Input Buffered", style);

        if (attackInputBuffered)
            GUI.Label(new Rect(10, 100, 300, 30), "Attack Input Buffered", style);
    }
    #endregion
}
