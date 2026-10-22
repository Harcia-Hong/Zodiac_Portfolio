using UnityEngine;

/// ============================================================
/// StageData.cs — 아케이드 웨이브 스테이지 데이터 (모듈 토글 적용)
/// ============================================================
/// 킬 나이트 + 윈드브로운 혼합 페이즈 기반 전투 구조.
///
/// 데이터 계층:
///   Stage (= 챕터 1개)
///     └ Phase[]  (= 하나의 Layout에서의 전투 단위, 다음 Layout 활성화 단위)
///         └ Wave[] (= 한 Phase 내부의 반복 전투. 사이사이 모듈 토글 발생)
///
/// 흐름 (모듈 토글 적용):
///   페이즈 시작 → 웨이브 0 → 클리어 → (모듈 토글: 활성 모듈 ID 셋 변경 + 밀어내기)
///   → 웨이브 1 → 클리어 → (모듈 토글)
///   → ... → 마지막 웨이브 클리어
///   → 페이즈 클리어 → 다음 Layout 추가 활성화 (누적)
///   → 플레이어 진입 → 다음 페이즈의 첫 웨이브 시작
///   → ... 마지막 페이즈 클리어 → 보스 Layout → 보스 처치 = 클리어
///
/// 하위 호환성:
///   PhaseData.subWaves가 비어 있으면 PhaseData 자체를 단일 웨이브로
///   변환하여 사용. 기존 SO 에셋은 수정 없이 그대로 작동함.
/// ============================================================

/// <summary>아케이드 웨이브 스테이지 데이터 (ScriptableObject)</summary>
[CreateAssetMenu(fileName = "StageData", menuName = "Scriptable Objects/StageData")]
public class StageData : ScriptableObject
{
    [Header("스테이지 기본 정보")]
    public string stageName = "챕터 1";
    public int chapterNumber = 1;

    [TextArea(2, 4)]
    public string stageDescription = "첫 번째 아레나입니다.";

    [Header("페이즈 구성 (순차 진행)")]
    [Tooltip("각 페이즈의 전투 데이터. 0번부터 순서대로 진행됨")]
    public PhaseData[] phases;

    [Header("보스 설정")]
    [Tooltip("모든 페이즈 클리어 후 등장하는 보스")]
    public BossSettings bossSettings;

    [Header("클리어 보상")]
    [Tooltip("보스 처치 후 지급되는 골드")]
    public int clearGold = 100;

    [Tooltip("보스 처치 후 지급되는 별가루")]
    public int clearStardust = 10;

    public int PhaseCount => phases != null ? phases.Length : 0;

    public PhaseData GetPhase(int index)
    {
        if (phases == null || index < 0 || index >= phases.Length)
            return null;

        return phases[index];
    }
}

// ============================================================
// 페이즈 데이터
// ============================================================

/// <summary>
/// 개별 페이즈 전투 데이터
/// - 한 페이즈 = 한 Layout에서의 전투 묶음
/// - 페이즈 안에는 여러 웨이브가 들어갈 수 있음 (subWaves)
/// </summary>
[System.Serializable]
public class PhaseData
{
    [Header("페이즈 식별")]
    [Tooltip("페이즈 이름 (디버깅용)")]
    public string phaseName = "페이즈 1";

    [Header("웨이브 구성 (다중)")]
    [Tooltip("이 페이즈 안에서 순차로 진행될 웨이브 배열.\n" +
             "비어 있으면 아래 '레거시 단일 웨이브' 필드를 사용 (호환)")]
    public WaveData[] subWaves;

    // ============================================================
    // 레거시 단일 웨이브 필드
    // ============================================================

    [Header("[레거시] 단일 웨이브 — subWaves가 비어 있을 때만 사용")]
    public int totalNormalCount = 30;
    public int maxAliveCount = 15;

    [Header("[레거시] 스폰 간격")]
    public float spawnInterval = 1.0f;
    public int spawnBatchSize = 3;

    [Header("[레거시] 일반 몬스터")]
    public GameObject[] normalEnemyPrefabs;

    [Header("[레거시] 엘리트 출현")]
    [Range(0f, 1f)]
    public float eliteTriggerRatio = 0.7f;
    public int eliteCount = 1;
    public GameObject[] eliteEnemyPrefabs;

    // ============================================================
    // 지형 활성화 (페이즈 단위 — 누적형)
    // ============================================================

    [Header("지형 활성화 (페이즈 클리어 시)")]
    [Tooltip("이 페이즈 클리어 후 '추가로 활성화'할 Layout 인덱스")]
    public int layoutIndexOnClear = 0;

    [Tooltip("Layout 솟아오름 연출 시간 (초)")]
    public float transitionDuration = 1.5f;

    // ============================================================
    // 보상 게이트 (페이즈 클리어 후 활성화)
    // ============================================================

    [Header("보상 게이트 (페이즈 클리어 후 등장)")]
    [Tooltip("이 페이즈 클리어 후 활성화될 보상 게이트들.\n" +
             "각 게이트는 씬에 미리 배치된 위치(spawnAnchor)에서 보상을 스폰함.\n" +
             "비어있으면 보상 없이 다음 페이즈로 진행")]
    public RewardGateData[] rewardGates;

    [Header("환경 효과")]
    public HazardType hazardType = HazardType.None;

    [Header("보상 오브젝트 (레거시 — 추후 재활용)")]
    public GameObject[] rewardObjectPrefabs;
    public int rewardObjectCount = 3;

    // ============================================================
    // 웨이브 접근 API
    // ============================================================

    public int WaveCount =>
        (subWaves != null && subWaves.Length > 0) ? subWaves.Length : 1;

    public bool IsLegacyWaveMode =>
        subWaves == null || subWaves.Length == 0;

    public WaveData GetWave(int index)
    {
        if (!IsLegacyWaveMode)
        {
            if (index < 0 || index >= subWaves.Length)
                return null;

            return subWaves[index];
        }

        if (index == 0)
            return BuildLegacyWaveFromSelf();

        return null;
    }

    /// <summary>레거시 PhaseData 필드를 임시 WaveData로 변환</summary>
    private WaveData BuildLegacyWaveFromSelf()
    {
        return new WaveData
        {
            waveName = phaseName + " (단일)",
            totalNormalCount = totalNormalCount,
            maxAliveCount = maxAliveCount,
            spawnInterval = spawnInterval,
            spawnBatchSize = spawnBatchSize,
            normalEnemyPrefabs = normalEnemyPrefabs,
            eliteTriggerRatio = eliteTriggerRatio,
            eliteCount = eliteCount,
            eliteEnemyPrefabs = eliteEnemyPrefabs,
            postWaveDelay = 0f,
            useModuleConfiguration = false,    // 레거시는 모듈 토글 안 함
            activeModuleIds = null,
        };
    }

    public bool ShouldTriggerElite(int killedCount)
    {
        if (totalNormalCount <= 0) return false;
        float ratio = (float)killedCount / totalNormalCount;
        return ratio >= eliteTriggerRatio;
    }

    public GameObject GetRandomNormalPrefab()
    {
        if (normalEnemyPrefabs == null || normalEnemyPrefabs.Length == 0)
            return null;
        return normalEnemyPrefabs[Random.Range(0, normalEnemyPrefabs.Length)];
    }

    public GameObject GetRandomElitePrefab()
    {
        if (eliteEnemyPrefabs == null || eliteEnemyPrefabs.Length == 0)
            return null;
        return eliteEnemyPrefabs[Random.Range(0, eliteEnemyPrefabs.Length)];
    }
}

// ============================================================
// 웨이브 데이터 (페이즈 내 단일 전투 단위)
// ============================================================

/// <summary>
/// 한 페이즈 안의 단일 웨이브 전투 데이터
/// - 몬스터 구성 + 스폰 룰
/// - 종료 후 모듈 토글 (활성 모듈 ID 셋 명시)
/// </summary>
[System.Serializable]
public class WaveData
{
    [Header("웨이브 식별")]
    [Tooltip("웨이브 이름 (디버깅용)")]
    public string waveName = "웨이브 1";

    [Header("몬스터 총 몰량")]
    public int totalNormalCount = 15;
    public int maxAliveCount = 8;

    [Header("스폰 간격")]
    public float spawnInterval = 1.0f;
    public int spawnBatchSize = 3;

    [Header("일반 몬스터")]
    public GameObject[] normalEnemyPrefabs;

    [Header("엘리트 출현")]
    [Range(0f, 1f)]
    public float eliteTriggerRatio = 0.7f;
    public int eliteCount = 0;
    public GameObject[] eliteEnemyPrefabs;

    // ============================================================
    // 웨이브 종료 후 — 모듈 토글
    // ============================================================

    [Header("웨이브 종료 후 — 호흡 시간")]
    [Tooltip("모듈 토글 + 추가 호흡 시간 (초). 다음 웨이브 시작 전 잠깐의 정적")]
    public float postWaveDelay = 1.5f;

    [Header("모듈 토글 설정")]
    [Tooltip("이 웨이브 클리어 후 모듈 토글을 수행할지.\n" +
             "false면 토글 자체를 스킵 (이전 웨이브의 모듈 구성 그대로 유지).\n" +
             "첫 웨이브에서는 false로 두고, 이후 웨이브에서 변화를 주는 패턴 권장")]
    public bool useModuleConfiguration = false;

    [Tooltip("이 웨이브 클리어 후 활성 상태로 만들 모듈 ID 배열.\n" +
             "예: [0, 1] → 모듈 0과 1만 활성, 나머지 일반 모듈은 비활성.\n" +
             "빈 배열 → 일반 모듈 모두 비활성 (Core만 남음).\n" +
             "Core 모듈(ID = -1)은 항상 활성이므로 명시 불필요.\n" +
             "useModuleConfiguration이 false면 무시됨")]
    public int[] activeModuleIds;

    // ============================================================
    // 레거시 (deprecated — 이전 BlockTransformManager용 필드)
    // ============================================================

    [Header("[레거시] 블록 변형 패턴 ID — 더 이상 사용 안 함")]
    [System.Obsolete("BlockTransformManager 폐기됨. 모듈 토글로 대체. " +
                     "useModuleConfiguration + activeModuleIds 사용")]
    [Tooltip("[폐기됨] 이전 BlockTransformManager 시절의 패턴 ID. 코드에서 더 이상 읽지 않음")]
    public string transformPatternId = string.Empty;

    // ============================================================
    // 헬퍼
    // ============================================================

    public bool ShouldTriggerElite(int killedCount)
    {
        if (totalNormalCount <= 0) return false;
        if (eliteCount <= 0) return false;
        float ratio = (float)killedCount / totalNormalCount;
        return ratio >= eliteTriggerRatio;
    }

    public GameObject GetRandomNormalPrefab()
    {
        if (normalEnemyPrefabs == null || normalEnemyPrefabs.Length == 0)
            return null;
        return normalEnemyPrefabs[Random.Range(0, normalEnemyPrefabs.Length)];
    }

    public GameObject GetRandomElitePrefab()
    {
        if (eliteEnemyPrefabs == null || eliteEnemyPrefabs.Length == 0)
            return null;
        return eliteEnemyPrefabs[Random.Range(0, eliteEnemyPrefabs.Length)];
    }
}

// ============================================================
// 환경 해저드
// ============================================================

public enum HazardType
{
    None,
    Lava,
    Spike,
    Poison,
    Ice
}

// ============================================================
// 보스 설정
// ============================================================

[System.Serializable]
public class BossSettings
{
    [Tooltip("보스 프리팹 (Boss 컴포넌트 필수)")]
    public GameObject bossPrefab;

    [Tooltip("보스 스폰 위치 오프셋 (아레나 중심 기준)")]
    public Vector3 spawnOffset = Vector3.zero;

    [Tooltip("보스 스폰 위치 — 이 Transform이 설정되면 spawnOffset 무시하고 여기에 스폰.\n" +
         "씬의 Layout 안에 빈 GameObject로 만들어서 드래그.\n" +
         "비어있으면 ArenaCenter + spawnOffset 사용")]
    public Transform bossSpawnPoint;

    [Tooltip("보스 등장 시 남아있는 일반 몬스터 제거 여부")]
    public bool clearMobsOnBossSpawn = true;

    [Tooltip("보스 전용 아레나 Layout 사용 여부")]
    public bool useCustomBossLayout = true;

    [Tooltip("보스 전용 Layout 인덱스. -1이면 전용 Layout 없이 즉시 소환")]
    public int bossArenaLayoutIndex = -1;
}

// ============================================================
// 보상 게이트 데이터
// ============================================================

/// <summary>
/// 한 보상 게이트의 명세
/// - 페이즈 클리어 후, 디자이너가 미리 배치한 위치(spawnAnchor)에서
///   풀(rewardPool) 안에서 rewardCount개 만큼 랜덤 + 중복 방지로 스폰
/// - 저주(CursedTome)는 cursedChance 확률로 별도 슬롯에 등장
/// - 게이트의 보상 1개를 인터랙트하면 그룹의 나머지 일반 보상은 부서짐
/// - 저주는 그룹 외부 — 부서지지 않고 남음
///
/// 셋업 흐름:
///   1. 씬에 빈 GameObject 생성 (예: RewardGate_Phase0)
///   2. 디자이너가 위치를 두 Layout 사이의 길에 배치
///   3. PhaseData의 rewardGates 배열에 이 데이터를 추가하고
///      spawnAnchor에 위 GameObject의 Transform 드래그
/// </summary>
[System.Serializable]
public class RewardGateData
{
    [Header("게이트 식별")]
    [Tooltip("게이트 이름 (디버깅용)")]
    public string gateName = "Gate";

    [Header("스폰 위치")]
    [Tooltip("씬에 미리 배치한 위치 GameObject의 Transform.\n" +
             "여기를 중심으로 보상이 펼쳐져 스폰됨")]
    public Transform spawnAnchor;

    [Header("보상 풀")]
    [Tooltip("이 게이트에서 스폰될 수 있는 보상 종류 후보.\n" +
             "예: [StatBoost, SkillDrop, SkillUpgrade] → 3개 중 rewardCount만큼 랜덤 선택.\n" +
             "CursedTome은 여기 포함하지 말 것 — 별도 슬롯으로 처리됨")]
    public ClearRewardType[] rewardPool;

    [Tooltip("rewardPool에서 실제로 스폰할 보상 개수 (중복 방지로 풀에서 추출).\n" +
             "rewardPool 길이보다 크면 풀 전체를 사용")]
    [Min(0)]
    public int rewardCount = 1;

    [Header("저주 (CursedTome)")]
    [Tooltip("저주 오브젝트가 등장할 확률 (0~100).\n" +
             "0이면 절대 안 나옴. 100이면 항상 등장.\n" +
             "저주는 보상 그룹 외부 — 보상 1개 선택 시에도 부서지지 않음")]
    [Range(0f, 100f)]
    public float cursedChance = 0f;
}


// ============================================================
// 스테이지 타입 (노드맵용)
// ============================================================

public enum StageType
{
    Combat,
    Shop,
    Rest,
    Event,
    Boss
}
