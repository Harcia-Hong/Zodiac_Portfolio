using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SkillDropEntry
{
    public SkillData skillData;
    [Range(0f, 100f)] public float weight;
}

[CreateAssetMenu(fileName = "SkillLootTable", menuName = "Skill System/Skill Loot Table")]
public class SkillLootTable : ScriptableObject
{
    [Header("드랍 가능한 스킬 목록")]
    public List<SkillDropEntry> skillEntries;

    /// <summary>
    /// 해금된 스킬 중에서 가중치 기반 랜덤 뽑기
    /// - SkillUnlockManager가 없으면 전체 풀에서 뽑음 (에디터 테스트 호환)
    /// </summary>
    public SkillData GetRandomSkill()
    {
        if (skillEntries == null || skillEntries.Count == 0)
        {
            Debug.LogError("스킬 룻 테이블이 비어있습니다!");
            return null;
        }

        // 해금된 스킬만 필터링 (매니저 없으면 전체 대상)
        bool useUnlockFilter = SkillUnlockManager.Instance != null;
        List<SkillDropEntry> validEntries = new List<SkillDropEntry>();

        foreach (var entry in skillEntries)
        {
            if (entry.skillData == null) continue;

            if (!useUnlockFilter || SkillUnlockManager.Instance.IsUnlocked(entry.skillData))
            {
                validEntries.Add(entry);
            }
        }

        // 해금된 스킬이 하나도 없는 경우 방어 처리
        if (validEntries.Count == 0)
        {
            Debug.LogWarning("[SkillLootTable] 해금된 스킬이 없습니다! 기본 첫 번째 스킬 반환");
            return skillEntries[0].skillData;
        }

        // 가중치 기반 랜덤 뽑기
        float totalWeight = 0f;
        foreach (var entry in validEntries)
        {
            totalWeight += entry.weight;
        }

        float randomValue = Random.Range(0f, totalWeight);

        float currentWeightSum = 0f;
        foreach (var entry in validEntries)
        {
            currentWeightSum += entry.weight;
            if (randomValue <= currentWeightSum)
            {
                return entry.skillData;
            }
        }

        // 오류 방지 코드
        return validEntries[0].skillData;
    }

    /// <summary>
    /// 현재 해금된 모든 스킬 리스트 반환.
    /// 카드 보상 풀 만들기 등 후보 전체가 필요할 때 사용 (weight 무시, 결정적).
    /// SkillUnlockManager 없으면 전체 풀 반환 (에디터 테스트 호환).
    /// </summary>
    public List<SkillData> GetAllUnlockedSkills()
    {
        List<SkillData> result = new List<SkillData>();
        if (skillEntries == null) return result;

        bool useUnlockFilter = SkillUnlockManager.Instance != null;

        foreach (var entry in skillEntries)
        {
            if (entry.skillData == null) continue;

            if (!useUnlockFilter || SkillUnlockManager.Instance.IsUnlocked(entry.skillData))
            {
                result.Add(entry.skillData);
            }
        }

        return result;
    }
}
