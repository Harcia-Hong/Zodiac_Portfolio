using UnityEngine;

/// <summary>
/// 스킬 강화 레벨별 비용 데이터
/// ScriptableObject로 관리하여 Inspector에서 수정 가능
/// </summary>
[CreateAssetMenu(fileName = "SkillUpgradeCost", menuName = "Game/Skill Upgrade Cost")]
public class SkillUpgradeCost : ScriptableObject
{
    [Header("레벨별 강화 비용")]
    [Tooltip("인덱스 = 현재 레벨, 값 = 다음 레벨로 강화 비용")]
    [SerializeField]
    private int[] upgradeCosts = new int[20]
    {
        10,  // Lv.0 → Lv.1
        15,  // Lv.1 → Lv.2
        20,  // Lv.2 → Lv.3
        25,  // Lv.3 → Lv.4
        30,  // Lv.4 → Lv.5
        40,  // Lv.5 → Lv.6
        50,  // Lv.6 → Lv.7
        60,  // Lv.7 → Lv.8
        70,  // Lv.8 → Lv.9
        80,  // Lv.9 → Lv.10
        100, // Lv.10 → Lv.11
        120, // Lv.11 → Lv.12
        140, // Lv.12 → Lv.13
        160, // Lv.13 → Lv.14
        180, // Lv.14 → Lv.15
        200, // Lv.15 → Lv.16
        230, // Lv.16 → Lv.17
        260, // Lv.17 → Lv.18
        300, // Lv.18 → Lv.19
        350  // Lv.19 → Lv.20
    };

    /// <summary>
    /// 현재 레벨에서 다음 레벨로 강화하는 비용 반환
    /// </summary>
    public int GetCost(int currentLevel)
    {
        if (currentLevel < 0 || currentLevel >= upgradeCosts.Length)
        {
            Debug.LogWarning($"[SkillUpgradeCost] 잘못된 레벨: {currentLevel}");
            return 9999;
        }

        return upgradeCosts[currentLevel];
    }

    /// <summary>
    /// 최대 레벨 반환
    /// </summary>
    public int GetMaxLevel()
    {
        return upgradeCosts.Length;
    }
}
