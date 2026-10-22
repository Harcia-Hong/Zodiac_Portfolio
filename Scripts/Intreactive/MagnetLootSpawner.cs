using UnityEngine;

/// <summary>
/// 자석형 루트 스폰 관리자 (싱글톤)
/// 몬스터 사망, 오브젝트 파괴 시 MagnetLoot 생성
/// </summary>
public class MagnetLootSpawner : MonoBehaviour
{
    #region Singleton
    public static MagnetLootSpawner Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    #endregion

    #region Settings
    [Header("프리팹")]
    [Tooltip("MagnetLoot 프리팹 (색상은 코드에서 변경)")]
    [SerializeField] private GameObject magnetLootPrefab;

    [Header("경험치 설정")]
    [SerializeField] private int expPerOrb = 5;
    [SerializeField] private int expOrbCount = 3;

    [Header("별빛 조각 설정")]
    [SerializeField] private int stardustPerOrb = 1;
    [SerializeField] private int stardustOrbCountMin = 2;
    [SerializeField] private int stardustOrbCountMax = 5;

    [Header("골드 설정")]
    [SerializeField] private int goldPerOrb = 1;
    [SerializeField] private int goldOrbCountMin = 3;
    [SerializeField] private int goldOrbCountMax = 8;

    [Header("스폰 오프셋")]
    [Tooltip("스폰 시 Y축 오프셋 (바닥에서 살짝 위)")]
    [SerializeField] private float spawnHeightOffset = 0.5f;
    #endregion

    #region Public Methods - Monster Death
    /// <summary>
    /// 몬스터 사망 시 루트 스폰
    /// 경험치는 확정, 별빛/골드는 확률
    /// </summary>
    /// <param name="position">몬스터 위치</param>
    /// <param name="expAmount">총 경험치량</param>
    /// <param name="stardustChance">별빛 드롭 확률 (0~100)</param>
    /// <param name="goldChance">골드 드롭 확률 (0~100)</param>
    public void SpawnFromMonster(Vector3 position, int expAmount, float stardustChance, float goldChance)
    {
        Vector3 spawnPos = position + Vector3.up * spawnHeightOffset;

        // 1. 경험치 (확정)
        SpawnExperience(spawnPos, expAmount);

        // 2. 별빛 조각 (확률)
        if (RollChance(stardustChance))
        {
            int orbCount = Random.Range(stardustOrbCountMin, stardustOrbCountMax + 1);
            SpawnMultipleLoot(spawnPos, MagnetLoot.LootType.Stardust, stardustPerOrb, orbCount);
        }

        // 3. 골드 (확률)
        if (RollChance(goldChance))
        {
            int orbCount = Random.Range(goldOrbCountMin, goldOrbCountMax + 1);
            SpawnMultipleLoot(spawnPos, MagnetLoot.LootType.Gold, goldPerOrb, orbCount);
        }
    }
    #endregion

    #region Public Methods - Object Break
    /// <summary>
    /// 별빛 오브젝트 파괴 시 별빛 조각 스폰
    /// </summary>
    /// <param name="position">오브젝트 위치</param>
    /// <param name="totalAmount">총 별빛 조각 수량</param>
    public void SpawnStardust(Vector3 position, int totalAmount)
    {
        Vector3 spawnPos = position + Vector3.up * spawnHeightOffset;

        int orbCount = Mathf.Max(1, totalAmount / stardustPerOrb);
        SpawnMultipleLoot(spawnPos, MagnetLoot.LootType.Stardust, stardustPerOrb, orbCount);
    }

    /// <summary>
    /// 골드 오브젝트 파괴 시 골드 스폰
    /// </summary>
    /// <param name="position">오브젝트 위치</param>
    /// <param name="totalAmount">총 골드 수량</param>
    public void SpawnGold(Vector3 position, int totalAmount)
    {
        Vector3 spawnPos = position + Vector3.up * spawnHeightOffset;

        int orbCount = Mathf.Max(1, totalAmount / goldPerOrb);
        SpawnMultipleLoot(spawnPos, MagnetLoot.LootType.Gold, goldPerOrb, orbCount);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 경험치 오브 스폰
    /// </summary>
    private void SpawnExperience(Vector3 position, int totalExp)
    {
        // 총 경험치를 오브 개수로 나눠서 스폰
        int perOrb = Mathf.Max(1, totalExp / expOrbCount);
        int remainder = totalExp % expOrbCount;

        for (int i = 0; i < expOrbCount; i++)
        {
            int amount = perOrb;

            // 나머지는 마지막 오브에 추가
            if (i == expOrbCount - 1)
            {
                amount += remainder;
            }

            SpawnSingleLoot(position, MagnetLoot.LootType.Experience, amount);
        }
    }

    /// <summary>
    /// 여러 개의 루트 오브 스폰
    /// </summary>
    private void SpawnMultipleLoot(Vector3 position, MagnetLoot.LootType type, int amountPerOrb, int orbCount)
    {
        for (int i = 0; i < orbCount; i++)
        {
            SpawnSingleLoot(position, type, amountPerOrb);
        }
    }

    /// <summary>
    /// 단일 루트 오브 스폰
    /// </summary>
    private void SpawnSingleLoot(Vector3 position, MagnetLoot.LootType type, int amount)
    {
        if (magnetLootPrefab == null)
        {
            Debug.LogError("[MagnetLootSpawner] magnetLootPrefab이 할당되지 않았습니다!");
            return;
        }

        GameObject lootObj = Instantiate(magnetLootPrefab, position, Quaternion.identity);
        MagnetLoot loot = lootObj.GetComponent<MagnetLoot>();

        if (loot != null)
        {
            loot.Initialize(type, amount, position);
        }
        else
        {
            Debug.LogError("[MagnetLootSpawner] MagnetLoot 컴포넌트가 없습니다!");
            Destroy(lootObj);
        }
    }

    /// <summary>
    /// 확률 판정
    /// </summary>
    /// <param name="chance">확률 (0~100)</param>
    /// <returns>성공 여부</returns>
    private bool RollChance(float chance)
    {
        return Random.Range(0f, 100f) <= chance;
    }
    #endregion
}
