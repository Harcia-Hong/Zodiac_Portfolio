using UnityEngine;

/// <summary>
/// 세그먼트 트윈즈 스타일 스테이지 포탈
/// - 스테이지 클리어 후 자동 생성되어 다음 씬으로 이동시키는 트리거 포탈
/// - 단일 책임: 트리거 감지 + 씬 전환만 처리 (비주얼은 프리팹에서 셋업)
/// - 호출 흐름: StageBase 클리어 → 포탈 스폰 → 플레이어 접촉 → FadeTransition → 다음 씬
///
/// 프리팹 셋업 (이 스크립트 외 비주얼 컴포넌트):
/// - 세로 스크롤 텍스트 (ScrollingText, Direction=Down) — "BATTLE BATTLE BATTLE..."
/// - "NEXT" 라벨 + 노란 역삼각형 화살표
/// - 시안/마젠타 색수차 라인 (옵션)
/// - Trigger 콜라이더 (이 스크립트 동작 조건)
/// </summary>
[RequireComponent(typeof(Collider))]
public class StagePortal : MonoBehaviour
{
    [Header("씬 전환 설정")]
    [SerializeField, Tooltip("이동할 다음 씬 이름 (예: \"Stage1-2\")")]
    private string targetSceneName;

    [Header("게임 클리어 모드")]
    [SerializeField, Tooltip("체크하면 씬 로드 대신 GameClearUI(결과창)를 띄움. 보스 클리어 포탈 전용.")]
    private bool showGameClearUI = false;

    [SerializeField, Tooltip("FadeTransition 검정 화면에서 표시할 메시지 (비우면 인트로 UI로 대체)")]
    private string transitionMessage = "";

    [Header("동작 옵션")]
    [SerializeField, Tooltip("1회만 작동 (중복 트리거 방지)")]
    private bool oneShot = true;

    [SerializeField, Tooltip("모든 적 처치 후에만 작동 (안전장치, 보통 OFF — StageBase가 클리어 후에만 스폰하므로 불필요)")]
    private bool requireAllEnemiesDead = false;

    [SerializeField, Tooltip("플레이어 태그 (감지 대상)")]
    private string playerTag = "Player";

    // oneShot 처리용 — 이미 작동한 적이 있는지
    private bool isActivated = false;

    /// <summary>
    /// 에디터 검증 — 콜라이더가 Trigger로 설정되어 있는지 확인
    /// Inspector에서 값 변경 시마다 자동 호출됨
    /// </summary>
    private void OnValidate()
    {
        // 트리거가 아니면 포탈이 작동하지 않으므로 경고
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[{nameof(StagePortal)}] '{name}'의 Collider는 isTrigger=true여야 합니다.", this);
        }
    }

    /// <summary>
    /// 플레이어가 트리거 영역에 진입했을 때 호출
    /// - oneShot 체크 → 플레이어 태그 검사 → 잔여 적 검사 → 포탈 작동
    /// </summary>
    /// <param name="other">트리거 영역에 들어온 콜라이더</param>
    private void OnTriggerEnter(Collider other)
    {
        // 이미 작동했으면 무시 (씬 전환 도중 또 트리거되는 것 방지)
        if (oneShot && isActivated) return;

        // 플레이어 외 오브젝트는 무시
        if (!other.CompareTag(playerTag)) return;

        // 안전장치 — 잔여 적이 있으면 포탈 작동 차단 (옵션)
        if (requireAllEnemiesDead && HasRemainingEnemies())
        {
            Debug.Log($"[{nameof(StagePortal)}] 잔여 적 존재 — 포탈 작동 차단");
            return;
        }

        // 모든 조건 통과 → 씬 전환 실행
        ActivatePortal();
    }

    /// <summary>
    /// 포탈 작동 — SceneLoader를 통해 페이드 컷씬과 함께 다음 씬 로드
    /// 외부 호출용 ForceActivate()도 이 메서드를 거침
    /// </summary>
    private void ActivatePortal()
    {
        isActivated = true;

        // [NEW] 게임 클리어 모드 — 씬 로드 대신 GameClearUI 결과창 표시
        // (GameClearUI는 DontDestroyOnLoad라 MainMenu에서 따라와 보스 씬에서도 살아있음)
        if (showGameClearUI)
        {
            if (GameClearUI.Instance != null)
            {
                Debug.Log($"[{nameof(StagePortal)}] 게임 클리어 — GameClearUI 표시");
                GameClearUI.Instance.Show(GameClearUI.GameClearMode.Clear);
            }
            else
            {
                Debug.LogError($"[{nameof(StagePortal)}] GameClearUI.Instance 없음. MainMenu부터 시작했는지 확인하세요.", this);
            }
            return;  // 씬 로드 안 함
        }

        // 일반 씬 전환
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError($"[{nameof(StagePortal)}] '{name}'의 targetSceneName이 비어있습니다.", this);
            return;
        }

        Debug.Log($"[{nameof(StagePortal)}] '{targetSceneName}' 씬으로 전환");
        SceneLoader.LoadStageTransition(targetSceneName, transitionMessage);
    }

    /// <summary>
    /// 잔여 적 존재 여부 확인 — EnemyManager에 등록된 적 수가 0보다 큰지 검사
    /// </summary>
    /// <returns>잔여 적이 1마리 이상이면 true</returns>
    private bool HasRemainingEnemies()
    {
        return EnemyManager.GetAllEnemies().Count > 0;
    }

    /// <summary>
    /// 외부 강제 작동 — 컷씬, 보스 처치 자동 전환 등에서 호출
    /// 트리거 영역에 들어가지 않고도 포탈을 발동시킬 수 있음
    /// </summary>
    public void ForceActivate()
    {
        if (oneShot && isActivated) return;
        ActivatePortal();
    }

    /// <summary>
    /// 작동 상태 리셋 — 테스트나 포탈 재사용이 필요할 때 호출
    /// (MonoBehaviour.Reset()과 충돌 방지를 위해 ResetState로 명명)
    /// </summary>
    public void ResetState()
    {
        isActivated = false;
    }

    /// <summary>
    /// 런타임에 타겟 씬 동적 설정
    /// StageBase가 포탈을 동적 스폰한 뒤 다음 씬 이름을 주입할 때 사용
    /// </summary>
    /// <param name="sceneName">이동할 씬 이름</param>
    /// <param name="message">FadeTransition 검정 화면 메시지 (선택)</param>
    public void SetTargetScene(string sceneName, string message = "")
    {
        targetSceneName = sceneName;
        transitionMessage = message;
    }
}
