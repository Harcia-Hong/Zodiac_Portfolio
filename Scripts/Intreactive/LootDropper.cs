using UnityEngine;

public class LootDropper : MonoBehaviour
{
    [Header("===== 프리팹 =====")]
    [SerializeField] private GameObject materialPrefab;

    [Header("===== 재료 설정 =====")]
    [Range(0f, 100f)]
    [SerializeField] private float materialDropChance = 30f;
    [SerializeField] private int materialAmount = 1;

    [Header("===== 퍼짐 설정 =====")]
    [SerializeField] private float spreadRadius = 1.5f;
    [SerializeField] private float dropHeight = 0.5f;

    /// <summary>
    /// 전리품 드롭 실행 (외부에서 호출)
    /// - 골드는 상점 판매로만 획득하므로 드랍하지 않음
    /// - 별가루(Material)만 확률적으로 드랍
    /// </summary>
    public void DropLoot()
    {
        TryDropMaterial();
    }

    /// <summary>
    /// 별가루 드롭 시도 (확률적)
    /// </summary>
    private void TryDropMaterial()
    {
        if (materialPrefab == null) return;

        float roll = Random.Range(0f, 100f);
        if (roll <= materialDropChance)
        {
            SpawnLoot(materialPrefab, LootItem.LootType.Material, materialAmount);
        }
    }

    /// <summary>
    /// 전리품 스폰 (사방으로 퍼뜨리기)
    /// </summary>
    private void SpawnLoot(GameObject prefab, LootItem.LootType type, int amount)
    {
        Vector3 spawnPos = transform.position + Vector3.up * dropHeight;

        Vector2 randomCircle = Random.insideUnitCircle * spreadRadius;
        Vector3 targetPos = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
            
        GameObject lootObj = Instantiate(prefab, spawnPos, Quaternion.identity);

        LootItem lootItem = lootObj.GetComponent<LootItem>();
        if (lootItem != null)
        {
            lootItem.Initialize(targetPos, type, amount);
        }
    }
}