using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 시스템 매니저
/// - 스킬 3개 + 소켓 3개 상품 생성
/// - 등급/강화 레벨 기반 가격 계산
/// - 구매 시 골드 차감 + 아이템 소환
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("상품 풀")]
    [SerializeField, Tooltip("스킬 드롭 테이블")]
    private SkillLootTable skillLootTable;

    [SerializeField, Tooltip("소켓 아이템 풀")]
    private SocketItemData[] socketPool;

    [Header("아이템 소환")]
    [SerializeField, Tooltip("드롭 스킬 프리팹")]
    private GameObject droppedSkillPrefab;

    [SerializeField, Tooltip("드롭 소켓 프리팹")]
    private GameObject droppedSocketPrefab;

    [SerializeField, Tooltip("아이템 소환 위치 (상점 앞)")]
    private Transform itemSpawnPoint;

    [Header("가격 설정")]
    [SerializeField, Tooltip("등급별 기본 가격")]
    private int priceCommon = 50;
    [SerializeField] private int priceRare = 100;
    [SerializeField] private int priceEpic = 180;
    [SerializeField] private int priceLegendary = 300;

    [SerializeField, Tooltip("강화 레벨당 가격 배율 증가분")]
    private float enhancePriceMultiplier = 0.5f;

    [SerializeField, Tooltip("가격 랜덤 편차 (±%)")]
    [Range(0f, 0.3f)]
    private float priceVariance = 0.15f;

    [Header("강화 레벨 설정")]
    [SerializeField, Tooltip("층당 강화 출현 확률 증가 (%)")]
    private float enhanceChancePerFloor = 5f;

    [SerializeField, Tooltip("기본 강화 출현 확률 (%)")]
    private float baseEnhanceChance = 10f;

    [SerializeField, Tooltip("최대 강화 레벨")]
    private int maxEnhanceLevel = 3;

    // === 런타임 ===
    private List<ShopItemData> currentShopItems = new List<ShopItemData>();
    private bool hasRerollRight = false;
    private bool hasRerolledThisVisit = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ============================================================
    // 상점 상품 생성
    // ============================================================

    /// <summary>
    /// 상점 상품 6개 생성 (스킬 3 + 소켓 3)
    /// </summary>
    /// <param name="currentFloor">현재 층 (강화 확률에 영향)</param>
    public void GenerateShopItems(int currentFloor)
    {
        currentShopItems.Clear();
        hasRerolledThisVisit = false;

        // 스킬 3개
        List<SkillData> usedSkills = new List<SkillData>();
        for (int i = 0; i < 3; i++)
        {
            ShopItemData item = GenerateSkillItem(currentFloor, usedSkills);
            currentShopItems.Add(item);
        }

        // 소켓 3개
        for (int i = 0; i < 3; i++)
        {
            ShopItemData item = GenerateSocketItem(currentFloor);
            currentShopItems.Add(item);
        }

        Debug.Log($"[ShopManager] 상점 상품 {currentShopItems.Count}개 생성 완료");
    }

    /// <summary>
    /// 스킬 상품 1개 생성
    /// </summary>
    private ShopItemData GenerateSkillItem(int floor, List<SkillData> usedSkills)
    {
        // 중복 없이 스킬 뽑기
        SkillData skill = null;
        int attempts = 0;
        do
        {
            skill = skillLootTable.GetRandomSkill();
            attempts++;
        }
        while (usedSkills.Contains(skill) && attempts < 20);

        usedSkills.Add(skill);

        // 강화 레벨 결정
        int enhanceLevel = RollEnhanceLevel(floor);

        // 가격 계산
        int price = CalculatePrice(skill.grade, enhanceLevel);

        return new ShopItemData
        {
            itemType = ShopItemType.Skill,
            skillData = skill,
            socketData = null,
            enhanceLevel = enhanceLevel,
            price = price,
            isSoldOut = false,
            displayName = skill.skillName,
            icon = skill.skillIcon,
            description = skill.description,
            quality = 0
        };
    }

    /// <summary>
    /// 소켓 상품 1개 생성
    /// </summary>
    private ShopItemData GenerateSocketItem(int floor)
    {
        if (socketPool == null || socketPool.Length == 0)
        {
            Debug.LogWarning("[ShopManager] 소켓 풀이 비어있습니다.");
            return null;
        }

        SocketItemData socket = socketPool[Random.Range(0, socketPool.Length)];

        // 강화 레벨 결정
        int enhanceLevel = RollEnhanceLevel(floor);

        // 품질 결정 (강화 레벨 영향)
        int quality = RollQuality(floor, enhanceLevel);

        // 가격 계산 (소켓은 품질 기반)
        int price = CalculateSocketPrice(quality, enhanceLevel);

        return new ShopItemData
        {
            itemType = ShopItemType.Socket,
            skillData = null,
            socketData = socket,
            enhanceLevel = enhanceLevel,
            price = price,
            isSoldOut = false,
            displayName = socket.itemName,
            icon = socket.icon,
            description = socket.descriptionTemplate,
            quality = quality
        };
    }

    // ============================================================
    // 가격 / 강화 / 품질 계산
    // ============================================================

    /// <summary>
    /// 스킬 가격 계산: 등급 기본값 × (1 + 강화 × 0.5) × 랜덤 편차
    /// </summary>
    private int CalculatePrice(SkillGrade grade, int enhanceLevel)
    {
        int basePrice = GetBasePrice(grade);
        float enhanceMultiplier = 1f + (enhanceLevel * enhancePriceMultiplier);
        float variance = Random.Range(1f - priceVariance, 1f + priceVariance);

        return Mathf.RoundToInt(basePrice * enhanceMultiplier * variance);
    }

    /// <summary>
    /// 소켓 가격 계산: 품질 기반
    /// </summary>
    private int CalculateSocketPrice(int quality, int enhanceLevel)
    {
        // 품질 높을수록 비쌈
        float basePrice = 80f + (quality * 2f);
        float enhanceMultiplier = 1f + (enhanceLevel * enhancePriceMultiplier);
        float variance = Random.Range(1f - priceVariance, 1f + priceVariance);

        return Mathf.RoundToInt(basePrice * enhanceMultiplier * variance);
    }

    /// <summary>
    /// 등급별 기본 가격
    /// </summary>
    private int GetBasePrice(SkillGrade grade)
    {
        switch (grade)
        {
            case SkillGrade.Common: return priceCommon;
            case SkillGrade.Rare: return priceRare;
            case SkillGrade.Epic: return priceEpic;
            case SkillGrade.Legendary: return priceLegendary;
            default: return priceCommon;
        }
    }

    /// <summary>
    /// 강화 레벨 굴림 — 층 높을수록 강화 확률 증가
    /// </summary>
    private int RollEnhanceLevel(int floor)
    {
        float chance = baseEnhanceChance + (floor * enhanceChancePerFloor);
        int level = 0;

        // 레벨별로 확률 굴림 (중첩)
        for (int i = 0; i < maxEnhanceLevel; i++)
        {
            if (Random.Range(0f, 100f) < chance)
            {
                level++;
                chance *= 0.5f; // 상위 레벨일수록 확률 절반
            }
            else
            {
                break;
            }
        }

        return level;
    }

    /// <summary>
    /// 소켓 품질 굴림 (10~100)
    /// </summary>
    private int RollQuality(int floor, int enhanceLevel)
    {
        int minQuality = Mathf.Min(10 + (floor * 2), 50);
        int maxQuality = Mathf.Min(30 + (floor * 5) + (enhanceLevel * 10), 100);

        return Random.Range(minQuality, maxQuality + 1);
    }

    // ============================================================
    // 구매 처리
    // ============================================================

    /// <summary>
    /// 상품 구매 — 골드 차감 + 아이템 소환
    /// </summary>
    /// <param name="slotIndex">상점 슬롯 인덱스 (0~5)</param>
    /// <returns>구매 성공 여부</returns>
    public bool PurchaseItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= currentShopItems.Count)
            return false;

        ShopItemData item = currentShopItems[slotIndex];

        if (item.isSoldOut)
        {
            Debug.Log("[ShopManager] 이미 품절된 상품입니다.");
            return false;
        }

        // 골드 확인
        if (PlayerStats.Instance == null || !PlayerStats.Instance.UseCoin(item.price))
        {
            Debug.Log("[ShopManager] 골드가 부족합니다.");
            return false;
        }

        // 아이템 소환
        SpawnPurchasedItem(item);

        // 품절 처리
        item.isSoldOut = true;

        Debug.Log($"[ShopManager] {item.displayName} 구매 완료! (-{item.price}골드)");
        return true;
    }

    /// <summary>
    /// 구매한 아이템을 땅에 소환
    /// </summary>
    private void SpawnPurchasedItem(ShopItemData item)
    {
        Transform player = FindFirstObjectByType<PlayerController>()?.transform;

        Vector3 spawnPos = player != null
            ? player.position + player.forward * 2f
            : transform.position + Vector3.forward * 2f;

        spawnPos += new Vector3(Random.Range(-0.5f, 0.5f), 1f, Random.Range(-0.5f, 0.5f));

        if (item.itemType == ShopItemType.Skill)
        {
            SpawnSkillItem(item, spawnPos);
        }
        else if (item.itemType == ShopItemType.Socket)
        {
            SpawnSocketItem(item, spawnPos);
        }
    }

    /// <summary>
    /// 스킬 아이템 소환
    /// </summary>
    private void SpawnSkillItem(ShopItemData item, Vector3 pos)
    {
        if (droppedSkillPrefab == null)
        {
            Debug.LogError("[ShopManager] droppedSkillPrefab이 없습니다.");
            return;
        }

        GameObject obj = Instantiate(droppedSkillPrefab, pos, Quaternion.identity);
        DroppedSkillItem dropped = obj.GetComponent<DroppedSkillItem>();

        if (dropped != null)
        {
            // 강화 레벨 + 1 = 스킬 레벨
            dropped.Initialize(item.skillData, item.enhanceLevel + 1);
        }
    }

    /// <summary>
    /// 소켓 아이템 소환
    /// </summary>
    private void SpawnSocketItem(ShopItemData item, Vector3 pos)
    {
        if (droppedSocketPrefab == null)
        {
            Debug.LogWarning("[ShopManager] droppedSocketPrefab이 없습니다. (소켓 시스템 미구현)");
            return;
        }

        // TODO: DroppedSocketItem 구현 후 연결
        GameObject obj = Instantiate(droppedSocketPrefab, pos, Quaternion.identity);
        Debug.Log($"[ShopManager] 소켓 '{item.displayName}' 소환");
    }

    // ============================================================
    // 리롤
    // ============================================================

    /// <summary>
    /// 상점 리롤 — 6개 상품 전부 새로 굴림
    /// </summary>
    public bool TryReroll(int currentFloor)
    {
        if (!hasRerollRight)
        {
            Debug.Log("[ShopManager] 리롤권이 없습니다.");
            return false;
        }

        if (hasRerolledThisVisit)
        {
            Debug.Log("[ShopManager] 이번 방문에서 이미 리롤했습니다.");
            return false;
        }

        hasRerolledThisVisit = true;
        GenerateShopItems(currentFloor);

        Debug.Log("[ShopManager] 상점 리롤 완료!");
        return true;
    }

    /// <summary>
    /// 리롤권 활성화 (영구 강화에서 구매 시)
    /// </summary>
    public void UnlockReroll()
    {
        hasRerollRight = true;
    }

    // ============================================================
    // 외부 접근
    // ============================================================

    /// <summary>현재 상점 상품 목록</summary>
    public List<ShopItemData> GetShopItems() => currentShopItems;

    /// <summary>리롤 가능 여부</summary>
    public bool CanReroll() => hasRerollRight && !hasRerolledThisVisit;
}
