using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// "지식의 서고" 영구 강화 시스템 매니저.
/// 모든 강화 항목의 레벨을 관리하고, PlayerPrefs로 영구 저장하며,
/// 런 시작 시 강화 수치를 PlayerStats에 반영한다.
/// </summary>
public class KnowledgeArchiveManager : MonoBehaviour
{
    public static KnowledgeArchiveManager Instance { get; private set; }

    [Header("강화 항목 목록")]
    [Tooltip("모든 StatUpgradeDataSO를 여기에 등록")]
    [SerializeField] private List<StatUpgradeDataSO> allUpgrades = new List<StatUpgradeDataSO>();

    // 각 항목의 현재 레벨 (key: SO 이름, value: 현재 레벨)
    private Dictionary<string, int> currentLevels = new Dictionary<string, int>();

    // 강화 성공 시 UI 갱신용 이벤트
    public event System.Action OnUpgradeChanged;

    // --- PlayerPrefs 키 접두사 ---
    private const string SAVE_PREFIX = "KA_Level_";
    private const string TOTAL_UPGRADES_KEY = "KA_TotalUpgrades";

    /// <summary>총 강화 횟수 (??? 항목 해금 조건용)</summary>
    public int TotalUpgradeCount { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAllLevels();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ============================
    //  데이터 접근
    // ============================

    /// <summary>
    /// 특정 탭에 해당하는 강화 항목 목록을 정렬하여 반환
    /// </summary>
    public List<StatUpgradeDataSO> GetUpgradesByTab(UpgradeTabType tab)
    {
        return allUpgrades
            .Where(u => u.tabType == tab)
            .OrderBy(u => u.sortOrder)
            .ToList();
    }

    /// <summary>
    /// 특정 항목의 현재 레벨을 반환
    /// </summary>
    public int GetCurrentLevel(StatUpgradeDataSO data)
    {
        string key = data.name;
        return currentLevels.ContainsKey(key) ? currentLevels[key] : 0;
    }

    /// <summary>
    /// 특정 항목이 최대 레벨인지 확인
    /// </summary>
    public bool IsMaxLevel(StatUpgradeDataSO data)
    {
        return GetCurrentLevel(data) >= data.maxLevel;
    }

    /// <summary>
    /// 특정 항목이 현재 플레이어에게 보이는지 확인
    /// (기본 공개이거나, 총 강화 횟수 조건 충족 시 공개)
    /// </summary>
    public bool IsVisible(StatUpgradeDataSO data)
    {
        if (data.isVisibleByDefault) return true;
        return TotalUpgradeCount >= data.requiredTotalUpgrades;
    }

    /// <summary>
    /// 특정 항목을 강화할 수 있는지 확인
    /// (최대 레벨 미만 + 별가루 충분 + 항목 공개 상태)
    /// </summary>
    public bool CanUpgrade(StatUpgradeDataSO data)
    {
        if (!IsVisible(data)) return false;
        if (IsMaxLevel(data)) return false;

        int nextLevel = GetCurrentLevel(data) + 1;
        int cost = data.GetCostForLevel(nextLevel);
        return PlayerStats.Instance != null && PlayerStats.Instance.Stardust >= cost;
    }

    // ============================
    //  강화 실행
    // ============================

    /// <summary>
    /// 강화를 실행한다.
    /// 별가루 차감 → 레벨 증가 → 저장 → 스탯 적용 → 이벤트 발행
    /// </summary>
    public bool TryUpgrade(StatUpgradeDataSO data)
    {
        if (!CanUpgrade(data)) return false;

        int nextLevel = GetCurrentLevel(data) + 1;
        int cost = data.GetCostForLevel(nextLevel);

        // 별가루 차감
        if (!PlayerStats.Instance.UseStardust(cost)) return false;

        // 레벨 증가
        string key = data.name;
        currentLevels[key] = nextLevel;
        TotalUpgradeCount++;

        // 저장
        SaveLevel(key, nextLevel);
        PlayerPrefs.SetInt(TOTAL_UPGRADES_KEY, TotalUpgradeCount);
        PlayerPrefs.Save();

        // 즉시 스탯 적용
        ApplySingleUpgrade(data, nextLevel);

        // UI 갱신 이벤트
        OnUpgradeChanged?.Invoke();

        Debug.Log($"[지식의 서고] {data.upgradeName} 강화 완료! Lv.{nextLevel}/{data.maxLevel}");
        return true;
    }

    // ============================
    //  스탯 적용
    // ============================

    /// <summary>
    /// 런 시작 시 호출. 모든 영구 강화를 PlayerStats에 반영한다.
    /// </summary>
    public void ApplyAllUpgradesToPlayer()
    {
        if (PlayerStats.Instance == null)
        {
            Debug.LogWarning("[지식의 서고] PlayerStats.Instance가 없어 스탯 적용 불가");
            return;
        }

        foreach (var data in allUpgrades)
        {
            int level = GetCurrentLevel(data);
            if (level <= 0) continue;

            ApplySingleUpgrade(data, level);
        }

        Debug.Log("[지식의 서고] 모든 영구 강화 스탯 적용 완료");
    }

    /// <summary>
    /// 단일 강화 항목을 PlayerStats에 적용
    /// </summary>
    private void ApplySingleUpgrade(StatUpgradeDataSO data, int level)
    {
        if (PlayerStats.Instance == null) return;

        float value = data.GetValueAtLevel(level);

        switch (data.statType)
        {
            // --- 기본 스탯 ---
            case StatUpgradeType.AttackPower:
                PlayerStats.Instance.AddAttackPower((int)value);
                break;
            case StatUpgradeType.MaxHP:
                PlayerStats.Instance.AddMaxHP((int)value);
                break;
            case StatUpgradeType.SpellPower:
                PlayerStats.Instance.AddSpellPower((int)value);
                break;
            case StatUpgradeType.AttackSpeed:
                PlayerStats.Instance.AddStat("AS", value / 100f);
                break;
            case StatUpgradeType.CriticalRate:
                PlayerStats.Instance.AddCriticalRate(value);
                break;
            case StatUpgradeType.CooldownReduction:
                PlayerStats.Instance.AddCooldownReduction(value);
                break;
            case StatUpgradeType.MoveSpeed:
                PlayerStats.Instance.AddMoveSpeed(value / 100f * PlayerStats.Instance.MoveSpeed);
                break;

            // --- 특수 능력 ---
            // 특수 능력은 각 시스템에서 직접 매니저를 참조하여 값을 읽는다.
            // 여기서는 적용하지 않고, GetSpecialValue()로 런타임에 조회.
            case StatUpgradeType.HealOnKill:
            case StatUpgradeType.Revive:
            case StatUpgradeType.GoldBonus:
            case StatUpgradeType.BossDamage:
            case StatUpgradeType.DropRate:
            case StatUpgradeType.DashDistance:
            case StatUpgradeType.InvincibleTime:
            case StatUpgradeType.ExtraDash:
            case StatUpgradeType.ExtraSkillSlot:
                // 특수 능력은 별도 처리 — GetSpecialValue()로 조회
                break;
        }
    }

    /// <summary>
    /// 특수 능력의 현재 수치를 반환.
    /// 각 시스템(전투, 상점 등)에서 필요할 때 호출.
    /// 예: float healAmount = KnowledgeArchiveManager.Instance.GetSpecialValue(StatUpgradeType.HealOnKill);
    /// </summary>
    public float GetSpecialValue(StatUpgradeType type)
    {
        var data = allUpgrades.FirstOrDefault(u => u.statType == type);
        if (data == null) return 0f;

        int level = GetCurrentLevel(data);
        if (level <= 0) return 0f;

        return data.GetValueAtLevel(level);
    }

    /// <summary>
    /// 특수 능력이 해금되었는지 확인 (레벨 1 이상이면 해금)
    /// </summary>
    public bool IsSpecialUnlocked(StatUpgradeType type)
    {
        return GetSpecialValue(type) > 0f;
    }

    // ============================
    //  저장 / 불러오기
    // ============================

    /// <summary>
    /// 모든 항목의 레벨을 PlayerPrefs에서 불러온다
    /// </summary>
    private void LoadAllLevels()
    {
        currentLevels.Clear();

        foreach (var data in allUpgrades)
        {
            string key = data.name;
            int level = PlayerPrefs.GetInt(SAVE_PREFIX + key, 0);
            currentLevels[key] = level;
        }

        TotalUpgradeCount = PlayerPrefs.GetInt(TOTAL_UPGRADES_KEY, 0);

        Debug.Log($"[지식의 서고] 저장 데이터 로드 완료 — 총 강화 횟수: {TotalUpgradeCount}");
    }

    /// <summary>
    /// 단일 항목의 레벨을 PlayerPrefs에 저장
    /// </summary>
    private void SaveLevel(string key, int level)
    {
        PlayerPrefs.SetInt(SAVE_PREFIX + key, level);
    }

    /// <summary>
    /// 모든 강화를 초기화 (디버그/테스트용)
    /// </summary>
    [ContextMenu("모든 강화 초기화")]
    public void ResetAllUpgrades()
    {
        foreach (var data in allUpgrades)
        {
            string key = data.name;
            PlayerPrefs.DeleteKey(SAVE_PREFIX + key);
            currentLevels[key] = 0;
        }

        TotalUpgradeCount = 0;
        PlayerPrefs.SetInt(TOTAL_UPGRADES_KEY, 0);
        PlayerPrefs.Save();

        OnUpgradeChanged?.Invoke();
        Debug.Log("[지식의 서고] 모든 강화 초기화 완료");
    }
}