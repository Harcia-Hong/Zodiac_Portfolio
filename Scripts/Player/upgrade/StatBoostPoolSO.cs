using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 흩어진 서판 — 선택지 풀
/// 8개의 StatBoostEntrySO를 담고, 런타임에 랜덤 3개를 뽑아 제공
/// 등급은 확률 기반으로 결정
/// </summary>
[CreateAssetMenu(fileName = "StatBoostPool", menuName = "Game/Stat Boost Pool")]
public class StatBoostPoolSO : ScriptableObject
{
    [Header("선택지 목록 (8개)")]
    [Tooltip("서판에서 등장할 수 있는 스탯 강화 항목들")]
    public StatBoostEntrySO[] entries;

    [Header("등급 확률 (%)")]
    [Tooltip("흰색(Common) 출현 확률")]
    public float commonChance = 50f;

    [Tooltip("초록(Uncommon) 출현 확률")]
    public float uncommonChance = 30f;

    [Tooltip("보라(Rare) 출현 확률")]
    public float rareChance = 15f;

    [Tooltip("빨강(Legendary) 출현 확률")]
    public float legendaryChance = 5f;

    /// <summary>
    /// 풀에서 랜덤 n개를 중복 없이 뽑고, 각각 등급을 부여해서 반환
    /// </summary>
    public List<StatBoostSelection> GetRandomSelections(int count = 3)
    {
        List<StatBoostSelection> results = new List<StatBoostSelection>();

        if (entries == null || entries.Length == 0) return results;

        // 중복 방지용 인덱스 셔플
        List<int> indices = new List<int>();
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null)
                indices.Add(i);
        }

        // 필요한 수만큼 랜덤 추출
        count = Mathf.Min(count, indices.Count);

        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(i, indices.Count);

            // 스왑 (Fisher-Yates 셔플)
            int temp = indices[i];
            indices[i] = indices[randomIndex];
            indices[randomIndex] = temp;

            StatBoostEntrySO entry = entries[indices[i]];
            StatBoostGrade grade = RollGrade();

            results.Add(new StatBoostSelection(entry, grade));
        }

        return results;
    }

    /// <summary>
    /// 확률 기반 등급 결정
    /// </summary>
    private StatBoostGrade RollGrade()
    {
        float roll = Random.Range(0f, 100f);
        float cumulative = 0f;

        cumulative += legendaryChance;
        if (roll < cumulative) return StatBoostGrade.Legendary;

        cumulative += rareChance;
        if (roll < cumulative) return StatBoostGrade.Rare;

        cumulative += uncommonChance;
        if (roll < cumulative) return StatBoostGrade.Uncommon;

        return StatBoostGrade.Common;
    }

    /// <summary>
    /// 인스펙터 유효성 검증
    /// </summary>
    private void OnValidate()
    {
        float total = commonChance + uncommonChance + rareChance + legendaryChance;
        if (Mathf.Abs(total - 100f) > 0.1f)
        {
            Debug.LogWarning($"[StatBoostPool] 등급 확률 합이 100%가 아닙니다: {total}%");
        }
    }
}

/// <summary>
/// 선택지 1개 — 어떤 스탯 강화를 어떤 등급으로 제공하는지
/// </summary>
[System.Serializable]
public class StatBoostSelection
{
    public StatBoostEntrySO entry;
    public StatBoostGrade grade;

    public StatBoostSelection(StatBoostEntrySO entry, StatBoostGrade grade)
    {
        this.entry = entry;
        this.grade = grade;
    }
}
