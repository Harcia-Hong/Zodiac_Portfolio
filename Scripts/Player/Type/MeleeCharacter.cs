using UnityEngine;

/// <summary>
/// 근접 캐릭터 컴포넌트. IPlayerCharacter 구현 — 태그 시스템의 근접 슬롯.
/// 
/// 이 컴포넌트의 책임:
///   - 근접 캐릭터의 컴포넌트들(콤보머신, 공격매니저, FlashSlash, 비주얼)을 그룹화
///   - 활성/비활성 시 위 컴포넌트들의 enabled 토글 + 비주얼 자식 SetActive
///   - 근접 캐릭터 고유의 스탯 보정값 보유 (방어력 ↑, 회피 0)
///   - 옵션 2 태그 윈도우 관리 (Animator Event로 후딜 구간 진입/종료 감지)
/// 
/// 부착 위치: PlayerRoot GameObject (PlayerController 등과 같은 오브젝트).
/// 근접 비주얼/Animator는 자식 GameObject (meleeVisualRoot)로 분리.
/// </summary>
public class MeleeCharacter : MonoBehaviour, IPlayerCharacter
{
    // ============================================================
    // Inspector References
    // ============================================================
    [Header("근접 캐릭터 비주얼 / 애니메이션")]
    [Tooltip("근접 비주얼 자식 GameObject (메시 + Animator). 활성/비활성 시 SetActive 토글됨")]
    [SerializeField] private GameObject meleeVisualRoot;

    [Tooltip("근접 캐릭터 Animator (보통 meleeVisualRoot 자식에 위치)")]
    [SerializeField] private Animator animator;

    [Header("근접 전용 컴포넌트 (PlayerRoot에 부착된 것 참조)")]
    [Tooltip("근접 콤보 상태머신")]
    [SerializeField] private PlayerCombatStateMachine combatStateMachine;

    [Tooltip("근접 공격 매니저")]
    [SerializeField] private PlayerGroundAttack groundAttack;

    [Tooltip("근접 시그니처 스킬 (우클릭 FlashSlash)")]
    [SerializeField] private PlayerFlashSlash flashSlash;

    [Tooltip("메인 비주얼 외에 활성/비활성 시 같이 토글할 추가 GameObject들. " +
         "예: 손 본에 매달린 검(Blade), 등의 망토, 캐릭 전용 VFX 등.")]
    [SerializeField] private GameObject[] additionalVisualObjects;

    // ============================================================
    // Stat Modifiers (인스펙터에서 캐릭터 컨셉 따라 조절)
    // ============================================================
    [Header("캐릭터별 스탯 보정")]
    [Tooltip("방어력 보정. 1.0 = 보정 없음, 1.2 = 받는 데미지 20% 감소")]
    [SerializeField] private float defenseModifier = 1.2f;

    [Tooltip("회피율 보정. 0.0 = 회피 없음, 0.3 = 30% 회피")]
    [SerializeField] private float dodgeModifier = 0.0f;

    // ============================================================
    // Tag Window State (옵션 2: 후딜 윈도우 캔슬)
    // ============================================================
    /// <summary>
    /// 현재 태그 윈도우가 열려있는지.
    /// Animation Event (OnTagWindowOpen / OnTagWindowClose)로 토글됨.
    /// 공격 애니메이션의 후딜 구간 진입 시 true, 종료 또는 캔슬 시 false.
    /// </summary>
    private bool isTagWindowOpen = false;

    // ============================================================
    // IPlayerCharacter Implementation - Properties
    // ============================================================

    /// <summary>이 캐릭터의 타입은 근접</summary>
    public PlayerCharacterType CharacterType => PlayerCharacterType.Melee;

    /// <summary>현재 활성 상태인지. 외부에서 읽기만 가능.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// 공격/스킬 진행 중인지 — PlayerCombatStateMachine.IsInAction()에 위임.
    /// 콤보머신이 Idle이 아니면(Attacking/FlashSlashing/DashAttacking) true.
    /// </summary>
    public bool IsBusy
    {
        get
        {
            if (combatStateMachine == null) return false;
            return combatStateMachine.IsInAction();
        }
    }

    /// <summary>
    /// 옵션 2 — 지금 태그 전환 받아도 되는 타이밍인가?
    /// 
    /// true 조건:
    ///   - 행동 안 중 (IsBusy=false → 자유 전환)
    ///   - 또는 행동 중인데 후딜 윈도우 열림 (isTagWindowOpen=true)
    /// false 조건:
    ///   - 행동 중인데 후딜 윈도우 닫힘 (선딜/타격 판정 구간)
    /// </summary>
    public bool CanTag => !IsBusy || isTagWindowOpen;

    /// <summary>이 캐릭터의 GameObject 루트 = PlayerRoot의 transform</summary>
    public Transform Root => transform;

    /// <summary>방어력 보정 노출</summary>
    public float DefenseModifier => defenseModifier;

    /// <summary>회피율 보정 노출</summary>
    public float DodgeModifier => dodgeModifier;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    /// <summary>인스펙터에서 비어있는 참조를 자동 보충</summary>
    private void Awake()
    {
        AutoFillReferences();
    }

    /// <summary>
    /// 인스펙터에서 빠뜨린 참조 자동 탐색.
    /// 컴포넌트 참조는 같은 GameObject에서, animator는 visualRoot 자식에서 찾음.
    /// </summary>
    private void AutoFillReferences()
    {
        if (combatStateMachine == null)
            combatStateMachine = GetComponent<PlayerCombatStateMachine>();

        if (groundAttack == null)
            groundAttack = GetComponent<PlayerGroundAttack>();

        if (flashSlash == null)
            flashSlash = GetComponent<PlayerFlashSlash>();

        if (animator == null && meleeVisualRoot != null)
            animator = meleeVisualRoot.GetComponentInChildren<Animator>();
    }

    // ============================================================
    // IPlayerCharacter Implementation - Methods
    // ============================================================

    /// <summary>
    /// 근접 캐릭터 활성화.
    /// 1) 태그 윈도우 상태 초기화 (안전한 시작)
    /// 2) 근접 비주얼 자식 SetActive(true)
    /// 3) 근접 전용 컴포넌트들 enabled = true (입력 수신 시작)
    /// </summary>
    public void Activate()
    {
        if (IsActive) return;

        IsActive = true;
        isTagWindowOpen = false;

        if (meleeVisualRoot != null)
            meleeVisualRoot.SetActive(true);

        SetAdditionalVisualsActive(true);

        SetMeleeComponentsEnabled(true);
    }

    /// <summary>
    /// 근접 캐릭터 비활성화.
    /// 1) 진행 중이던 콤보/공격 강제 종료
    /// 2) 태그 윈도우 명시적으로 닫음
    /// 3) 근접 전용 컴포넌트들 enabled = false (입력 차단)
    /// 4) 근접 비주얼 자식 SetActive(false)
    /// 
    /// 공유 시스템(PlayerHealth/PlayerStats/SkillManager)은 건드리지 않음.
    /// </summary>
    public void Deactivate()
    {
        if (!IsActive) return; // 중복 호출 방지

        // 콤보 상태 정리 먼저 (활성 상태에서 호출돼야 안전)
        CancelCurrentAction();

        IsActive = false;
        isTagWindowOpen = false; // 비활성 시 명시적으로 닫기

        SetMeleeComponentsEnabled(false);

        SetAdditionalVisualsActive(false);

        if (meleeVisualRoot != null)
            meleeVisualRoot.SetActive(false);
    }

    /// <summary>
    /// 진행 중인 공격/콤보/FlashSlash를 즉시 강제 종료.
    /// 어택캔슬 전환 시 호출 — Deactivate 직전에 콤보 상태 정리용.
    /// IsBusy=false면 사실상 no-op.
    /// </summary>
    public void CancelCurrentAction()
    {
        // 1) 기본 공격(콤보) 캔슬
        if (groundAttack != null && groundAttack.isAttacking)
        {
            groundAttack.CancelGroundAttack();
        }

        // 2) 콤보 상태머신을 Idle로 강제 복귀 (Attacking / FlashSlashing / DashAttacking 모두 정리)
        if (combatStateMachine != null && combatStateMachine.IsInAction())
        {
            combatStateMachine.ReturnToIdle();
        }

        // 3) FlashSlash 코루틴이 돌고 있으면 강제 중단 (이동 잠금 해제도 같이 처리됨)
        if (flashSlash != null && flashSlash.IsActive())
        {
            flashSlash.CancelSkill();
        }

        // 4) 캔슬되었으니 태그 윈도우도 닫음 (안전 가드)
        isTagWindowOpen = false;
    }

    /// <summary>
    /// 이동 애니메이션 파라미터 컨트롤. PlayerController.MoveInput에서 매 프레임 호출.
    /// 비활성 상태이거나 animator 없으면 무시(안전 가드).
    /// </summary>
    public void SetMoving(bool isMoving)
    {
        if (!IsActive) return;
        if (animator == null) return;

        animator.SetBool("isRunning", isMoving);
    }

    /// <summary>
    /// 트윈 스틱 패턴 미사용 — 근접 캐릭은 이동 방향 응시라 4방향 Blend Tree 불필요.
    /// IPlayerCharacter 인터페이스 충족용 빈 구현.
    /// </summary>
    public void SetMoveDirection(Vector2 localDirection)
    {
        // no-op — 근접 캐릭은 단일 Run 클립 사용
    }

    /// <summary>
    /// 대시 애니메이션 트리거. PlayerController.DodgeInput에서 대시 시작 시 1회 호출.
    /// 비활성 상태이거나 animator 없으면 무시.
    /// </summary>
    public void TriggerDash()
    {
        if (!IsActive) return;
        if (animator == null) return;

        animator.SetTrigger("Dodge");
    }

    // ============================================================
    // Animation Event Callbacks (Animator Clip Event → MeleeCharacter)
    // ============================================================
    // Animator는 자식 GameObject(meleeVisualRoot)에 있으므로, Animation Event를
    // MeleeCharacter에 직접 연결할 수 없음. 작은 릴레이 컴포넌트(MeleeAnimEventRelay)를
    // Animator GameObject에 두고 거기서 부모의 MeleeCharacter로 위임 호출하는 구조.
    // (릴레이 컴포넌트는 Hierarchy 작업 시 추가)

    /// <summary>
    /// Animation Event 콜백 — 후딜 태그 윈도우 진입.
    /// 공격 애니메이션의 타격 판정 끝나는 지점(후딜 시작점)에 심을 것.
    /// 이 시점부터 Tab 입력으로 자연스러운 캔슬 전환이 허용됨.
    /// </summary>
    public void OnTagWindowOpen()
    {
        if (!IsActive) return; // 비활성 캐릭 안전 가드
        isTagWindowOpen = true;
    }

    /// <summary>
    /// Animation Event 콜백 — 후딜 태그 윈도우 종료.
    /// 공격 애니메이션의 마지막 (Idle 복귀 직전)에 심을 것.
    /// 또는 콤보머신이 자연 Idle 복귀할 때도 닫혀야 하므로,
    /// PlayerCombatStateMachine.ReturnToIdle 시점에도 호출 권장 (보강 단계에서).
    /// </summary>
    public void OnTagWindowClose()
    {
        isTagWindowOpen = false;
    }

    // ============================================================
    // Helpers
    // ============================================================

    /// <summary>
    /// 근접 전용 MonoBehaviour들의 enabled를 일괄 토글.
    /// 비활성 시 Update가 안 돌아 입력 무시됨 (PlayerCombatStateMachine.HandleInput 차단).
    /// </summary>
    /// <param name="enabled">true면 활성화, false면 비활성화</param>
    private void SetMeleeComponentsEnabled(bool enabled)
    {
        if (combatStateMachine != null) combatStateMachine.enabled = enabled;
        if (groundAttack != null) groundAttack.enabled = enabled;
        if (flashSlash != null) flashSlash.enabled = enabled;
    }

    /// <summary>
    /// 추가 비주얼 GameObject들의 SetActive를 일괄 토글.
    /// 본 계층에 부착된 무기/이펙트 등 SK_Mannequin_Mobile과 분리된 비주얼 처리용.
    /// </summary>
    private void SetAdditionalVisualsActive(bool active)
    {
        if (additionalVisualObjects == null) return;

        foreach (var obj in additionalVisualObjects)
        {
            if (obj != null) obj.SetActive(active);
        }
    }
}
