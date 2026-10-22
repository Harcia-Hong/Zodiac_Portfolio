using UnityEngine;

/// <summary>
/// 보스 액션 스텝 타입
/// - 각 스텝이 어떤 행동인지 정의
/// - 패턴은 이 타입들의 조합으로 구성됨
/// </summary>
public enum BossActionType
{
    Prepare,    // 준비자세 (칼 뽑기 등 예비 동작)
    Teleport,   // 순간이동 (플레이어 근처로 이동)
    Move,       // 이동 (NavMesh/Lerp로 걸어서 이동)
    Telegraph,  // 텔레그래프 (공격 범위 표시, 시간 지나면 사라짐)
    Attack,     // 공격 (실제 데미지 판정 + 공격 모션 + VFX)
    Recovery    // 후딜 (칼 집어넣기, 준비자세 복귀 등)
}

/// <summary>
/// 텔레그래프 형태 타입
/// - 기존 AttackTelegraph.TelegraphType과 대응
/// </summary>
public enum BossTelegraphShape
{
    Circle,     // 원형 (내려찍기, 범위 공격)
    Cone,       // 부채꼴 (베기, 휘두르기)
    Line        // 직선 (돌진, 관통 공격)
}

/// <summary>
/// 텔레그래프 위치 기준
/// - 텔레그래프가 어디를 중심으로 생성되는지
/// </summary>
public enum TelegraphOrigin
{
    Boss,       // 보스 위치 기준
    Player,     // 플레이어 위치 기준 (순간이동 후 내려찍기 등)
    Custom      // 커스텀 위치 (특수 패턴용)
}

/// <summary>
/// 순간이동 목적지 타입
/// </summary>
public enum TeleportTarget
{
    NearPlayer,         // 플레이어 근처 (오프셋 거리만큼 떨어진 위치)
    OnPlayer,           // 플레이어 정확한 위치 (내려찍기용)
    RandomAroundPlayer, // 플레이어 주변 랜덤 위치
    OnTelegraph         // 텔레그래프 표시 위치로 이동 (깜짝베기용)
}

/// <summary>
/// 스텝 실행 조건 모드
/// - 언제 이 스텝이 실행될지 결정하는 방식
/// </summary>
public enum StepTriggerMode
{
    /// <summary>startTime 기준 (기존 방식). 패턴 시작 후 N초 뒤 실행</summary>
    StartByTime,

    /// <summary>텔레그래프 진행도 기준. 텔레그래프가 N% 채워지면 실행</summary>
    StartByTelegraphProgress
}

/// <summary>
/// 보스 액션 스텝 데이터
/// - 패턴을 구성하는 개별 행동 1개의 설정
/// - startTime 또는 triggerProgress로 실행 시점 결정
/// - 타입에 따라 필요한 필드만 사용 (나머지는 무시됨)
/// 
/// 예시 (내려찍기 패턴 - 진행도 트리거 활용):
///   [0] Time    0.0   Prepare   - 준비자세
///   [1] Time    0.5   Telegraph - 플레이어 위치에 원형 Fill
///   [2] Progress 0.75 Teleport  - Fill 75%에 보스 사라짐
///   [3] Progress 1.0  Attack    - Fill 100%에 텔레그래프 위치에서 내려찍기
///   [4] Time    자동  Recovery  - 공격 후 후딜
/// </summary>
[System.Serializable]
public class BossActionStep
{
    [Header("실행 조건")]
    [Tooltip("이 스텝이 언제 실행되는지 결정하는 모드")]
    public StepTriggerMode triggerMode = StepTriggerMode.StartByTime;

    [Header("시간 기반 트리거 (StartByTime)")]
    [Tooltip("패턴 시작 후 이 스텝이 실행되는 시간 (초). 같은 시간이면 동시 실행")]
    public float startTime = 0f;

    [Header("진행도 기반 트리거 (StartByTelegraphProgress)")]
    [Tooltip("텔레그래프가 이 비율에 도달하면 실행 (0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float triggerProgress = 0.75f;

    [Header("기본 설정")]
    [Tooltip("이 스텝의 행동 타입")]
    public BossActionType actionType = BossActionType.Prepare;

    [Tooltip("이 스텝의 지속 시간 (초)")]
    public float duration = 0.5f;

    [Tooltip("이 스텝 시작 시 재생할 애니메이션 트리거 이름 (비어있으면 무시)")]
    public string animationTrigger = "";

    [Tooltip("애니메이션 시작 지점 (0.0 = 처음부터, 0.125 = 12.5% 지점부터)")]
    [Range(0f, 1f)]
    public float animationStartOffset = 0f;

    [Tooltip("공격 애니메이션 시작 후 실제 데미지 판정까지의 딜레이 (초)")]
    public float damageDelay = 0f;

    [Tooltip("텔레그래프 표시와 동시에 재생할 VFX 프리팹 (번개 빌드업 등)")]
    public GameObject telegraphVFXPrefab;

    // ===== Teleport 전용 =====
    [Header("순간이동 설정 (Teleport 타입 전용)")]
    [Tooltip("순간이동 목적지 타입")]
    public TeleportTarget teleportTarget = TeleportTarget.NearPlayer;

    [Tooltip("플레이어로부터의 오프셋 거리 (NearPlayer일 때 사용)")]
    public float teleportOffset = 2.0f;

    [Tooltip("순간이동 시 VFX 프리팹 (사라질 때)")]
    public GameObject teleportDisappearVFX;

    [Tooltip("순간이동 시 VFX 프리팹 (나타날 때)")]
    public GameObject teleportAppearVFX;

    // ===== Move 전용 =====
    [Header("이동 설정 (Move 타입 전용)")]
    [Tooltip("이동 속도 배율 (보스 기본 이동속도 기준)")]
    public float moveSpeedMultiplier = 1.5f;

    [Tooltip("이동 목표 (true: 플레이어 방향, false: 현재 바라보는 방향)")]
    public bool moveTowardPlayer = true;

    [Tooltip("이동 거리")]
    public float moveDistance = 5.0f;

    [Tooltip("이동 중 재생할 VFX 프리팹 (번개 이펙트 등)")]
    public GameObject moveVFXPrefab;

    // ===== Telegraph 전용 =====
    [Header("텔레그래프 설정 (Telegraph 타입 전용)")]
    [Tooltip("텔레그래프 형태")]
    public BossTelegraphShape telegraphShape = BossTelegraphShape.Circle;

    [Tooltip("텔레그래프 위치 기준")]
    public TelegraphOrigin telegraphOrigin = TelegraphOrigin.Boss;

    [Tooltip("텔레그래프 범위 (반경 또는 길이)")]
    public float telegraphRange = 5.0f;

    [Tooltip("텔레그래프 각도 (Cone 타입 전용, 부채꼴 벌어지는 각도)")]
    public float telegraphAngle = 90f;

    [Tooltip("Fill 모드 사용 여부 (true면 안쪽→바깥 채움 연출)")]
    public bool useFillTelegraph = true;

    // ===== Attack 전용 =====
    [Header("공격 설정 (Attack 타입 전용)")]
    [Tooltip("기본 데미지 (페이즈 배율이 곱해짐)")]
    public int baseDamage = 30;

    [Tooltip("데미지 판정 범위 (반경)")]
    public float attackRadius = 4.0f;

    [Tooltip("데미지 판정 형태 (텔레그래프와 동일하게 맞추는 것을 권장)")]
    public BossTelegraphShape attackShape = BossTelegraphShape.Circle;

    [Tooltip("공격 판정 각도 (Cone 타입 전용)")]
    public float attackAngle = 90f;

    [Tooltip("공격 VFX 프리팹")]
    public GameObject attackVFXPrefab;

    [Tooltip("공격 투사체 프리팹 (설정 시 즉시 판정 대신 투사체 발사)")]
    public GameObject attackProjectilePrefab;

    [Tooltip("공격 VFX 크기 배율 (attackRadius × 이 값 = 최종 스케일). 프리팹 원본 크기에 맞춰 조절")]
    public float attackVFXScale = 0.5f;

    [Tooltip("투사체 발사 속도 (0이면 프리팹 기본값 사용)")]
    public float projectileSpeed = 15f;

    [Tooltip("피격 시 넉백 힘")]
    public float knockbackForce = 5.0f;

    [Header("추가 원형 번개 (페이즈 조건부 — Attack 타입 전용)")]
    [Tooltip("공격(슬램) 후 중심 주변에 원형으로 번개를 추가로 떨어뜨릴지")]
    public bool spawnCircularLightning = false;

    [Tooltip("이 페이즈 인덱스 이상에서만 번개 발동 (0=항상, 1=2페이즈/HP2000, 2=3페이즈/HP1000)")]
    public int lightningMinPhase = 2;

    [Tooltip("원형으로 떨어질 번개 개수")]
    public int lightningStrikeCount = 8;

    [Tooltip("중심에서 번개까지 거리 (반경)")]
    public float lightningRadius = 4f;

    [Tooltip("각 번개 데미지 (페이즈 데미지 배율 적용됨)")]
    public int lightningDamage = 30;

    [Tooltip("각 번개의 피격 판정 반경")]
    public float lightningHitRadius = 1.5f;

    [Tooltip("슬램 후 번개 떨어지기까지 딜레이 (초) — 플레이어 반응 시간")]
    public float lightningDelay = 0.4f;

    [Tooltip("번개 VFX 크기 배율 (lightningHitRadius × 이 값 = VFX 스케일). 판정 범위에 VFX 맞추기")]
    public float lightningVFXScale = 1f;

    [Tooltip("번개 VFX 프리팹")]
    public GameObject lightningVFXPrefab;

    // ===== Recovery 전용 =====
    [Header("후딜 설정 (Recovery 타입 전용)")]
    [Tooltip("후딜 중 회전 가능 여부 (false면 제자리 고정)")]
    public bool canRotateDuringRecovery = false;

    /// <summary>
    /// 이 스텝이 끝나는 시간 (시간 기반일 때만 유효)
    /// </summary>
    public float EndTime => startTime + duration;

    /// <summary>
    /// 이 스텝이 시간 기반 트리거인지 확인
    /// </summary>
    public bool IsTimeBased => triggerMode == StepTriggerMode.StartByTime;

    /// <summary>
    /// 이 스텝이 진행도 기반 트리거인지 확인
    /// </summary>
    public bool IsProgressBased => triggerMode == StepTriggerMode.StartByTelegraphProgress;
}

/// <summary>
/// 보스 공격 패턴 ScriptableObject
/// - 개별 공격 패턴 1개의 전체 데이터
/// - 시간 기반 + 진행도 기반 스텝을 혼합 사용 가능
/// 
/// 예시 (내려찍기 패턴):
///   [0] Time     0.0   Prepare    - 준비자세 (0.5초)
///   [1] Time     0.5   Telegraph  - 플레이어 위치에 원형 Fill (1.0초)
///   [2] Progress  0.75 Teleport   - Fill 75%에 사라짐 → 텔레그래프 위치로 이동
///   [3] Progress  1.0  Attack     - Fill 100%에 내려찍기 데미지 + VFX
///   [4] Time     자동  Recovery   - 후딜 (1.0초)
/// 
/// 예시 (접근 베기 패턴):
///   [0] Time     0.0   Prepare    - 준비자세 (0.3초)
///   [1] Time     0.3   Move       - 플레이어 근처로 빠른 이동 (0.4초)
///   [2] Time     0.7   Telegraph  - 부채꼴 범위 표시 (0.8초)
///   [3] Progress  1.0  Attack     - 베기 + VFX
///   [4] Time     자동  Recovery   - 후딜 (1.0초)
/// </summary>
[CreateAssetMenu(fileName = "BossAttackPattern", menuName = "Boss/Attack Pattern")]
public class BossAttackPatternSO : ScriptableObject
{
    [Header("패턴 기본 정보")]
    [Tooltip("패턴 이름 (디버깅 및 식별용)")]
    public string patternName = "New Pattern";

    [Tooltip("패턴 설명")]
    [TextArea(2, 4)]
    public string description = "";

    [Header("선택 조건")]
    [Tooltip("이 패턴의 선택 가중치 (높을수록 자주 선택됨)")]
    public float selectionWeight = 1.0f;

    [Tooltip("최소 사용 거리 (플레이어가 이보다 가까우면 사용 안 함)")]
    public float minRange = 0f;

    [Tooltip("최대 사용 거리 (플레이어가 이보다 멀면 사용 안 함)")]
    public float maxRange = 50f;

    [Tooltip("패턴 개별 쿨타임 (사용 후 N초간 재사용 불가)")]
    public float cooldown = 3.0f;

    [Header("액션 스텝 목록")]
    [Tooltip("패턴을 구성하는 행동들. triggerMode에 따라 시간 or 진행도 기반 실행")]
    public BossActionStep[] actionSteps;

    /// <summary>
    /// 거리 조건 충족 여부 확인
    /// </summary>
    public bool IsInRange(float distance)
    {
        return distance >= minRange && distance <= maxRange;
    }

    /// <summary>
    /// 전체 패턴 소요 시간 추정 (시간 기반 스텝들만 계산)
    /// - 진행도 기반 스텝은 텔레그래프 duration에 의존하므로 정확한 계산 불가
    /// </summary>
    public float EstimateTotalDuration()
    {
        float maxEndTime = 0f;

        if (actionSteps == null) return 0f;

        foreach (var step in actionSteps)
        {
            if (step.IsTimeBased && step.EndTime > maxEndTime)
            {
                maxEndTime = step.EndTime;
            }
        }

        return maxEndTime;
    }
}
