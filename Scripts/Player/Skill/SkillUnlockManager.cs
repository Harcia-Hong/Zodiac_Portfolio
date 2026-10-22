using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 해금 상태를 관리하는 영구 싱글톤
/// 
/// 역할:
///   - 스킬 해금 여부 확인 / 해금 처리 / 해금 목록 반환
///   - PlayerPrefs로 해금 상태 영속 저장 (런 리셋 영향 없음)
///   - 이벤트로 해금 알림 (UI, 연출 등은 외부에서 구독)
/// 
/// 사용 예:
///   SkillUnlockManager.Instance.TryUnlock(someSkillData);
///   bool ok = SkillUnlockManager.Instance.IsUnlocked(someSkillData);
///   List&lt;SkillData&gt; list = SkillUnlockManager.Instance.GetUnlockedSkills();
/// </summary>
public class SkillUnlockManager : MonoBehaviour
{
    public static SkillUnlockManager Instance { get; private set; }

    [Header("해금 데이터")]
    [SerializeField, Tooltip("전체 스킬 해금 조건 테이블")]
    private SkillUnlockData unlockData;

    // === 런타임 해금 상태 (스킬 이름 기반 HashSet, O(1) 검색) ===
    private HashSet<string> unlockedSkillIds = new HashSet<string>();

    // === 이벤트: 스킬 해금 시 외부 알림 (Observer 패턴) ===
    /// <summary>
    /// 스킬 해금 시 발행되는 이벤트
    /// - UI 갱신, 사운드, 연출 등에서 구독
    /// </summary>
    public event Action<SkillData> OnSkillUnlocked;

    // === PlayerPrefs 저장 키 ===
    private const string SAVE_KEY = "UnlockedSkills";
    private const char SEPARATOR = '|';

    #region Unity Lifecycle

    private void Awake()
    {
        // DontDestroyOnLoad 싱글톤
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region 초기화

    /// <summary>
    /// 저장된 해금 상태 로드 + 기본 해금 스킬 등록
    /// </summary>
    private void Initialize()
    {
        Load();
        RegisterDefaultUnlocks();
    }

    /// <summary>
    /// unlockedByDefault가 true인 스킬들을 자동 해금 등록
    /// - 첫 실행 시 기본 스킬이 해금 상태로 시작하도록 보장
    /// </summary>
    private void RegisterDefaultUnlocks()
    {
        if (unlockData == null) return;

        foreach (var entry in unlockData.unlockEntries)
        {
            if (entry.unlockedByDefault && entry.skill != null)
            {
                string skillId = GetSkillId(entry.skill);
                unlockedSkillIds.Add(skillId);
            }
        }

        // 기본 해금 등록 후 저장
        Save();
    }

    #endregion

    #region 해금 조회

    /// <summary>
    /// 해당 스킬이 해금되었는지 확인
    /// </summary>
    public bool IsUnlocked(SkillData skill)
    {
        if (skill == null) return false;
        return unlockedSkillIds.Contains(GetSkillId(skill));
    }

    /// <summary>
    /// 해당 스킬을 해금할 수 있는지 확인
    /// - 미해금 상태 + 별가루 충분
    /// </summary>
    public bool CanUnlock(SkillData skill)
    {
        if (skill == null) return false;
        if (IsUnlocked(skill)) return false;

        SkillUnlockEntry entry = FindEntry(skill);
        if (entry == null) return false;

        // 별가루 확인
        if (PlayerStats.Instance == null) return false;
        return PlayerStats.Instance.Stardust >= entry.stardustCost;
    }

    /// <summary>
    /// 해금에 필요한 별가루 비용 반환
    /// - 등록되지 않은 스킬이면 -1 반환
    /// </summary>
    public int GetUnlockCost(SkillData skill)
    {
        SkillUnlockEntry entry = FindEntry(skill);
        return entry != null ? entry.stardustCost : -1;
    }

    /// <summary>
    /// 현재 해금된 모든 스킬 목록 반환
    /// </summary>
    public List<SkillData> GetUnlockedSkills()
    {
        List<SkillData> result = new List<SkillData>();

        if (unlockData == null) return result;

        foreach (var entry in unlockData.unlockEntries)
        {
            if (entry.skill != null && IsUnlocked(entry.skill))
            {
                result.Add(entry.skill);
            }
        }

        return result;
    }

    /// <summary>
    /// 현재 미해금된 모든 스킬 목록 반환
    /// - 해금소 UI에서 사용
    /// </summary>
    public List<SkillUnlockEntry> GetLockedEntries()
    {
        List<SkillUnlockEntry> result = new List<SkillUnlockEntry>();

        if (unlockData == null) return result;

        foreach (var entry in unlockData.unlockEntries)
        {
            if (entry.skill != null && !IsUnlocked(entry.skill))
            {
                result.Add(entry);
            }
        }

        return result;
    }

    #endregion

    #region 해금 처리

    /// <summary>
    /// 스킬 해금 시도
    /// - 별가루 차감 → 해금 상태 등록 → 저장 → 이벤트 발행
    /// </summary>
    /// <returns>해금 성공 여부</returns>
    public bool TryUnlock(SkillData skill)
    {
        // 1. 해금 가능 여부 확인
        if (!CanUnlock(skill))
        {
            Debug.Log($"[SkillUnlockManager] '{skill.skillName}' 해금 불가 (이미 해금 또는 별가루 부족)");
            return false;
        }

        // 2. 비용 조회
        SkillUnlockEntry entry = FindEntry(skill);

        // 3. 별가루 차감
        if (!PlayerStats.Instance.UseStardust(entry.stardustCost))
        {
            Debug.LogWarning($"[SkillUnlockManager] 별가루 차감 실패");
            return false;
        }

        // 4. 해금 상태 등록
        string skillId = GetSkillId(skill);
        unlockedSkillIds.Add(skillId);

        // 5. 영속 저장
        Save();

        // 6. 이벤트 발행
        OnSkillUnlocked?.Invoke(skill);

        Debug.Log($"[SkillUnlockManager] '{skill.skillName}' 해금 완료! (별가루 -{entry.stardustCost})");
        return true;
    }

    #endregion

    #region 저장 / 불러오기

    /// <summary>
    /// 해금 상태를 PlayerPrefs에 저장
    /// - 스킬 이름들을 구분자로 연결한 문자열로 저장
    /// </summary>
    private void Save()
    {
        string saveData = string.Join(SEPARATOR.ToString(), unlockedSkillIds);
        PlayerPrefs.SetString(SAVE_KEY, saveData);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// PlayerPrefs에서 해금 상태 로드
    /// </summary>
    private void Load()
    {
        unlockedSkillIds.Clear();

        if (!PlayerPrefs.HasKey(SAVE_KEY)) return;

        string saveData = PlayerPrefs.GetString(SAVE_KEY);
        if (string.IsNullOrEmpty(saveData)) return;

        string[] ids = saveData.Split(SEPARATOR);
        foreach (string id in ids)
        {
            if (!string.IsNullOrEmpty(id))
            {
                unlockedSkillIds.Add(id);
            }
        }

        Debug.Log($"[SkillUnlockManager] 저장 데이터 로드 완료 — {unlockedSkillIds.Count}개 스킬 해금됨");
    }

    /// <summary>
    /// 해금 데이터 전체 초기화 (디버그/테스트용)
    /// </summary>
    public void ResetAllUnlocks()
    {
        unlockedSkillIds.Clear();
        PlayerPrefs.DeleteKey(SAVE_KEY);
        RegisterDefaultUnlocks();
        Debug.Log("[SkillUnlockManager] 모든 해금 데이터 초기화 완료");
    }

    #endregion

    #region 유틸리티

    /// <summary>
    /// SkillData에서 고유 식별자 추출
    /// - SO의 name(에셋 파일명)을 사용하여 안정적인 키 생성
    /// </summary>
    private string GetSkillId(SkillData skill)
    {
        return skill.name;
    }

    /// <summary>
    /// SkillUnlockData에서 해당 스킬의 엔트리 검색
    /// </summary>
    private SkillUnlockEntry FindEntry(SkillData skill)
    {
        if (unlockData == null || skill == null) return null;

        string targetId = GetSkillId(skill);

        foreach (var entry in unlockData.unlockEntries)
        {
            if (entry.skill != null && GetSkillId(entry.skill) == targetId)
            {
                return entry;
            }
        }

        return null;
    }

    #endregion
}