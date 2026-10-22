using UnityEngine;

/// <summary>
/// Stage 1-1 컨트롤러 — 웨이브 기반 전투 스테이지 (세그먼트 트윈즈 스타일)
/// - StageBase 상속 (인트로 UI, 포탈 스폰 처리는 베이스가 담당)
/// - 클리어 조건: StageEnemySpawner의 모든 웨이브 종료 (OnAllWavesCompleted 이벤트)
/// - 게이지(progressPerKill) 시스템 폐기 — 웨이브 기반으로 전환됨
///
/// 흐름:
///   씬 진입 → OnStageStart() → 인트로 UI → StartMission() → 스포너 시작
///   → 웨이브 1 스폰 → 처치 → 휴식 → 웨이브 2 스폰 → 처치
///   → OnAllWavesCompleted 발화 → HandleAllWavesCompleted → OnStageCleared() → 포탈 스폰
///
/// Stage 1-1은 Chapter 1의 첫 Stage이므로 GlobalTimer를 0초부터 시작
/// </summary>
public class ObeliskStage : StageBase
{
    // ============================================================
    // Inspector 설정
    // ============================================================

    [Header("Stage 1-1 설정")]
    [SerializeField, Tooltip("적 스포너 (씬에 배치된 StageEnemySpawner). 웨이브 시스템이 클리어 조건 자동 판정")]
    private StageEnemySpawner enemySpawner;

    // ============================================================
    // 생명주기
    // ============================================================

    /// <summary>
    /// Stage 1-1 진입 시 초기화 (StageBase.Start에서 자동 호출)
    /// - GlobalTimer 리셋 + 시작 (Chapter 1의 첫 Stage이므로 0초부터)
    /// - 스포너의 OnAllWavesCompleted 이벤트 구독 (클리어 트리거)
    /// </summary>
    protected override void OnStageStart()
    {
        // Stage 1-1은 Chapter 첫 Stage → 글로벌 타이머를 0초부터 새로 시작
        if (GlobalTimer.Instance != null)
        {
            GlobalTimer.Instance.ResetTimer();
            GlobalTimer.Instance.StartTimer();
        }
        else
        {
            Debug.LogWarning("[ObeliskStage] GlobalTimer.Instance가 없습니다. " +
                             "MainMenu 씬에 GlobalTimer GameObject가 배치되어 있는지 확인하세요.");
        }

        // 스포너의 '모든 웨이브 종료' 이벤트 구독 → 발화 시 스테이지 클리어
        if (enemySpawner != null)
        {
            enemySpawner.OnAllWavesCompleted += HandleAllWavesCompleted;
        }
        else
        {
            Debug.LogError("[ObeliskStage] enemySpawner가 설정되지 않았습니다. " +
                           "Inspector에서 StageEnemySpawner를 연결하세요.");
        }
    }

    /// <summary>
    /// Stage 종료(씬 전환 등) 시 이벤트 구독 해제
    /// 메모리 누수 + 죽은 객체 참조로 인한 NullReference 방지
    /// </summary>
    private void OnDisable()
    {
        if (enemySpawner != null)
        {
            enemySpawner.OnAllWavesCompleted -= HandleAllWavesCompleted;
        }
    }

    // ============================================================
    // 미션 시작 (외부 호출용 — 인트로 종료 시 자동 호출)
    // ============================================================

    /// <summary>
    /// 미션 시작 — StageBase 인트로 종료 콜백에서 자동 호출
    /// 1) 베이스: isMissionStarted = true + 게이지 0으로 초기화 (게이지 미사용이지만 상태 기록)
    /// 2) 자식: 적 스포너의 웨이브 시작
    /// </summary>
    public override void StartMission()
    {
        // 베이스 처리 (중복 호출 방지)
        base.StartMission();

        // 웨이브 스폰 시작 (StageEnemySpawner가 자체적으로 웨이브 순회 + 클리어 판정)
        if (enemySpawner != null)
        {
            enemySpawner.StartSpawning();
        }
        else
        {
            Debug.LogError("[ObeliskStage] enemySpawner가 설정되지 않아 스폰을 시작할 수 없습니다.");
        }
    }

    // ============================================================
    // 클리어 트리거 — 모든 웨이브 종료 시 호출
    // ============================================================

    /// <summary>
    /// StageEnemySpawner의 OnAllWavesCompleted 이벤트 핸들러
    /// 모든 웨이브가 끝나면 (= 마지막 웨이브의 적 전원 처치) 스테이지 클리어 처리
    /// </summary>
    private void HandleAllWavesCompleted()
    {
        Debug.Log("[ObeliskStage] 모든 웨이브 종료 신호 수신 → 스테이지 클리어 처리");

        // 베이스 클리어 처리 (포탈 스폰 또는 자동 씬 전환)
        OnStageCleared();
    }

    // ============================================================
    // 클리어 후처리 (잡몹 스폰 정지 + 살아있는 적 정리)
    // ============================================================

    /// <summary>
    /// Stage 클리어 시 추가 처리
    /// 1) 자식: 적 스폰 정지 + 살아있는 적 제거 (보통 모든 웨이브 종료 후라 비어있지만 안전장치)
    /// 2) 베이스: clearDelay 대기 → 포탈 스폰 (또는 자동 씬 전환)
    /// </summary>
    protected override void OnStageCleared()
    {
        // 적 스폰 정지 + 살아있는 적 정리 (안전장치 — 이미 비어있을 가능성 높음)
        if (enemySpawner != null)
        {
            enemySpawner.StopSpawning();
            enemySpawner.ClearAllEnemies();
        }

        // 베이스: clearDelay 대기 + 포탈 스폰 (또는 자동 씬 전환)
        base.OnStageCleared();
    }
}
