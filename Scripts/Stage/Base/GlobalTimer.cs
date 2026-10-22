using UnityEngine;

/// <summary>
/// 글로벌 타이머 (Chapter 전체 누적)
/// - Stage 1-1 → 1-2 → 1-3 거치는 동안 시간이 계속 흐름 (씬 전환 후에도 유지)
/// - 시간 = 난이도: secondsPerLevel초마다 적 레벨 +1
/// - 사용 흐름:
///     Chapter 시작 시:   GlobalTimer.Instance.StartTimer()
///     Chapter 종료 시:   GlobalTimer.Instance.ResetTimer()  (사망/클리어/메인메뉴 복귀)
///     적 스폰 시 레벨:   GlobalTimer.CurrentLevel
/// </summary>
public class GlobalTimer : MonoBehaviour
{
    public static GlobalTimer Instance { get; private set; }

    // ============================================================
    // 설정값
    // ============================================================

    [Header("레벨업 주기")]
    [SerializeField, Tooltip("몇 초마다 레벨이 +1 되는지 (기본 60초)")]
    private float secondsPerLevel = 60f;

    // ============================================================
    // 런타임 상태 (디버깅용)
    // ============================================================

    [Header("현재 상태 (디버깅용 - 직접 수정 금지)")]
    [SerializeField, Tooltip("현재 누적 시간 (초)")]
    private float currentTime = 0f;

    [SerializeField, Tooltip("타이머 작동 중인지")]
    private bool isRunning = false;

    // ============================================================
    // 정적 프로퍼티 (외부 접근용)
    // ============================================================

    /// <summary>
    /// 현재 누적 시간 (초)
    /// - Instance가 없으면 0 반환 (씬에 GlobalTimer GameObject 없을 때 안전 처리)
    /// </summary>
    public static float GlobalTime => Instance != null ? Instance.currentTime : 0f;

    /// <summary>
    /// 현재 레벨 (1부터 시작, secondsPerLevel초마다 +1)
    /// - 예: 60초 = Lv2, 120초 = Lv3, ...
    /// - Instance가 없으면 1 반환
    /// </summary>
    public static int CurrentLevel
    {
        get
        {
            if (Instance == null) return 1;
            return 1 + Mathf.FloorToInt(Instance.currentTime / Instance.secondsPerLevel);
        }
    }

    /// <summary>타이머가 현재 작동 중인지 여부</summary>
    public static bool IsRunning => Instance != null && Instance.isRunning;

    // ============================================================
    // Unity 생명주기
    // ============================================================

    /// <summary>
    /// 싱글톤 초기화 + DontDestroyOnLoad
    /// 씬마다 중복 생성되는 것 방지
    /// </summary>
    private void Awake()
    {
        // 이미 인스턴스가 있으면 자기 자신 파괴
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 매 프레임 시간 누적 (isRunning일 때만)
    /// 게임 일시정지 중에도 작동시키려면 unscaledDeltaTime으로 변경 가능
    /// </summary>
    private void Update()
    {
        if (isRunning)
        {
            currentTime += Time.deltaTime;
        }
    }

    // ============================================================
    // Public Interface
    // ============================================================

    /// <summary>
    /// 타이머 시작 (Chapter 1-1 진입 시 호출)
    /// 누적 시간은 유지 — 이미 진행 중이던 타이머를 이어서 시작
    /// 메인 메뉴 → Stage 1-1 진입 시 ObeliskStage 같은 컨트롤러에서 호출
    /// </summary>
    public void StartTimer()
    {
        isRunning = true;
        Debug.Log($"[GlobalTimer] 타이머 시작 (현재 시간: {currentTime:F1}초, 레벨: {CurrentLevel})");
    }

    /// <summary>
    /// 타이머 일시 정지 (메뉴/사망 UI 등 잠시 멈출 때)
    /// 누적 시간은 유지됨 — 다시 StartTimer() 호출하면 이어서 진행
    /// </summary>
    public void StopTimer()
    {
        isRunning = false;
        Debug.Log($"[GlobalTimer] 타이머 정지 (현재 시간: {currentTime:F1}초)");
    }

    /// <summary>
    /// 타이머 완전 리셋 (Chapter 클리어, 사망, 메인 메뉴 복귀 시 호출)
    /// 누적 시간 0초로 초기화 + 정지 상태로
    /// RunManager.ResetRun()에서 호출하는 것을 권장
    /// </summary>
    public void ResetTimer()
    {
        currentTime = 0f;
        isRunning = false;
        Debug.Log("[GlobalTimer] 타이머 리셋");
    }

    // ============================================================
    // UI 표시용 헬퍼
    // ============================================================

    /// <summary>
    /// 현재 시간을 mm:ss 형식 문자열로 반환 (UI 표시용)
    /// 예: 75초 → "01:15"
    /// 나중에 좌측 상단 UI 만들 때 활용
    /// </summary>
    public static string GetFormattedTime()
    {
        float t = GlobalTime;
        int minutes = Mathf.FloorToInt(t / 60f);
        int seconds = Mathf.FloorToInt(t % 60f);
        return $"{minutes:D2}:{seconds:D2}";
    }
}
