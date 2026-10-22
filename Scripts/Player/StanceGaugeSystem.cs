using UnityEngine;
using System;

/// <summary>
/// 스탠스 게이지 시스템 — 화살 단일 자원 관리 및 무기 전환 처리.
/// 
/// 게임 컨셉:
///   한 육체에 두 영혼(검사/궁수)이 공존. 검 모드에서 적을 베며 활용 화살을 모으고,
///   활 모드에서 그 화살을 발사한다. 두 영혼이 자원 하나로 연결된 사이클.
/// 
/// 자원 매커니즘 (단일 자원: arrows):
///   [검 모드 (Melee)]
///     좌클릭 명중 → 화살 +1
///     우클릭(일섬) 명중 → 화살 +2
///     화살 == Max + Tab → 태그 공격 (활 전환 + 광역 일제사격)
///     화살 < Max + Tab → 그냥 활로 전환 (자원 그대로 유지)
/// 
///   [활 모드 (Ranged)]
///     좌클릭 → 화살 -1
///     우클릭(차징) → 화살 -2
///     화살 == 0 + Tab → 태그 공격 (검 전환 + 광역 검 회오리)
///     화살 > 0 + Tab → 그냥 검으로 전환 (자원 그대로 유지)
/// 
/// 시작 상태: 검 모드 + 화살 0 (검으로 모으는 패턴이 메인 루프 학습에 적합).
/// 
/// 책임:
///   - 화살 잔량 관리 및 클램프
///   - 자원 변동 API 제공 (공격/소모 시 외부에서 호출)
///   - 활성 스탠스 추적 + 전환 시 결과 판정 (Denied / NormalSwap / TagSwap)
///   - UI/SFX/실제 무기 전환을 위한 이벤트 발행
/// 
/// 책임 밖:
///   - 실제 캐릭터 활성/비활성 (PlayerTagController가 OnStanceSwapped 구독해서 처리)
///   - Tab 입력 처리 (PlayerTagController.Update 담당)
///   - UI 렌더링 (StanceGaugeUI 담당)
///   - 태그 공격 모션/데미지 (별도 PlayerTagAttack 시스템에서 처리 예정)
/// </summary>
public class StanceGaugeSystem : MonoBehaviour
{
    public static StanceGaugeSystem Instance { get; private set; }

    // ============================================================
    // 자원 최대치 설정
    // ============================================================
    [Header("자원 최대치")]
    [SerializeField, Tooltip("화살 최대 보유량. 좌클릭 1발/우클릭 2발 소모, 검 좌+1/우+2 적립 기준 5권장.")]
    private int baseMaxArrows = 5;

    [SerializeField, Tooltip("게임 시작 시 보유한 화살 수. 0이면 검으로 모아야 활 사용 가능.")]
    private int startingArrows = 0;

    // ============================================================
    // 전환 제약
    // ============================================================
    [Header("전환 제약")]
    [SerializeField, Tooltip("연속 전환 사이 쿨다운(초). 무한 Tab 도배 방지용. 0.3~0.5 권장.")]
    private float swapCooldown = 0.3f;

    // ============================================================
    // 강화 보너스 (현재는 껍데기 — 추후 새 강화 항목으로 의미 채움)
    // ============================================================
    [Header("강화 보너스 (런타임 누적, 인스펙터는 디버그용)")]
    [SerializeField, Tooltip("화살 최대 보유량 추가량. 강화로 누적.")]
    private int arrowsMaxBonus = 0;

    // ============================================================
    // 런타임 상태
    // ============================================================
    private int arrows;
    private PlayerCharacterType activeStance = PlayerCharacterType.Melee;
    private float swapCooldownTimer = 0f;

    // ============================================================
    // 전환 결과 정의
    // ============================================================

    /// <summary>
    /// TrySwap() 호출 시의 결과 분류.
    /// Denied   : 쿨다운으로 전환 거부.
    /// NormalSwap: 그냥 무기만 교체 (자원 변화 없음).
    /// TagSwap  : 태그 공격 발동 조건 충족 (광역기 호출 신호).
    /// </summary>
    public enum SwapResult
    {
        Denied,
        NormalSwap,
        TagSwap
    }

    // ============================================================
    // Public Getter
    // ============================================================

    /// <summary>현재 보유 화살 (0 ~ MaxArrows). 단일 자원.</summary>
    public int Arrows => arrows;

    /// <summary>강화 반영된 화살 최대 보유량.</summary>
    public int MaxArrows => baseMaxArrows + arrowsMaxBonus;

    /// <summary>현재 활성 스탠스 (Melee = 검, Ranged = 활).</summary>
    public PlayerCharacterType ActiveStance => activeStance;

    /// <summary>전환 쿨다운 진행 중 여부.</summary>
    public bool IsOnCooldown => swapCooldownTimer > 0f;

    /// <summary>
    /// 지금 Tab을 누르면 태그 공격이 발동될 조건인지 여부.
    /// 검 모드: 화살 풀 / 활 모드: 화살 0.
    /// UI 강조 표시용.
    /// </summary>
    public bool CanTagSwap
    {
        get
        {
            if (IsOnCooldown) return false;
            return activeStance == PlayerCharacterType.Melee
                ? arrows >= MaxArrows
                : arrows <= 0;
        }
    }

    // ============================================================
    // 이벤트
    // ============================================================

    /// <summary>
    /// 스탠스가 전환됐을 때 발생. 인자: (새 활성 스탠스, 태그 공격 전환 여부).
    /// 구독자: PlayerTagController, CombatUIManager, StanceGaugeUI, SoundManager, PlayerTagAttack.
    /// </summary>
    public event Action<PlayerCharacterType, bool> OnStanceSwapped;

    /// <summary>화살 보유량 변경 시 발생. 인자: (현재값, 최대값). UI 업데이트용.</summary>
    public event Action<int, int> OnArrowsChanged;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // 시작 상태: 검 모드 + 시작 화살 (기본 0 — 검으로 모아야 활 사용 가능)
        arrows = Mathf.Clamp(startingArrows, 0, MaxArrows);
        activeStance = PlayerCharacterType.Melee;
    }

    private void Start()
    {
        // 구독자들이 OnEnable에서 구독 마친 뒤 초기값 전달 (Awake에서 발행하면 구독 전이라 놓침)
        OnArrowsChanged?.Invoke(arrows, MaxArrows);
        OnStanceSwapped?.Invoke(activeStance, false);
    }

    private void Update()
    {
        UpdateSwapCooldown();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ============================================================
    // 쿨다운 갱신
    // ============================================================

    /// <summary>전환 쿨다운 타이머 매 프레임 감소.</summary>
    private void UpdateSwapCooldown()
    {
        if (swapCooldownTimer <= 0f) return;

        swapCooldownTimer -= Time.deltaTime;
        if (swapCooldownTimer < 0f) swapCooldownTimer = 0f;
    }

    // ============================================================
    // 자원 변동 API — 외부(공격 시스템)에서 호출
    // ============================================================

    /// <summary>
    /// 화살 적립. 검 공격 명중 시 호출 (좌=+1, 우=+2).
    /// 최대치 클램프 + 변경 시에만 이벤트 발행.
    /// </summary>
    public void AddArrows(int amount)
    {
        if (amount <= 0) return;

        int prev = arrows;
        arrows = Mathf.Clamp(arrows + amount, 0, MaxArrows);

        if (arrows != prev)
            OnArrowsChanged?.Invoke(arrows, MaxArrows);
    }

    /// <summary>
    /// 화살 소모 시도. 활 공격 직전 호출.
    /// 잔량이 부족하면 false 반환 → 호출자가 공격을 막아야 함.
    /// </summary>
    public bool ConsumeArrows(int amount)
    {
        if (amount <= 0) return true;
        if (arrows < amount) return false;

        arrows -= amount;
        OnArrowsChanged?.Invoke(arrows, MaxArrows);
        return true;
    }

    /// <summary>화살 잔량 사전 체크 (소모하지 않음). UI/예측 로직용.</summary>
    public bool HasEnoughArrows(int amount)
    {
        return arrows >= amount;
    }

    // ============================================================
    // 전환 API
    // ============================================================

    /// <summary>
    /// 무기 전환 시도. Tab 입력의 종착점.
    /// 
    /// 동작 흐름:
    ///   1. 쿨다운 중이면 Denied.
    ///   2. 자원 충족 시 TagSwap 반환 (광역기 발동 신호 — 자원 그대로 유지).
    ///   3. 자원 미충족 시 NormalSwap 반환 (그냥 모드 토글).
    ///   4. 두 경우 모두 활성 스탠스 토글 + 쿨다운 시작 + 이벤트 발행.
    /// 
    /// 자원 변환 로직 없음 — 화살은 단일 자원이라 변환 불필요.
    /// </summary>
    public SwapResult TrySwap()
    {
        if (IsOnCooldown)
        {
            Debug.Log($"[StanceGauge] 전환 거부 (쿨다운 {swapCooldownTimer:F2}s)");
            return SwapResult.Denied;
        }

        bool isTag = CanTagSwap;

        DoSwap(isTag);
        return isTag ? SwapResult.TagSwap : SwapResult.NormalSwap;
    }

    /// <summary>실제 스탠스 토글 + 이벤트 발행.</summary>
    private void DoSwap(bool isTagSwap)
    {
        activeStance = (activeStance == PlayerCharacterType.Melee)
            ? PlayerCharacterType.Ranged
            : PlayerCharacterType.Melee;

        swapCooldownTimer = swapCooldown;

        OnStanceSwapped?.Invoke(activeStance, isTagSwap);

        Debug.Log($"[StanceGauge] 전환 → {activeStance} ({(isTagSwap ? "TagSwap" : "NormalSwap")})");
    }

    // ============================================================
    // 리셋
    // ============================================================

    /// <summary>자원과 스탠스를 시작 상태로 리셋. RunManager/사망 처리에서 호출.</summary>
    public void ResetGauges()
    {
        arrows = Mathf.Clamp(startingArrows, 0, MaxArrows);
        activeStance = PlayerCharacterType.Melee;
        swapCooldownTimer = 0f;

        OnArrowsChanged?.Invoke(arrows, MaxArrows);
        OnStanceSwapped?.Invoke(activeStance, false);

        Debug.Log($"[StanceGauge] 자원/스탠스 리셋 (화살 {arrows}, 활성=Melee)");
    }

    // ============================================================
    // 강화 시스템 연동 (껍데기 — 추후 강화 항목 도입 시 의미 채움)
    // ============================================================

    /// <summary>강화로 화살 최대치 증가. 단일 자원이라 인자도 단순화.</summary>
    public void ApplyBoost(int arrowsMaxBonusAmt)
    {
        arrowsMaxBonus += arrowsMaxBonusAmt;
        OnArrowsChanged?.Invoke(arrows, MaxArrows);

        Debug.Log($"[StanceGauge] 강화 적용: arrowsMax+{arrowsMaxBonusAmt}");
    }

    /// <summary>강화 보너스 초기화. 런 리셋 시 호출.</summary>
    public void ResetBoosts()
    {
        arrowsMaxBonus = 0;
        OnArrowsChanged?.Invoke(arrows, MaxArrows);

        Debug.Log("[StanceGauge] 강화 보너스 초기화");
    }
}
