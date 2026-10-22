using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 컨트롤러 - 쿼터뷰 기반 이동 + Windblown식 대시
/// 고저차 대시, 갭 대시(플랫폼 간 이동) 지원
/// 대시는 시작점→도착점 직선 이동 (포물선 없음)
/// 
/// 책임 (캐릭터 무관 공유 로직):
///   - 이동 / 회전 / 마우스 좌표 변환 / 대시
///   - 활성 IPlayerCharacter에 애니메이션·공격 캔슬 의도 위임
///   - 활성 캐릭터 등록 인터페이스 제공 (PlayerTagController가 사용)
/// 
/// 캐릭터별 책임 (분리됨):
///   - 공격/콤보/스킬은 활성 캐릭터(MeleeCharacter / RangedCharacter)가 처리
///   - Animator는 활성 캐릭터 내부 디테일이라 직접 참조하지 않음
/// </summary>
public class PlayerController : MonoBehaviour
{
    // ============================================================
    // 대시 설정
    // ============================================================
    [Header("Dash Stack Settings")]
    [Tooltip("최대 대시 스택 수")]
    public int maxDodgeStacks = 3;

    [Tooltip("스택 당 충전 쿨타임 (초)")]
    public float dodgeCooldownPerStack = 3f;

    [Tooltip("대시 이동 거리")]
    public float dodgeDistance = 5f;

    [Tooltip("대시 지속 시간 (초)")]
    public float dodgeDuration = 0.25f;

    [Header("Sound")]
    [Tooltip("대시 시 재생할 효과음")]
    [SerializeField] private AudioClip dashSfx;

    [Tooltip("이동 시 반복 재생할 발걸음 효과음")]
    [SerializeField] private AudioClip footstepSfx;

    [Tooltip("발걸음 재생 간격 (초). 작을수록 빠른 발걸음")]
    [SerializeField] private float footstepInterval = 0.35f;

    [Tooltip("발걸음 음량 배율 (0~1, 검 휘두르기보다 작게 권장)")]
    [SerializeField, Range(0f, 1f)] private float footstepVolumeScale = 0.5f;

    [Header("Dash - Height & Gap Settings")]
    [Tooltip("대시로 이동 가능한 최대 높이 차이")]
    public float maxDashHeightDiff = 5f;

    [Tooltip("갭 대시 시 착지점 탐색 범위 (대시 거리 배수)")]
    public float gapSearchMultiplier = 1.2f;

    [Tooltip("착지점 탐색용 Raycast 높이 오프셋")]
    public float landingRayHeight = 15f;

    [Header("Dash - Invincibility")]
    [Tooltip("대시 중 무적 여부")]
    public bool dashInvincible = true;

    // 대시 상태
    private int currentDodgeStacks = 3;
    private float dodgeCooldownTimer = 0f;

    // ============================================================
    // 이동 설정
    // ============================================================
    [Header("Mouse Control")]
    public LayerMask groundLayer = 1;

    float hAxis;
    float vAxis;

    [Header("공격 후 잔여 회전 잠금")]
    [SerializeField, Tooltip("공격/스킬 종료 후 마우스 방향을 유지하는 시간 (초). 이 시간 동안 이동 방향으로 회전하지 않아 자연스러운 응시 잔여 효과")]
    private float postAttackTurnLockDuration = 0.4f;

    private float postAttackTurnLockTimer = 0f;

    // 발걸음 사운드 타이머 (정지 시 footstepInterval로 세팅되어 다음 이동 첫 프레임에 즉시 재생)
    private float footstepTimer = 0f;

    // ============================================================
    // 컴포넌트 참조 (캐릭터 무관 공유 시스템)
    // ============================================================
    Rigidbody rigid;
    PlayerLockOn playerLockOn;
    Camera mainCamera;

    // ============================================================
    // 활성 캐릭터 (IPlayerCharacter 위임)
    // ============================================================
    /// <summary>같은 GameObject에 부착된 모든 IPlayerCharacter (Melee/Ranged 등)</summary>
    private IPlayerCharacter[] availableCharacters;

    /// <summary>현재 활성 캐릭터. 모든 캐릭터별 동작은 이 참조를 통해 위임됨.</summary>
    private IPlayerCharacter activeCharacter;

    /// <summary>외부에서 활성 캐릭터 조회 (UI, 디버그 등)</summary>
    public IPlayerCharacter ActiveCharacter => activeCharacter;

    Vector3 moveVec;
    Vector3 dodgeVec;
    Vector3 mouseWorldPos;

    // ============================================================
    // 공개 상태 플래그
    // ============================================================
    public bool isDodging;
    public bool isSkillCasting = false;
    public bool isCharging = false;
    public bool isSwap;
    public bool isInSwitchMode = false;

    /// <summary>대시 중 무적 상태인지 (외부에서 확인용)</summary>
    public bool IsDashInvincible => isDodging && dashInvincible;

    // UI 연동용 프로퍼티
    public int CurrentDodgeStacks => currentDodgeStacks;
    public int MaxDodgeStacks => maxDodgeStacks;
    public float CooldownProgress => dodgeCooldownTimer / dodgeCooldownPerStack;
    public bool IsFullyCharged => currentDodgeStacks >= maxDodgeStacks;

    // ============================================================
    // 초기화
    // ============================================================
    void Awake()
    {
        rigid = GetComponent<Rigidbody>();
        playerLockOn = GetComponent<PlayerLockOn>();
        mainCamera = Camera.main;

        // 활성 캐릭터 자동 탐색 + 초기 활성화
        InitializeCharacters();
    }

    /// <summary>
    /// 씬 로드 콜백 등록 — DontDestroyOnLoad된 Player가 새 씬 진입 시 카메라 재캐싱 필요
    /// </summary>
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// 씬 로드 시 호출 — 새 씬의 Main Camera로 재캐싱
    /// 이전 씬의 카메라는 Destroy되므로 참조 갱신 없이는 MissingReferenceException 발생
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogWarning($"[PlayerController] 씬 '{scene.name}'에 MainCamera 태그를 가진 카메라가 없습니다.");
        }
    }

    /// <summary>
    /// 같은 GameObject의 모든 IPlayerCharacter를 모아 첫 번째를 활성화.
    /// PlayerTagController 도입(4단계) 후에는 이 책임이 PlayerTagController로 이전됨 — 그때 이 메서드는 제거 또는 비활성.
    /// </summary>
    private void InitializeCharacters()
    {
        availableCharacters = GetComponents<IPlayerCharacter>();

        if (availableCharacters.Length == 0)
        {
            Debug.LogError("[PlayerController] IPlayerCharacter 컴포넌트가 부착되어 있지 않음! 최소 1개 (MeleeCharacter 등) 필요.");
            return;
        }

        // 첫 번째 캐릭터를 활성으로 (보통 MeleeCharacter)
        activeCharacter = availableCharacters[0];
        activeCharacter.Activate();

        // 나머지는 비활성화 보장 (씬에서 켜진 채 시작했을 가능성 차단)
        for (int i = 1; i < availableCharacters.Length; i++)
        {
            availableCharacters[i].Deactivate();
        }

        Debug.Log($"[PlayerController] 캐릭터 {availableCharacters.Length}개 등록, 시작 활성: {activeCharacter.CharacterType}");
    }

    // ============================================================
    // 업데이트
    // ============================================================
    private void Update()
    {
        //if (Time.timeScale == 0f)
        //{
        //    Time.timeScale = 1f;
        //}

        // 공격 후 잔여 회전 잠금 타이머 감소
        if (postAttackTurnLockTimer > 0f)
            postAttackTurnLockTimer -= Time.deltaTime;

        UpdateDodgeStacks();
    }

    void LateUpdate()
    {
        GetInput();
        UpdateMouseWorldPosition();
        MoveInput();
        Turn();
        DodgeInput();
    }

    // ============================================================
    // 입력 처리
    // ============================================================

    /// <summary>매 프레임 입력 수집</summary>
    void GetInput()
    {
        hAxis = Input.GetAxisRaw("Horizontal");
        vAxis = Input.GetAxisRaw("Vertical");
    }

    /// <summary>이동 입력 → 속도 적용 + 활성 캐릭터에 이동 애니메이션 위임</summary>
    void MoveInput()
    {
        if (isInSwitchMode) return;

        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 cameraRight = mainCamera.transform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        moveVec = (cameraForward * vAxis + cameraRight * hAxis).normalized;

        // 대시 중에는 코루틴이 이동을 담당하므로 일반 이동 차단
        if (isDodging)
        {
            rigid.linearVelocity = new Vector3(0f, rigid.linearVelocity.y, 0f);
            return;
        }

        if (isSwap || isSkillCasting)
            moveVec = Vector3.zero;

        // 이동 속도 계산
        float realMoveSpeed = 5f;
        if (PlayerStats.Instance != null)
        {
            realMoveSpeed = PlayerStats.Instance.MoveSpeed / 80f;
        }

        Vector3 velocity = moveVec * realMoveSpeed;
        velocity.y = rigid.linearVelocity.y;
        rigid.linearVelocity = velocity;

        // 활성 캐릭터에 이동 애니메이션 위임 (캐릭터별 자체 Animator 컨트롤)
        if (activeCharacter != null)
        {
            activeCharacter.SetMoving(moveVec != Vector3.zero);
        }

        // 캐릭터 로컬 기준 이동 방향 계산 (트윈 스틱 4방향 Blend Tree용)
        // 활 캐릭은 항상 마우스 응시 중이므로, 입력 벡터를 캐릭터 기준으로 변환해야 정확한 방향 모션 재생
        Vector3 localMoveVec = transform.InverseTransformDirection(moveVec);
        activeCharacter?.SetMoveDirection(new Vector2(localMoveVec.x, localMoveVec.z));

        // ============================================================
        // 발걸음 사운드 처리 (이동 중 + 대시 아닐 때만)
        // ============================================================
        HandleFootstepSound();
    }

    /// <summary>
    /// 발걸음 사운드 처리 — 이동 중이고 대시가 아닐 때 footstepInterval 간격으로 반복 재생
    /// 정지 시 타이머를 interval로 세팅하여 다음 이동 첫 프레임에 즉시 발걸음 재생
    /// </summary>
    private void HandleFootstepSound()
    {
        // 정지 또는 대시 중이면 타이머 리셋 (다음 이동 시 즉시 재생되도록)
        if (moveVec == Vector3.zero || isDodging)
        {
            footstepTimer = footstepInterval;
            return;
        }

        // 이동 중 — 타이머 누적
        footstepTimer += Time.deltaTime;

        if (footstepTimer >= footstepInterval)
        {
            footstepTimer = 0f;

            if (footstepSfx != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(footstepSfx, footstepVolumeScale);
            }
        }
    }

    // ============================================================
    // 회전 처리
    // ============================================================

    /// <summary>
    /// 캐릭터 회전 — 행동 상황에 따라 분기.
    /// 활성 캐릭터가 행동 중(IsBusy=true)이면 마우스 방향 부드럽게 추적,
    /// 아니면 이동 방향으로 즉시 향함.
    /// </summary>
    void Turn()
    {
        bool inCombatAction = activeCharacter != null && activeCharacter.IsBusy;
        bool inPostAttackLock = postAttackTurnLockTimer > 0f;

        if (inCombatAction || inPostAttackLock)
        {
            // 공격 진행 중 또는 공격 직후 잔여 잠금 — 회전 변경 X
            return;
        }

        if (moveVec != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveVec);
            transform.rotation = Quaternion.Lerp(
                transform.rotation, targetRotation, Time.deltaTime * 15f
            );
        }
    }

    // ============================================================
    // 마우스 월드 좌표
    // ============================================================

    /// <summary>마우스 스크린 좌표 → 월드 좌표 변환</summary>
    void UpdateMouseWorldPosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayer))
            mouseWorldPos = hit.point;
        else
        {
            Plane groundPlane = new Plane(Vector3.up, transform.position);

            if (groundPlane.Raycast(ray, out float distance))
                mouseWorldPos = ray.GetPoint(distance);
        }
    }

    /// <summary>마우스 방향으로 즉시 회전</summary>
    public void RotateToMouseDirection()
    {
        Vector3 lookDirection;

        if (playerLockOn != null && playerLockOn.isLockOn && playerLockOn.currentTarget != null)
        {
            lookDirection = (playerLockOn.currentTarget.position - transform.position).normalized;
        }
        else
        {
            lookDirection = (mouseWorldPos - transform.position).normalized;
        }

        lookDirection.y = 0;

        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }

    /// <summary>마우스 방향 벡터 반환 (Y=0). 락온 시 락온 타겟 방향.</summary>
    public Vector3 GetMouseDirection()
    {
        Vector3 direction;

        if (playerLockOn != null && playerLockOn.isLockOn && playerLockOn.currentTarget != null)
        {
            direction = (playerLockOn.currentTarget.position - transform.position).normalized;
        }
        else
        {
            direction = (mouseWorldPos - transform.position).normalized;
        }

        direction.y = 0;
        return direction;
    }

    /// <summary>마우스 월드 좌표 반환 (락온 시 적 위치)</summary>
    public Vector3 GetMouseWorldPosition()
    {
        if (playerLockOn != null && playerLockOn.isLockOn && playerLockOn.currentTarget != null)
        {
            return playerLockOn.currentTarget.position;
        }

        return mouseWorldPos;
    }

    // ============================================================
    // 대시 시스템 — Windblown 스타일
    // ============================================================

    /// <summary>Space키 입력 → 대시 실행</summary>
    void DodgeInput()
    {
        if (isInSwitchMode || isDodging) return;
        if (currentDodgeStacks <= 0) return;

        // 카메라 기준 입력 방향 계산
        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 cameraRight = mainCamera.transform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 inputVec = (cameraForward * vAxis + cameraRight * hAxis).normalized;

        // Space키 + 방향 입력이 있을 때 대시 실행
        if (Input.GetKeyDown(KeyCode.Space) && inputVec != Vector3.zero)
        {
            // 행동 중이면 활성 캐릭터에 캔슬 위임 (콤보/공격/스킬 일괄 정리)
            if (activeCharacter != null && activeCharacter.IsBusy)
            {
                activeCharacter.CancelCurrentAction();
            }

            // 스택 소모
            currentDodgeStacks--;

            // 대시 방향 저장 후 실행
            dodgeVec = inputVec;
            StartCoroutine(DashCoroutine(dodgeVec));

            // UI 쿨다운 표시
            if (currentDodgeStacks < maxDodgeStacks && dodgeCooldownTimer == 0f)
            {
                CombatUIManager.Instance?.StartSkillCoolDown(KeyCode.Space, dodgeCooldownPerStack);
            }
        }
    }

    /// <summary>
    /// Windblown식 대시 코루틴
    /// - 도착 지점 지형 높이를 감지하여 고저차 대시
    /// - 빈 공간이 있으면 건너편 플랫폼을 탐색하여 갭 대시
    /// - 시작점 → 도착점 직선 이동 (Y축 포함)
    /// </summary>
    private IEnumerator DashCoroutine(Vector3 direction)
    {
        isDodging = true;

        // 대시 사운드 재생
        if (dashSfx != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(dashSfx);
        }

        // 활성 캐릭터에 대시 애니메이션 위임 (각 캐릭 자체 Animator의 Dodge 트리거)
        if (activeCharacter != null)
        {
            activeCharacter.TriggerDash();
        }

        // 대시 시작 위치
        Vector3 startPos = transform.position;

        // 착지 지점 탐색
        Vector3 landingPos = FindLandingPosition(startPos, direction);

        // 중력 비활성화 (대시 중 떨어지지 않게)
        bool originalUseGravity = rigid.useGravity;
        rigid.useGravity = false;
        rigid.linearVelocity = Vector3.zero;

        // 직선 이동 (시작점 → 도착점, Y축 포함)
        float elapsed = 0f;

        while (elapsed < dodgeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dodgeDuration);

            // Ease Out Cubic — 빠르게 시작, 부드럽게 감속
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            // 3D 직선 보간 (Y축 포함)
            Vector3 currentPos = Vector3.Lerp(startPos, landingPos, easedT);

            rigid.MovePosition(currentPos);

            yield return null;
        }

        // 최종 위치 확정
        rigid.MovePosition(landingPos);

        // 중력 복원
        rigid.useGravity = originalUseGravity;
        rigid.linearVelocity = Vector3.zero;

        // 대시 종료
        isDodging = false;
    }

    /// <summary>
    /// 착지 지점 탐색
    /// 1) 대시 거리 지점에서 아래로 Raycast → 바닥 있으면 거기 (고저차 대시)
    /// 2) 바닥 없으면 더 먼 곳까지 탐색 (갭 대시 — 건너편 플랫폼)
    /// 3) 그래도 못 찾으면 뒤로 가장 가까운 안전 지점 (낙사 방지)
    /// </summary>
    private Vector3 FindLandingPosition(Vector3 startPos, Vector3 direction)
    {
        float searchDistance = dodgeDistance * gapSearchMultiplier;
        float stepSize = 0.5f;

        // 1단계: 대시 거리 지점에 바닥이 있는지 확인
        Vector3 targetXZ = startPos + direction * dodgeDistance;
        Vector3 rayOrigin = new Vector3(targetXZ.x, startPos.y + landingRayHeight, targetXZ.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, landingRayHeight * 2f, groundLayer))
        {
            float heightDiff = Mathf.Abs(hit.point.y - startPos.y);

            if (heightDiff <= maxDashHeightDiff)
            {
                return hit.point + Vector3.up * 0.05f;
            }
        }

        // 2단계: 갭 건너편 탐색 (더 먼 곳까지)
        for (float dist = dodgeDistance + stepSize; dist <= searchDistance; dist += stepSize)
        {
            Vector3 searchXZ = startPos + direction * dist;
            Vector3 searchRayOrigin = new Vector3(searchXZ.x, startPos.y + landingRayHeight, searchXZ.z);

            if (Physics.Raycast(searchRayOrigin, Vector3.down, out RaycastHit gapHit, landingRayHeight * 2f, groundLayer))
            {
                float heightDiff = Mathf.Abs(gapHit.point.y - startPos.y);

                if (heightDiff <= maxDashHeightDiff)
                {
                    return gapHit.point + Vector3.up * 0.05f;
                }
            }
        }

        // 3단계: 뒤로 돌아와서 가장 가까운 안전 지점 찾기
        for (float dist = dodgeDistance - stepSize; dist > 0f; dist -= stepSize)
        {
            Vector3 fallbackXZ = startPos + direction * dist;
            Vector3 fallbackRayOrigin = new Vector3(fallbackXZ.x, startPos.y + landingRayHeight, fallbackXZ.z);

            if (Physics.Raycast(fallbackRayOrigin, Vector3.down, out RaycastHit fallbackHit, landingRayHeight * 2f, groundLayer))
            {
                float heightDiff = Mathf.Abs(fallbackHit.point.y - startPos.y);

                if (heightDiff <= maxDashHeightDiff)
                {
                    return fallbackHit.point + Vector3.up * 0.05f;
                }
            }
        }

        // 어디에도 착지 불가 → 현재 위치 유지 (대시 실패)
        return startPos;
    }

    // ============================================================
    // 대시 스택 충전
    // ============================================================

    /// <summary>시간 경과에 따라 대시 스택 자동 충전</summary>
    void UpdateDodgeStacks()
    {
        if (currentDodgeStacks < maxDodgeStacks)
        {
            dodgeCooldownTimer += Time.deltaTime;

            if (dodgeCooldownTimer >= dodgeCooldownPerStack)
            {
                currentDodgeStacks++;
                dodgeCooldownTimer = 0f;

                if (currentDodgeStacks < maxDodgeStacks)
                {
                    CombatUIManager.Instance?.StartSkillCoolDown(KeyCode.Space, dodgeCooldownPerStack);
                }
            }
        }
    }

    // ============================================================
    // 외부 인터페이스 (기존)
    // ============================================================

    /// <summary>스위치 모드 진입 (UI 등)</summary>
    public void EnterSwitchMode()
    {
        isInSwitchMode = true;
        moveVec = Vector3.zero;
        rigid.linearVelocity = Vector3.zero;

        // 행동 중이면 활성 캐릭터에 캔슬 위임
        if (activeCharacter != null && activeCharacter.IsBusy)
        {
            activeCharacter.CancelCurrentAction();
        }
    }

    /// <summary>스위치 모드 해제</summary>
    public void ExitSwitchMode()
    {
        isInSwitchMode = false;
    }

    /// <summary>
    /// 공격/스킬 종료 직후 호출 — 잔여 회전 잠금 타이머 시작.
    /// 이 시간 동안 마우스 방향을 유지하고 이동 방향으로 회전하지 않음.
    /// PlayerArrowAttack, PlayerGroundAttack, PlayerFlashSlash 등에서 종료 시 호출.
    /// </summary>
    public void StartPostAttackTurnLock()
    {
        postAttackTurnLockTimer = postAttackTurnLockDuration;
    }

    // ============================================================
    // 태그 시스템 외부 인터페이스 (PlayerTagController가 사용)
    // ============================================================

    /// <summary>
    /// 활성 캐릭터 등록 — PlayerTagController가 전환 처리 후 호출.
    /// 주의: 이 메서드는 단순 등록만 함. 실제 Activate/Deactivate는 PlayerTagController 책임.
    /// </summary>
    public void SetActiveCharacter(IPlayerCharacter character)
    {
        activeCharacter = character;
    }

    /// <summary>등록된 모든 캐릭터 조회 (PlayerTagController가 토글 대상 파악용)</summary>
    public IPlayerCharacter[] GetAvailableCharacters() => availableCharacters;

    // ============================================================
    // 디버그 시각화
    // ============================================================
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 마우스 위치 표시
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, mouseWorldPos);
        Gizmos.DrawSphere(mouseWorldPos, 0.3f);

        // 락온 타겟 표시
        if (playerLockOn != null && playerLockOn.isLockOn && playerLockOn.currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, playerLockOn.currentTarget.position);
            Gizmos.DrawSphere(playerLockOn.currentTarget.position, 0.5f);
        }

        // 대시 중이면 표시
        if (isDodging)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
