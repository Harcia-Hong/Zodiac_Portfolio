using UnityEngine;

/// <summary>
/// 등급별 보상 풀
/// - 해당 등급에서 나올 수 있는 보상/디버프 목록
/// </summary>
[CreateAssetMenu(fileName = "RewardPool_", menuName = "Game/Reward Pool")]
public class RewardPoolSO : ScriptableObject
{
    [Header("등급 정보")]
    [Tooltip("보상 등급")]
    public RewardTier tier = RewardTier.Minor;

    [Tooltip("풀 이름 (디버깅용)")]
    public string poolName = "미미한 보상";

    [Header("보상 목록")]
    [Tooltip("가능한 보상들")]
    public RewardEntrySO[] possibleRewards;

    [Header("디버프 목록 (Minor는 비워둠)")]
    [Tooltip("가능한 디버프들")]
    public DebuffEntrySO[] possibleDebuffs;

    [Header("디버프 설정")]
    [Tooltip("적용할 디버프 개수")]
    [Range(0, 3)]
    public int debuffCount = 0;

    [Tooltip("디버프 개수 랜덤 범위 사용")]
    public bool useRandomDebuffCount = false;

    [Tooltip("최소 디버프 개수")]
    [Range(0, 3)]
    public int minDebuffCount = 0;

    [Tooltip("최대 디버프 개수")]
    [Range(0, 3)]
    public int maxDebuffCount = 1;

    /// <summary>
    /// 랜덤 보상 1개 선택
    /// </summary>
    public RewardEntrySO GetRandomReward()
    {
        if (possibleRewards == null || possibleRewards.Length == 0)
        {
            Debug.LogWarning($"[RewardPool] {poolName}: 보상 목록이 비어있습니다.");
            return null;
        }

        int index = Random.Range(0, possibleRewards.Length);
        return possibleRewards[index];
    }

    /// <summary>
    /// 랜덤 디버프 여러 개 선택
    /// </summary>
    public DebuffEntrySO[] GetRandomDebuffs()
    {
        if (possibleDebuffs == null || possibleDebuffs.Length == 0)
        {
            return new DebuffEntrySO[0];
        }

        // 디버프 개수 결정
        int count = useRandomDebuffCount
            ? Random.Range(minDebuffCount, maxDebuffCount + 1)
            : debuffCount;

        // 개수가 0이면 빈 배열 반환
        if (count <= 0)
        {
            return new DebuffEntrySO[0];
        }

        // 가능한 디버프보다 많이 요청하면 조정
        count = Mathf.Min(count, possibleDebuffs.Length);

        // 중복 없이 랜덤 선택
        DebuffEntrySO[] selected = new DebuffEntrySO[count];
        System.Collections.Generic.List<int> usedIndices = new System.Collections.Generic.List<int>();

        for (int i = 0; i < count; i++)
        {
            int index;
            do
            {
                index = Random.Range(0, possibleDebuffs.Length);
            }
            while (usedIndices.Contains(index));

            usedIndices.Add(index);
            selected[i] = possibleDebuffs[index];
        }

        return selected;
    }

    /// <summary>
    /// 보상 + 디버프 함께 적용
    /// </summary>
    public void ApplyRandomRewardWithDebuffs()
    {
        // 보상 적용
        RewardEntrySO reward = GetRandomReward();
        if (reward != null)
        {
            reward.Apply();
        }

        // 디버프 적용
        DebuffEntrySO[] debuffs = GetRandomDebuffs();
        foreach (var debuff in debuffs)
        {
            if (debuff != null)
            {
                debuff.Apply();
            }
        }
    }

    /// <summary>
    /// 보상 풀 요약 텍스트
    /// </summary>
    public string GetPoolDescription()
    {
        string desc = $"[{poolName}]\n";
        desc += $"보상 종류: {possibleRewards?.Length ?? 0}개\n";

        if (possibleDebuffs != null && possibleDebuffs.Length > 0)
        {
            int count = useRandomDebuffCount
                ? maxDebuffCount
                : debuffCount;
            desc += $"디버프: {count}개 (총 {possibleDebuffs.Length}종)";
        }
        else
        {
            desc += "디버프: 없음";
        }

        return desc;
    }
}
