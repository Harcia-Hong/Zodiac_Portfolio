using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전리품 드랍 풀의 개별 항목
/// - 어떤 전리품이 얼마나 자주 나오는지 정의
/// </summary>
[System.Serializable]
public class TrophyDropEntry
{
    [Tooltip("드랍할 전리품 데이터")]
    public TrophyItemData trophyData;

    [Tooltip("드랍 가중치 (높을수록 자주 나옴)")]
    [Range(0f, 100f)]
    public float weight;
}

/// <summary>
/// 전리품 드랍 테이블
/// - 스테이지/상자별로 다른 테이블을 할당하여 드랍 풀을 다르게 설정 가능
/// - 가중치 기반 랜덤으로 전리품 1개를 뽑아 반환
/// 
/// 사용처:
///   - TrophyRewardChest: 상자 열 때 이 테이블에서 전리품 뽑기
///   - StageData: 스테이지별로 다른 테이블 지정 가능
/// </summary>
[CreateAssetMenu(fileName = "TrophyLootTable", menuName = "Loot System/Trophy Loot Table")]
public class TrophyLootTable : ScriptableObject
{
    [Header("드랍 가능한 전리품 목록")]
    public List<TrophyDropEntry> trophyEntries;

    /// <summary>
    /// 가중치 기반 랜덤으로 전리품 1개 뽑기
    /// </summary>
    public TrophyItemData GetRandomTrophy()
    {
        if (trophyEntries == null || trophyEntries.Count == 0)
        {
            Debug.LogWarning("[TrophyLootTable] 전리품 테이블이 비어있습니다!");
            return null;
        }

        // 전체 가중치 합산
        float totalWeight = 0f;
        foreach (var entry in trophyEntries)
        {
            if (entry.trophyData != null)
                totalWeight += entry.weight;
        }

        if (totalWeight <= 0f)
        {
            Debug.LogWarning("[TrophyLootTable] 유효한 전리품이 없습니다!");
            return null;
        }

        // 가중치 기반 랜덤 선택
        float randomValue = Random.Range(0f, totalWeight);
        float currentSum = 0f;

        foreach (var entry in trophyEntries)
        {
            if (entry.trophyData == null) continue;

            currentSum += entry.weight;
            if (randomValue <= currentSum)
            {
                return entry.trophyData;
            }
        }

        // 오류 방지
        return trophyEntries[0].trophyData;
    }

    /// <summary>
    /// 전리품 여러 개를 한번에 뽑기 (중복 허용)
    /// - 상자에서 전리품 2~3개 나올 때 사용
    /// </summary>
    public List<TrophyItemData> GetRandomTrophies(int count)
    {
        List<TrophyItemData> results = new List<TrophyItemData>();

        for (int i = 0; i < count; i++)
        {
            TrophyItemData trophy = GetRandomTrophy();
            if (trophy != null)
                results.Add(trophy);
        }

        return results;
    }
}