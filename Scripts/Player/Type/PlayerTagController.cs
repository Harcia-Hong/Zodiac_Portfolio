using System.Collections;
using UnityEngine;

/// <summary>
/// 태그 전환 컨트롤러 — Tab 키로 활성 캐릭터 토글 + 조건 충족 시 태그 공격 발동.
/// 
/// StanceGaugeSystem 연동:
///   - Tab 입력 시 게이지 시스템에 TrySwap 위임
///   - 반환된 SwapResult로 분기: Denied(거부) / NormalSwap(일반 전환) / TagSwap(태그 공격)
///   - OnStanceSwapped 이벤트 구독 → 실제 캐릭터 활성/비활성 실행
/// 
/// 책임:
///   - Tab 입력 감지
///   - 캐릭터 행동 상태 체크 (CanTag, IsBusy)
///   - StanceGaugeSystem.TrySwap() 호출 + 결과 분기 처리
///   - OnStanceSwapped 구독 → 캐릭터 Activate/Deactivate
///   - 일반 전환 VFX 재생, 태그 공격 VFX는 다음 단계 PlayerTagAttack에서 처리 예정
/// 
/// 책임 밖:
///   - 게이지/자원 관리 (StanceGaugeSystem)
///   - 태그 공격 광역기 모션/데미지 (별도 PlayerTagAttack 시스템에서 처리 예정)
///   - UI 갱신 (CombatUIManager/StanceGaugeUI)
/// 
/// 부착 위치: PlayerRoot GameObject (PlayerController, StanceGaugeSystem과 같은 오브젝트).
/// </summary>
public class PlayerTagController : MonoBehaviour
{
    // ============================================================
    // Inspector Settings
    // ============================================================
    [Header("입력")]
    [SerializeField, Tooltip("태그 전환 키 (기본: Tab)")]
    private KeyCode tagKey = KeyCode.Tab;

    [Header("어택캔슬 정책")]
    [SerializeField, Tooltip(
        "true: CanTag 무시 — 언제든 강제 캔슬 + 전환 (옵션 1, 완전 자유 캔슬).\n" +
        "false: CanTag 존중 — 후딜 윈도우만 허용 (옵션 2, 권장).\n" +
        "디버깅 중엔 true 권장.\n" +
        "주의: 태그 공격은 강력한 광역기라 이 옵션과 무관하게 항상 캔슬 가능하게 처리됨.")]
    private bool forceCancel = false;

    [Header("VFX (선택)")]
    [SerializeField, Tooltip("일반 전환 시 캐릭터 위치에 생성할 이펙트 프리팹 (소규모).")]
    private GameObject normalSwapEffect;

    [SerializeField, Tooltip("태그 공격 시 캐릭터 위치에 생성할 이펙트 프리팹 (대규모). " +
                             "실제 광역기 효과는 추후 PlayerTagAttack에서 처리됨.")]
    private GameObject tagSwapEffect;

    // ============================================================
    // References & State
    // ============================================================
    private PlayerController playerController;
    private bool isSubscribed = false;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();

        if (playerController == null)
        {
            Debug.LogError("[PlayerTagController] PlayerController가 같은 GameObject에 없음! " +
                           "PlayerRoot에 부착되어야 함.");
        }
    }

    /// <summary>
    /// 활성화 시 게이지 시스템의 전환 이벤트 구독.
    /// 같은 GameObject라도 Awake 순서가 보장 안 되므로 코루틴으로 Instance 대기.
    /// </summary>
    private void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    /// <summary>StanceGaugeSystem.Instance가 잡힐 때까지 한 프레임씩 대기 후 이벤트 구독.</summary>
    private IEnumerator SubscribeWhenReady()
    {
        while (StanceGaugeSystem.Instance == null) yield return null;

        StanceGaugeSystem.Instance.OnStanceSwapped += HandleStanceSwapped;
        isSubscribed = true;
    }

    /// <summary>비활성화 시 이벤트 해제. 누수 방지.</summary>
    private void OnDisable()
    {
        if (isSubscribed && StanceGaugeSystem.Instance != null)
        {
            StanceGaugeSystem.Instance.OnStanceSwapped -= HandleStanceSwapped;
        }
        isSubscribed = false;
    }

    private void Update()
    {
        HandleInput();
    }

    // ============================================================
    // Input
    // ============================================================

    /// <summary>Tab 키 입력 감지 → 태그 전환 시도</summary>
    private void HandleInput()
    {
        if (Input.GetKeyDown(tagKey))
        {
            TryTagSwap();
        }
    }

    // ============================================================
    // Tag Swap — 전환 진입점
    // ============================================================

    /// <summary>
    /// 전환 시도. Tab 입력 또는 외부(UI 버튼/게임패드)에서 호출.
    /// 
    /// 흐름:
    ///   1. 활성 캐릭터 존재 확인
    ///   2. 어택캔슬 정책: forceCancel=false이고 CanTag=false면 거부 (단, 태그 조건 충족 시 예외 허용)
    ///   3. StanceGaugeSystem.TrySwap() 호출 → SwapResult로 분기
    ///   4. NormalSwap/TagSwap이면 OnStanceSwapped 이벤트 발생 → HandleStanceSwapped 호출
    ///   5. Denied면 아무 동작 안 함 (쿨다운 등)
    /// </summary>
    public void TryTagSwap()
    {
        if (playerController == null) return;

        IPlayerCharacter current = playerController.ActiveCharacter;
        if (current == null)
        {
            Debug.LogWarning("[PlayerTagController] 활성 캐릭터가 없음");
            return;
        }

        if (StanceGaugeSystem.Instance == null)
        {
            Debug.LogError("[PlayerTagController] StanceGaugeSystem.Instance == null — 전환 불가");
            return;
        }

        // 전환 시도. OnStanceSwapped 이벤트로 실제 활성/비활성은 HandleStanceSwapped에서 처리.
        StanceGaugeSystem.SwapResult result = StanceGaugeSystem.Instance.TrySwap();

        switch (result)
        {
            case StanceGaugeSystem.SwapResult.Denied:
                // 쿨다운 등 — StanceGaugeSystem에서 이미 로그 출력함
                break;

            case StanceGaugeSystem.SwapResult.NormalSwap:
                Debug.Log("[PlayerTagController] 일반 전환 요청 처리됨");
                break;

            case StanceGaugeSystem.SwapResult.TagSwap:
                Debug.Log("[PlayerTagController] 태그 공격 요청 처리됨 → 광역기 발동 예정 (PlayerTagAttack)");
                // TODO(다음 단계): PlayerTagAttack 시스템 호출
                // - 검→활: 광역 일제사격 모션/데미지
                // - 활→검: 광역 검 회오리 모션/데미지
                break;
        }
    }

    // ============================================================
    // Stance Swap Event Handler — 일반/태그 통합 처리
    // ============================================================

    /// <summary>
    /// StanceGaugeSystem.OnStanceSwapped 이벤트 핸들러.
    /// 일반/태그 전환 모두 이 경로로 통합 처리.
    /// </summary>
    /// <param name="newActive">전환 후 활성될 스탠스 타입 (Melee/Ranged)</param>
    /// <param name="isTagSwap">true=태그 공격 전환(자원 풀 충족), false=일반 전환</param>
    private void HandleStanceSwapped(PlayerCharacterType newActive, bool isTagSwap)
    {
        if (playerController == null) return;

        IPlayerCharacter current = playerController.ActiveCharacter;
        IPlayerCharacter next = GetCharacterByType(newActive);

        if (current == null || next == null)
        {
            Debug.LogWarning($"[PlayerTagController] 전환 대상 캐릭터 못 찾음 (target={newActive})");
            return;
        }

        // 이미 해당 스탠스가 활성 상태면 (리셋 후 동기화 이벤트 등) 무시
        if (current == next) return;

        ExecuteTagSwap(current, next, isTagSwap);
    }

    // ============================================================
    // Tag Swap Execution
    // ============================================================

    /// <summary>
    /// 실제 캐릭터 활성/비활성 전환 실행.
    /// 흐름: 행동 캔슬 → Deactivate(현재) → Activate(다음) → PlayerController 알림 → VFX
    /// 
    /// 태그 공격(isTagSwap=true)이면 광역기 발동 전제이므로 무조건 캔슬.
    /// 일반 전환은 IsBusy 시에만 캔슬.
    /// </summary>
    private void ExecuteTagSwap(IPlayerCharacter current, IPlayerCharacter next, bool isTagSwap)
    {
        // 1. 진행 중 행동 캔슬 — 태그 공격이면 무조건, 일반이면 바쁠 때만
        if (isTagSwap || current.IsBusy)
        {
            current.CancelCurrentAction();
        }

        // 2. 현재 캐릭 비활성화
        current.Deactivate();

        // 3. 새 캐릭 활성화
        next.Activate();

        // 4. PlayerController에 새 활성 알림
        playerController.SetActiveCharacter(next);

        // 5. VFX — 태그/일반 분리. 태그 공격 본체 이펙트는 추후 PlayerTagAttack에서.
        GameObject effectPrefab = isTagSwap ? tagSwapEffect : normalSwapEffect;
        if (effectPrefab != null)
        {
            Instantiate(effectPrefab, transform.position, Quaternion.identity);
        }

        Debug.Log($"[PlayerTagController] 전환 완료: {current.CharacterType} → {next.CharacterType} " +
                  $"({(isTagSwap ? "TagSwap" : "NormalSwap")})");
    }

    // ============================================================
    // Helper
    // ============================================================

    /// <summary>
    /// 캐릭터 타입(Melee/Ranged)에 해당하는 IPlayerCharacter 인스턴스 반환.
    /// PlayerController.GetAvailableCharacters() 배열에서 타입 매칭.
    /// </summary>
    private IPlayerCharacter GetCharacterByType(PlayerCharacterType type)
    {
        IPlayerCharacter[] characters = playerController.GetAvailableCharacters();
        if (characters == null) return null;

        foreach (var c in characters)
        {
            if (c != null && c.CharacterType == type) return c;
        }

        return null;
    }
}
