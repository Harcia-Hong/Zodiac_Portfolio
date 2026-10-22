using UnityEngine;
using UnityEngine.VFX;
using System.Collections.Generic; // Dictionary 사용을 위해 추가

/// <summary>
/// Enemy 피격 이펙트 관리 클래스
/// HitEffectType에 따라 다른 피격 이펙트를 재생
/// (Dictionary 기반으로 변경되어 확장 용이)
/// </summary>
public class EnemyHitEffectManager : MonoBehaviour
{
    // HitEffectType과 이펙트 프리팹을 연결하는 클래스 (인스펙터 노출용)
    [System.Serializable]
    public class HitEffectPair
    {
        public HitEffectType effectType;
        public GameObject effectPrefab;
        // public VisualEffect effectVFX; // 필요시 VFX도 함께 관리
    }

    [Header("피격 이펙트 목록")]
    [Tooltip("여기에 모든 피격 이펙트를 등록하세요.")]
    [SerializeField] private List<HitEffectPair> hitEffectList;

    // 실제 사용될 딕셔너리 (빠른 탐색용)
    private Dictionary<HitEffectType, GameObject> hitEffectDictionary = new Dictionary<HitEffectType, GameObject>();

    [Header("Effect Settings")]
    [Tooltip("Prefab 자동 삭제 시간 (ParticleSystem이 없을 경우)")]
    public float defaultDestroyTime = 3f;

    /// <summary>
    /// Awake에서 List를 Dictionary로 변환 (성능 최적화)
    /// </summary>
    private void Awake()
    {
        // 인스펙터에서 설정한 리스트를 딕셔너리로 변환
        foreach (var pair in hitEffectList)
        {
            if (pair.effectPrefab == null) continue;

            if (!hitEffectDictionary.ContainsKey(pair.effectType))
            {
                hitEffectDictionary.Add(pair.effectType, pair.effectPrefab);
            }
            else
            {
                Debug.LogWarning($"[EnemyHitEffectManager] {pair.effectType}이(가) 이미 딕셔너리에 등록되어 있습니다.");
            }
        }
    }

    /// <summary>피격 이펙트 재생 (수정됨: HitEffectType 기반)</summary>
    /// <param name="effectType">피격 효과 타입</param>
    /// <param name="hitPosition">피격 위치</param>
    /// <param name="hitNormal">피격 표면의 법선 벡터 (이펙트 방향 조정용, 옵션)</param>
    public void PlayHitEffect(HitEffectType effectType, Vector3 hitPosition, Vector3 hitNormal = default)
    {
        // 딕셔너리에서 effectType에 맞는 프리팹을 찾습니다.
        if (hitEffectDictionary.TryGetValue(effectType, out GameObject prefabToSpawn))
        {
            // 찾았으면 해당 프리팹을 생성합니다.
            SpawnPrefabEffect(prefabToSpawn, hitPosition, hitNormal, effectType.ToString());
        }
        else
        {
            // 딕셔너리에 등록되지 않은 이펙트일 경우 경고
            Debug.LogWarning($"[EnemyHitEffectManager] {effectType}에 해당하는 피격 이펙트가 등록되지 않았습니다.");
        }
    }

    /// <summary>Prefab 이펙트 생성 및 자동 삭제</summary>
    private void SpawnPrefabEffect(GameObject prefab, Vector3 position, Vector3 normal, string effectName)
    {
        // 이펙트 회전 계산 (법선 벡터가 제공된 경우)
        Quaternion rotation = Quaternion.identity;
        if (normal != Vector3.zero)
        {
            rotation = Quaternion.LookRotation(normal);
        }

        // Prefab 인스턴스화
        GameObject effectInstance = Instantiate(prefab, position, rotation);

        Debug.Log($"[EnemyHitEffectManager] {effectName} Prefab 생성 at {position}");

        // 파티클 시스템 자동 삭제 처리
        DestroyEffectAfterDuration(effectInstance);
    }

    /// <summary>이펙트 오브젝트 자동 삭제 처리</summary>
    private void DestroyEffectAfterDuration(GameObject effectInstance)
    {
        ParticleSystem particleSystem = effectInstance.GetComponent<ParticleSystem>();

        if (particleSystem != null)
        {
            // 파티클 시스템의 duration + lifetime에 맞춰 자동 삭제
            float destroyTime = particleSystem.main.duration + particleSystem.main.startLifetime.constantMax;
            Destroy(effectInstance, destroyTime);
        }
        else
        {
            // 파티클 시스템이 없으면 기본 시간 후 삭제
            Destroy(effectInstance, defaultDestroyTime);
            Debug.LogWarning("[EnemyHitEffectManager] Prefab에 ParticleSystem이 없습니다. 기본 시간 후 자동 삭제됩니다.");
        }
    }
}
