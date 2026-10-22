using UnityEngine;

/// <summary>
/// 원거리 캐릭터 컴포넌트. IPlayerCharacter 구현 — 태그 시스템의 원거리 슬롯.
/// 
/// 책임:
///   - 원거리 캐릭터의 컴포넌트들(ArrowAttack, ChargedShot, 비주얼)을 그룹화
///   - 활성/비활성 시 위 컴포넌트들의 enabled 토글 + 비주얼 자식 SetActive
///   - 우클릭 입력을 받아 차징 사격(ChargedShot) 발동 (RangedCombatStateMachine 분리 전 임시)
///   - 옵션 2 태그 윈도우 관리 (Animation Event 콜백)
///   - 원거리 캐릭 고유 스탯 보정값 보유 (방어력 ↓, 회피 ↑ — 글래스 캐논)
/// 
/// 부착 위치: PlayerRoot GameObject (PlayerController 등과 같은 오브젝트).
/// 원거리 비주얼/Animator는 자식 GameObject (rangedVisualRoot)로 분리.
/// </summary>
public class RangedCharacter : MonoBehaviour, IPlayerCharacter
{
    // ============================================================
    // Inspector References
    // ============================================================
    [Header("원거리 캐릭터 비주얼 / 애니메이션")]
    [Tooltip("원거리 비주얼 자식 GameObject (메시 + Animator). 활성/비활성 시 SetActive 토글됨")]
    [SerializeField] private GameObject rangedVisualRoot;

    [Tooltip("원거리 캐릭터 Animator (보통 rangedVisualRoot 자식에 위치)")]
    [SerializeField] private Animator animator;

    [Header("원거리 전용 컴포넌트 (PlayerRoot에 부착된 것 참조)")]
    [Tooltip("좌클릭 사격 시스템")]
    [SerializeField] private PlayerArrowAttack arrowAttack;

    [Tooltip("우클릭 시그니처 스킬 (차징 사격 — 강사격)")]
    [SerializeField] private PlayerChargedShot chargedShot;

    [Header("Blend Tree 파라미터")]
    [SerializeField, Tooltip("이동 X (좌우) Blend Tree 파라미터 이름. Animator의 Float 파라미터와 일치해야 함")]
    private string moveXParam = "MoveX";

    [SerializeField, Tooltip("이동 Z (전후) Blend Tree 파라미터 이름")]
    private string moveZParam = "MoveZ";

    [SerializeField, Tooltip("Blend Tree 보간 시간 (초). 낮을수록 즉각적, 높을수록 부드러움")]
    private float moveBlendDampTime = 0.1f;

    // ============================================================
    // Stat Modifiers (인스펙터에서 캐릭터 컨셉 따라 조절)
    // ============================================================
    [Header("캐릭터별 스탯 보정 — 글래스 캐논 컨셉")]
    [Tooltip("방어력 보정. 원거리는 약함. 0.8 = 받는 데미지 20% 증가")]
    [SerializeField] private float defenseModifier = 0.8f;

    [Tooltip("회피율 보정. 원거리는 회피로 생존. 0.25 = 25% 회피")]
    [SerializeField] private float dodgeModifier = 0.25f;

    // ============================================================
    // Tag Window State (옵션 2: 후딜 윈도우 캔슬)
    // ============================================================
    /// <summary>
    /// 현재 태그 윈도우가 열려있는지.
    /// Animation Event (OnTagWindowOpen / OnTagWindowClose)로 토글됨.
    /// 원거리는 사격 후딜 + 차징 사격 후딜 구간 둘 다에서 윈도우 열기 가능.
    /// </summary>
    private bool isTagWindowOpen = false;

    // ============================================================
    // IPlayerCharacter Implementation - Properties
    // ============================================================

    /// <summary>이 캐릭터의 타입은 원거리</summary>
    public PlayerCharacterType CharacterType => PlayerCharacterType.Ranged;

    /// <summary>현재 활성 상태인지. 외부에서 읽기만 가능.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// 공격/스킬 진행 중인지 — 두 컴포넌트 OR 판정.
    /// 사격 진행 중이거나 차징 사격 진행 중이면 true.
    /// </summary>
    public bool IsBusy
    {
        get
        {
            bool attacking = arrowAttack != null && arrowAttack.isRangedAttacking;
            bool skillActive = chargedShot != null && chargedShot.IsActive();
            return attacking || skillActive;
        }
    }

    /// <summary>
    /// 옵션 2 — 지금 태그 전환 받아도 되는 타이밍인가?
    /// MeleeCharacter와 동일 로직: 행동 안 중이거나 후딜 윈도우 열렸으면 true.
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

    /// <summary>인스펙터에서 비어있는 참조 자동 보충</summary>
    private void Awake()
    {
        AutoFillReferences();
    }

    /// <summary>
    /// 시작 시 비활성 상태 동기화.
    /// IsActive=false인데 컴포넌트 enabled가 인스펙터 default(true)로 있으면
    /// 검 모드인데 활 좌클릭이 같이 발동되는 버그 발생 → Start에서 일괄 비활성화.
    /// </summary>
    private void Start()
    {
        if (!IsActive)
        {
            SetRangedComponentsEnabled(false);
            if (rangedVisualRoot != null) rangedVisualRoot.SetActive(false);
        }
    }

    /// <summary>
    /// 우클릭 입력 처리 (RangedCombatStateMachine 분리 전 임시).
    /// 활성 상태에서만 입력 받음 — IsActive 가드 필수
    /// (RangedCharacter.enabled는 항상 true라 이 Update가 항상 돎).
    /// 좌클릭은 PlayerArrowAttack이 자체 Update로 처리.
    /// </summary>
    private void Update()
    {
        HandleSkillInput();
    }

    /// <summary>
    /// 인스펙터에서 빠뜨린 참조 자동 탐색.
    /// 컴포넌트 참조는 같은 GameObject에서, animator는 visualRoot 자식에서 찾음.
    /// </summary>
    private void AutoFillReferences()
    {
        if (arrowAttack == null)
            arrowAttack = GetComponent<PlayerArrowAttack>();

        if (chargedShot == null)
            chargedShot = GetComponent<PlayerChargedShot>();

        if (animator == null && rangedVisualRoot != null)
            animator = rangedVisualRoot.GetComponentInChildren<Animator>();
    }

    // ============================================================
    // Input Handling (RangedCombatStateMachine 분리 전 임시 처리)
    // ============================================================

    /// <summary>
    /// 우클릭 → 차징 사격(ChargedShot) 시그니처 스킬 발동.
    /// 비활성 캐릭터일 때는 IsActive 가드로 무시 (검 모드 우클릭과 충돌 방지).
    /// </summary>
    private void HandleSkillInput()
    {
        // 안전 가드: 비활성이면 입력 무시
        if (!IsActive) return;

        if (Input.GetMouseButtonDown(1))
        {
            if (chargedShot != null && chargedShot.CanUseSkill())
            {
                chargedShot.UseSkill();
            }
        }
    }

    // ============================================================
    // IPlayerCharacter Implementation - Methods
    // ============================================================

    /// <summary>
    /// 원거리 캐릭터 활성화.
    /// 1) 태그 윈도우 상태 초기화 (안전한 시작)
    /// 2) 원거리 비주얼 자식 SetActive(true)
    /// 3) 원거리 전용 컴포넌트들 enabled = true
    /// </summary>
    public void Activate()
    {
        if (IsActive) return;

        IsActive = true;
        isTagWindowOpen = false;

        if (rangedVisualRoot != null)
            rangedVisualRoot.SetActive(true);

        SetRangedComponentsEnabled(true);
    }

    /// <summary>
    /// 원거리 캐릭터 비활성화.
    /// 1) 진행 중이던 사격/스킬 강제 종료
    /// 2) 태그 윈도우 명시적으로 닫음
    /// 3) 원거리 전용 컴포넌트들 enabled = false (입력 차단)
    /// 4) 원거리 비주얼 자식 SetActive(false)
    /// 
    /// 단, RangedCharacter 자체의 enabled는 건드리지 않음
    /// (Animation Event 콜백 받기 위해 항상 enabled 유지).
    /// </summary>
    public void Deactivate()
    {
        if (!IsActive) return;

        // 진행 중인 행동 정리 먼저 (활성 상태에서 호출돼야 안전)
        CancelCurrentAction();

        IsActive = false;
        isTagWindowOpen = false;

        SetRangedComponentsEnabled(false);

        if (rangedVisualRoot != null)
            rangedVisualRoot.SetActive(false);
    }

    /// <summary>
    /// 진행 중인 사격/차징 사격을 즉시 강제 종료.
    /// 어택캔슬 전환 시 호출 — Deactivate 직전에 상태 정리용.
    /// 둘 다 진행 중 아니면 사실상 no-op.
    /// </summary>
    public void CancelCurrentAction()
    {
        // 1) 일반 사격 진행 중이면 캔슬
        if (arrowAttack != null && arrowAttack.isRangedAttacking)
        {
            arrowAttack.CancelRangedAttack();
        }

        // 2) 차징 사격 진행 중이면 캔슬 (코루틴 강제 중단 + 이동 잠금 해제)
        if (chargedShot != null && chargedShot.IsActive())
        {
            chargedShot.CancelSkill();
        }

        // 3) 캔슬되었으니 태그 윈도우도 닫음 (안전 가드)
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
    /// 트윈 스틱 패턴 — 캐릭터 로컬 기준 이동 방향을 4방향 Blend Tree에 전달.
    /// PlayerController가 매 프레임 호출. dampTime으로 부드럽게 보간하여 클립 전환 자연스럽게.
    /// </summary>
    public void SetMoveDirection(Vector2 localDirection)
    {
        if (animator == null) return;

        // SetFloat의 4번째 인수 = dampTime (Animator 내장 보간)
        animator.SetFloat(moveXParam, localDirection.x, moveBlendDampTime, Time.deltaTime);
        animator.SetFloat(moveZParam, localDirection.y, moveBlendDampTime, Time.deltaTime);
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
    // Animation Event Callbacks (Animator Clip Event → RangedCharacter)
    // ============================================================
    // Animator는 자식 GameObject(rangedVisualRoot)에 있으므로, Animation Event를
    // RangedCharacter에 직접 연결할 수 없음. 작은 릴레이 컴포넌트(RangedAnimEventRelay)를
    // Animator GameObject에 두고 거기서 부모의 RangedCharacter로 위임 호출하는 구조.
    // (릴레이 컴포넌트는 Hierarchy 작업 시 추가 — MeleeCharacter용과 같은 패턴)

    /// <summary>
    /// Animation Event 콜백 — 후딜 태그 윈도우 진입.
    /// 사격/스킬 애니메이션의 후딜 시작점에 심을 것.
    /// </summary>
    public void OnTagWindowOpen()
    {
        if (!IsActive) return;
        isTagWindowOpen = true;
    }

    /// <summary>Animation Event 콜백 — 후딜 태그 윈도우 종료.</summary>
    public void OnTagWindowClose()
    {
        isTagWindowOpen = false;
    }

    // ============================================================
    // Helpers
    // ============================================================

    /// <summary>
    /// 원거리 전용 MonoBehaviour들의 enabled를 일괄 토글.
    /// 비활성 시 PlayerArrowAttack의 Update가 안 돌아 좌클릭 입력 무시됨.
    /// 단, RangedCharacter 자체의 enabled는 건드리지 않음 (위 Update의 IsActive 가드로 처리).
    /// </summary>
    /// <param name="enabled">true면 활성화, false면 비활성화</param>
    private void SetRangedComponentsEnabled(bool enabled)
    {
        if (arrowAttack != null) arrowAttack.enabled = enabled;
        if (chargedShot != null) chargedShot.enabled = enabled;
    }
}
