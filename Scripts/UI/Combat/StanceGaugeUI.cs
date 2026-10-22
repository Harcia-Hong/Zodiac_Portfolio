using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 화살 자원 UI — 중앙 하단 5칸 인디케이터.
/// 
/// 동작:
///   각 칸 = 화살 1발. 검 모드에서 적립되어 채워지고, 활 모드에서 소모되어 비워짐.
///   활성 스탠스에 따라 채워진 칸 색상이 달라짐 (검=적립톤, 활=소모톤).
/// 
/// 데이터 흐름:
///   - 화살 값: StanceGaugeSystem.OnArrowsChanged 이벤트 구독
///   - 스탠스 전환: OnStanceSwapped 이벤트 구독 → 칸 색상 갱신
/// 
/// 책임 밖:
///   - 자원 관리 (StanceGaugeSystem)
///   - 스킬 슬롯 UI (CombatUIManager)
///   - HP/EXP UI (별도 컴포넌트)
/// </summary>
public class StanceGaugeUI : MonoBehaviour
{
    // ============================================================
    // Inspector - 칸 슬롯
    // ============================================================
    [Header("Arrow Slots (중앙 하단 5칸)")]
    [SerializeField, Tooltip("화살 칸 Image 배열. 인스펙터에서 5개(또는 MaxArrows만큼) 드래그.")]
    private Image[] arrowSlots;

    // ============================================================
    // Inspector - 색상
    // ============================================================
    [Header("Slot Colors")]
    [SerializeField, Tooltip("빈 칸 색상 (소모된/적립 전 상태). 어두운 회색 권장.")]
    private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);

    [SerializeField, Tooltip("검 모드일 때 채워진 칸 색상. 적립 톤 (보라/금색 권장).")]
    private Color meleeFillColor = new Color(0.85f, 0.65f, 0.20f, 1f);

    [SerializeField, Tooltip("활 모드일 때 채워진 칸 색상. 소모 톤 (청록/하늘색 권장).")]
    private Color rangedFillColor = new Color(0.30f, 0.80f, 1.00f, 1f);

    [SerializeField, Tooltip("화살 풀(태그 가능) 시 채워진 칸 색상. 노랑/금색 강조.")]
    private Color tagReadyColor = new Color(1.0f, 0.95f, 0.30f, 1f);

    // ============================================================
    // Inspector - 트윈 설정
    // ============================================================
    [Header("Tween Settings")]
    [SerializeField, Tooltip("칸 변경 시 트윈 시간 (초).")]
    private float tweenDuration = 0.2f;

    [SerializeField, Tooltip("칸 채워질 때 펀치 스케일 강도. 0이면 효과 없음.")]
    private float fillPunchScale = 0.3f;

    // ============================================================
    // Runtime State
    // ============================================================
    private bool isSubscribed = false;
    private int lastArrows = 0;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    private void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    private void OnDisable()
    {
        UnsubscribeFromSystem();

        // 트윈 정리
        if (arrowSlots != null)
        {
            foreach (var slot in arrowSlots)
            {
                if (slot != null)
                {
                    slot.DOKill();
                    slot.transform.DOKill();
                }
            }
        }
    }

    // ============================================================
    // 이벤트 구독/해제
    // ============================================================

    /// <summary>StanceGaugeSystem.Instance 대기 후 이벤트 구독 + 초기 상태 동기화.</summary>
    private IEnumerator SubscribeWhenReady()
    {
        while (StanceGaugeSystem.Instance == null) yield return null;

        StanceGaugeSystem sys = StanceGaugeSystem.Instance;
        sys.OnArrowsChanged += HandleArrowsChanged;
        sys.OnStanceSwapped += HandleStanceSwapped;
        isSubscribed = true;

        // 초기 상태 즉시 반영 (트윈 없이 스냅)
        lastArrows = sys.Arrows;
        SyncSlotsInstant(sys.Arrows, sys.ActiveStance);
    }

    private void UnsubscribeFromSystem()
    {
        if (StanceGaugeSystem.Instance != null)
        {
            StanceGaugeSystem.Instance.OnArrowsChanged -= HandleArrowsChanged;
            StanceGaugeSystem.Instance.OnStanceSwapped -= HandleStanceSwapped;
        }
        isSubscribed = false;
    }

    // ============================================================
    // 이벤트 핸들러
    // ============================================================

    /// <summary>화살 값 변경 — 칸 채움/비움 + 변경된 칸에 펀치 효과.</summary>
    private void HandleArrowsChanged(int current, int max)
    {
        if (arrowSlots == null) return;

        PlayerCharacterType stance = StanceGaugeSystem.Instance != null
            ? StanceGaugeSystem.Instance.ActiveStance
            : PlayerCharacterType.Melee;

        bool isIncrease = current > lastArrows;

        for (int i = 0; i < arrowSlots.Length; i++)
        {
            if (arrowSlots[i] == null) continue;

            bool shouldBeFilled = i < current;
            Color targetColor = GetSlotColor(shouldBeFilled, stance, current, max);

            arrowSlots[i].DOKill();
            arrowSlots[i].DOColor(targetColor, tweenDuration);

            // 새로 채워지는 칸 또는 새로 비워지는 칸에 펀치 효과
            bool wasFilled = i < lastArrows;
            if (shouldBeFilled != wasFilled && fillPunchScale > 0f)
            {
                arrowSlots[i].transform.DOKill();
                arrowSlots[i].transform.localScale = Vector3.one;
                arrowSlots[i].transform.DOPunchScale(Vector3.one * fillPunchScale, tweenDuration, 6, 0.5f);
            }
        }

        lastArrows = current;
    }

    /// <summary>스탠스 전환 — 모든 채워진 칸의 색상 갱신 (모드 색 전환).</summary>
    private void HandleStanceSwapped(PlayerCharacterType newStance, bool isTagSwap)
    {
        if (arrowSlots == null || StanceGaugeSystem.Instance == null) return;

        int current = StanceGaugeSystem.Instance.Arrows;
        int max = StanceGaugeSystem.Instance.MaxArrows;

        for (int i = 0; i < arrowSlots.Length; i++)
        {
            if (arrowSlots[i] == null) continue;

            bool shouldBeFilled = i < current;
            Color targetColor = GetSlotColor(shouldBeFilled, newStance, current, max);

            arrowSlots[i].DOKill();
            arrowSlots[i].DOColor(targetColor, tweenDuration);
        }
    }

    // ============================================================
    // Helpers
    // ============================================================

    /// <summary>
    /// 칸 하나의 목표 색상 결정.
    /// 채워진 칸 = 검/활 모드 색 (단, 화살 풀 시엔 tagReadyColor로 강조).
    /// 빈 칸 = emptyColor.
    /// </summary>
    private Color GetSlotColor(bool isFilled, PlayerCharacterType stance, int current, int max)
    {
        if (!isFilled) return emptyColor;

        // 태그 발동 조건 충족 시 강조
        // 검 모드: 화살 풀 / 활 모드: 화살 0 (근데 이 함수는 isFilled=true 분기라 활 0 케이스는 안 옴)
        bool isTagReady = (stance == PlayerCharacterType.Melee && current >= max);
        if (isTagReady) return tagReadyColor;

        return stance == PlayerCharacterType.Melee ? meleeFillColor : rangedFillColor;
    }

    /// <summary>초기 동기화 — 트윈 없이 즉시 색상 적용.</summary>
    private void SyncSlotsInstant(int current, PlayerCharacterType stance)
    {
        if (arrowSlots == null || StanceGaugeSystem.Instance == null) return;

        int max = StanceGaugeSystem.Instance.MaxArrows;

        for (int i = 0; i < arrowSlots.Length; i++)
        {
            if (arrowSlots[i] == null) continue;

            bool shouldBeFilled = i < current;
            arrowSlots[i].color = GetSlotColor(shouldBeFilled, stance, current, max);
            arrowSlots[i].transform.localScale = Vector3.one;
        }
    }
}
