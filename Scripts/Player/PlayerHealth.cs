using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 체력 + 베리어 시스템.
/// 
/// HP는 PlayerStats로 위임. 베리어는 PlayerHealth가 직접 관리.
/// 
/// 베리어 동작:
///   - ApplyBarrier로 부여 (양 + 지속시간 + VFX, ApplyBarrierAction에서 호출)
///   - TakeDamage 시 베리어 먼저 흡수, 남는 데미지만 PlayerStats로 전달
///   - 지속시간 만료 또는 잔량 0 → 자동 해제 + VFX 제거
///   - OnBarrierChanged 이벤트로 외부 알림 + UI fill 자체 갱신
/// 
/// UI 갱신:
///   - barrierBarImage(흰색 fill)를 maxHP 기준 비율로 표시
///   - HP_bar와 같은 위치/크기에 덧씌워지는 구조
/// 
/// 책임 밖:
///   - HP 실제 관리 (PlayerStats)
///   - 시전 사운드 (ApplyBarrierAction이 직접 재생)
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    // ============================================================
    // HP — PlayerStats로 위임
    // ============================================================
    public int maxHealth => PlayerStats.Instance != null ? PlayerStats.Instance.MaxHP : 100;
    public int curHealth => PlayerStats.Instance != null ? PlayerStats.Instance.CurrentHP : 100;
    public bool isDead => PlayerStats.Instance != null ? PlayerStats.Instance.isDead : false;

    // ============================================================
    // UI 연결
    // ============================================================
    [Header("UI 연결")]
    [SerializeField, Tooltip("HP 바 위에 덧씌워질 흰색 베리어 fill. Image Type=Filled / Horizontal / Left 필수.")]
    private Image barrierBarImage;

    // ============================================================
    // 베리어 상태
    // ============================================================
    private int currentBarrier = 0;
    private float remainingBarrierTime = 0f;
    private GameObject activeBarrierVFX;

    /// <summary>현재 베리어 잔량.</summary>
    public int CurrentBarrier => currentBarrier;

    /// <summary>베리어 활성 여부.</summary>
    public bool HasBarrier => currentBarrier > 0 && remainingBarrierTime > 0f;

    /// <summary>베리어 잔량 변화 이벤트. 외부 UI/시스템 갱신용. 인자: 현재 잔량.</summary>
    public event Action<int> OnBarrierChanged;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    private void Update()
    {
        UpdateBarrierTime();
    }

    // ============================================================
    // 데미지/회복 — 베리어 가로채기 포함
    // ============================================================

    /// <summary>피격 처리. 베리어 활성 시 먼저 흡수, 남는 데미지만 HP로 전달.</summary>
    public void TakeDamage(int damage)
    {
        if (PlayerStats.Instance == null)
        {
            Debug.LogError("[PlayerHealth] PlayerStats.Instance가 없습니다!");
            return;
        }

        // 베리어 가로채기
        damage = AbsorbWithBarrier(damage);
        if (damage <= 0) return;

        PlayerStats.Instance.TakeDamage(damage);
    }

    public void Heal(int amount)
    {
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.Heal(amount);
        }
    }

    // ============================================================
    // 베리어 Public API
    // ============================================================

    /// <summary>
    /// 베리어 부여. ApplyBarrierAction에서 호출.
    /// 기존 베리어가 있으면 덮어씀 (쿨다운 > 지속시간이라 사실상 재시전 없음).
    /// </summary>
    public void ApplyBarrier(int amount, float duration, GameObject vfxPrefab)
    {
        if (amount <= 0 || duration <= 0f)
        {
            Debug.LogWarning("[PlayerHealth] amount/duration이 0 이하 — 베리어 부여 무시");
            return;
        }

        // 기존 VFX 정리 (덮어쓰기)
        ClearBarrierVFX();

        currentBarrier = amount;
        remainingBarrierTime = duration;

        SpawnBarrierVFX(vfxPrefab);
        NotifyBarrierChanged();

        Debug.Log($"[PlayerHealth] 베리어 부여: {amount}, {duration}s");
    }

    // ============================================================
    // 내부 처리
    // ============================================================

    /// <summary>베리어 데미지 흡수 — 남는 데미지 반환.</summary>
    private int AbsorbWithBarrier(int damage)
    {
        if (!HasBarrier) return damage;

        if (damage <= currentBarrier)
        {
            // 베리어가 전부 흡수
            currentBarrier -= damage;
            NotifyBarrierChanged();
            if (currentBarrier <= 0) ExpireBarrier();
            return 0;
        }
        else
        {
            // 베리어 깎이고 남는 데미지 반환
            int remaining = damage - currentBarrier;
            currentBarrier = 0;
            NotifyBarrierChanged();
            ExpireBarrier();
            return remaining;
        }
    }

    /// <summary>지속시간 만료 체크. 매 프레임.</summary>
    private void UpdateBarrierTime()
    {
        if (remainingBarrierTime <= 0f) return;

        remainingBarrierTime -= Time.deltaTime;
        if (remainingBarrierTime <= 0f) ExpireBarrier();
    }

    /// <summary>베리어 해제 (만료/깨짐 공통). VFX 정리 + 이벤트 발행.</summary>
    private void ExpireBarrier()
    {
        ClearBarrierVFX();
        currentBarrier = 0;
        remainingBarrierTime = 0f;
        NotifyBarrierChanged();
        Debug.Log("[PlayerHealth] 베리어 해제");
    }

    private void SpawnBarrierVFX(GameObject vfxPrefab)
    {
        if (vfxPrefab == null) return;
        activeBarrierVFX = Instantiate(vfxPrefab, transform.position, transform.rotation, transform);
    }

    private void ClearBarrierVFX()
    {
        if (activeBarrierVFX != null)
        {
            Destroy(activeBarrierVFX);
            activeBarrierVFX = null;
        }
    }

    // ============================================================
    // 변경 통보 + UI 갱신
    // ============================================================

    /// <summary>베리어 변경 통보 — 이벤트 발행 + UI fill 갱신을 한 곳에서.</summary>
    private void NotifyBarrierChanged()
    {
        OnBarrierChanged?.Invoke(currentBarrier);
        UpdateBarrierUI();
    }

    /// <summary>베리어 fill 갱신 — maxHP 기준 비율 표시. maxHP 초과 시 1로 클램프.</summary>
    private void UpdateBarrierUI()
    {
        if (barrierBarImage == null) return;

        float ratio = (float)currentBarrier / Mathf.Max(1, maxHealth);
        barrierBarImage.fillAmount = Mathf.Clamp01(ratio);
    }
}
