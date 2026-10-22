using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보상 오브젝트 스폰 관리
/// - 기존: Epic 몬스터 사망 시 보상 스폰
/// - 추가: 층 클리어 시 확률 기반 보상/재앙 오브젝트 스폰
/// 
/// 층 클리어 스폰 규칙:
///   포탈 — 무조건 (StageManager에서 별도 처리)
///   보상 오브젝트 — 서판/힘의조각/잉크병 중 1개 (확률, 안 나올 수도 있음)
///   재앙 오브젝트 — 금서의 잔재 (확률, 보상과 독립적으로 등장 가능)
/// </summary>
public class RewardObjectSpawner : MonoBehaviour
{
    #region Singleton
    public static RewardObjectSpawner Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    #endregion

    #region Serialized Fields — 기존 (Epic 몬스터용)
    [Header("=== Epic 몬스터 사망 시 ===")]
    [SerializeField] private GameObject statBoostObjectPrefab;
    [SerializeField] private GameObject skillDropObjectPrefab;

    [Header("Epic 스폰 확률")]
    [Range(0f, 100f)]
    [SerializeField] private float statBoostChance = 50f;
    #endregion

    #region Serialized Fields — 층 클리어용
    [Header("=== 층 클리어 시 보상 오브젝트 ===")]
    [SerializeField] private GameObject statBoostClearPrefab;
    [SerializeField] private GameObject skillDropClearPrefab;
    [SerializeField] private GameObject skillUpgradeClearPrefab;

    [Header("층 클리어 보상 출현 확률 (%)")]
    [Tooltip("보상 오브젝트가 나올 전체 확률")]
    [Range(0f, 100f)]
    [SerializeField] private float clearRewardChance = 45f;

    [Header("보상 종류별 가중치 (보상이 나올 때 어떤 종류인지)")]
    [Tooltip("흩어진 서판 (스탯 강화)")]
    [SerializeField] private float statBoostWeight = 40f;

    [Tooltip("힘의 조각 (스킬 획득)")]
    [SerializeField] private float skillDropWeight = 30f;

    [Tooltip("기억의 잉크병 (스킬 강화)")]
    [SerializeField] private float skillUpgradeWeight = 30f;

    [Header("=== 층 클리어 시 재앙 오브젝트 ===")]
    [SerializeField] private GameObject cursedTomePrefab;

    [Tooltip("금서의 잔재 출현 확률 (%)")]
    [Range(0f, 100f)]
    [SerializeField] private float cursedTomeChance = 20f;

    [Header("스폰 설정")]
    [SerializeField] private float spawnHeightOffset = 1.5f;
    [SerializeField] private float fadeInDuration = 1f;
    [SerializeField] private float spawnSpreadDistance = 3f;
    #endregion

    // Fade 쉐이더 프로퍼티
    private static readonly int FadeAmountID = Shader.PropertyToID("_FadeAmount");

    #region 기존 — Epic 몬스터 사망 시 스폰
    /// <summary>
    /// Epic 몬스터 사망 시 호출 (기존 로직 유지)
    /// </summary>
    public void SpawnRewardObject(Vector3 spawnPosition)
    {
        StartCoroutine(SpawnSingleRoutine(spawnPosition, DecideEpicReward()));
    }

    /// <summary>
    /// Epic용 프리팹 결정
    /// </summary>
    private GameObject DecideEpicReward()
    {
        float roll = Random.Range(0f, 100f);
        bool spawnStatBoost = roll < statBoostChance;

        if (spawnStatBoost && statBoostObjectPrefab != null)
            return statBoostObjectPrefab;

        if (!spawnStatBoost && skillDropObjectPrefab != null)
            return skillDropObjectPrefab;

        return statBoostObjectPrefab != null ? statBoostObjectPrefab : skillDropObjectPrefab;
    }
    #endregion

    #region 층 클리어 시 스폰
    /// <summary>
    /// 층 클리어 시 호출 — 보상 + 재앙 오브젝트 확률 스폰
    /// StageManager.HandleStageCleared()에서 호출
    /// </summary>
    public void SpawnClearRewards(Vector3 centerPosition)
    {
        StartCoroutine(SpawnClearRewardsRoutine(centerPosition));
    }

    /// <summary>
    /// 클리어 보상 스폰 루틴
    /// 보상 오브젝트와 재앙 오브젝트는 독립적으로 판정
    /// </summary>
    private IEnumerator SpawnClearRewardsRoutine(Vector3 center)
    {
        List<GameObject> toSpawn = new List<GameObject>();

        // 1. 보상 오브젝트 판정 (서판/힘의조각/잉크병 중 1개 or 없음)
        GameObject rewardPrefab = RollClearReward();
        if (rewardPrefab != null)
        {
            toSpawn.Add(rewardPrefab);
        }

        // 2. 재앙 오브젝트 판정 (보상과 독립)
        if (cursedTomePrefab != null && Random.Range(0f, 100f) < cursedTomeChance)
        {
            toSpawn.Add(cursedTomePrefab);
        }

        // 3. 결과 로그
        if (toSpawn.Count == 0)
        {
            Debug.Log("[RewardObjectSpawner] 클리어 보상: 없음");
            yield break;
        }

        // 4. 위치 계산 — 오브젝트가 여러 개면 좌우로 벌려서 배치
        List<Vector3> positions = CalculateSpawnPositions(center, toSpawn.Count);

        // 5. 순차적으로 스폰 (연출)
        for (int i = 0; i < toSpawn.Count; i++)
        {
            yield return StartCoroutine(SpawnSingleRoutine(positions[i], toSpawn[i]));
            yield return new WaitForSeconds(0.3f);
        }

        Debug.Log($"[RewardObjectSpawner] 클리어 보상 {toSpawn.Count}개 스폰 완료");
    }

    /// <summary>
    /// 보상 종류 판정 — 전체 확률 통과 후 가중치로 종류 결정
    /// </summary>
    private GameObject RollClearReward()
    {
        // 보상 자체가 나올지 판정
        if (Random.Range(0f, 100f) >= clearRewardChance)
            return null;

        // 가중치 기반 종류 결정
        float totalWeight = statBoostWeight + skillDropWeight + skillUpgradeWeight;
        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        cumulative += statBoostWeight;
        if (roll < cumulative && statBoostClearPrefab != null)
            return statBoostClearPrefab;

        cumulative += skillDropWeight;
        if (roll < cumulative && skillDropClearPrefab != null)
            return skillDropClearPrefab;

        if (skillUpgradeClearPrefab != null)
            return skillUpgradeClearPrefab;

        return null;
    }

    /// <summary>
    /// 스폰 위치 계산 — 여러 개일 때 좌우로 벌려서 배치
    /// </summary>
    private List<Vector3> CalculateSpawnPositions(Vector3 center, int count)
    {
        List<Vector3> positions = new List<Vector3>();

        if (count == 1)
        {
            positions.Add(center);
        }
        else
        {
            // 중심 기준 좌우로 벌리기
            float startOffset = -(count - 1) * spawnSpreadDistance * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Vector3 offset = Vector3.right * (startOffset + i * spawnSpreadDistance);
                positions.Add(center + offset);
            }
        }

        return positions;
    }
    #endregion

    #region 공통 — 스폰 연출
    /// <summary>
    /// 단일 오브젝트 스폰 + Fade 연출
    /// </summary>
    private IEnumerator SpawnSingleRoutine(Vector3 position, GameObject prefab)
    {
        if (prefab == null) yield break;

        Vector3 spawnPos = position + Vector3.up * spawnHeightOffset;
        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);

        yield return StartCoroutine(FadeInRoutine(obj));

        Debug.Log($"[RewardObjectSpawner] 스폰: {prefab.name}");
    }

    /// <summary>
    /// Fade In 연출 (FadeAmount 1→0)
    /// </summary>
    private IEnumerator FadeInRoutine(GameObject obj)
    {
        if (obj == null) yield break;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        float elapsed = 0f;
        SetFadeAmount(renderers, propertyBlock, 1f);

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeInDuration;
            float fadeValue = Mathf.Lerp(1f, 0f, t);
            SetFadeAmount(renderers, propertyBlock, fadeValue);
            yield return null;
        }

        SetFadeAmount(renderers, propertyBlock, 0f);
    }

    /// <summary>
    /// FadeAmount 값 설정
    /// </summary>
    private void SetFadeAmount(Renderer[] renderers, MaterialPropertyBlock propertyBlock, float value)
    {
        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            rend.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(FadeAmountID, value);
            rend.SetPropertyBlock(propertyBlock);
        }
    }
    #endregion
}
