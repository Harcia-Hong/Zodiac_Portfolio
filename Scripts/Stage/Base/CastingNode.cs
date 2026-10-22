using UnityEngine;

/// ============================================================
/// CastingNode.cs — 시간 충전형 노드 (NodeBase 상속)
/// ============================================================
/// Stage 1-2 NodeStage에서 사용되는 노드 타입 중 하나.
///
/// [작동 방식]
///   1. NodeInteract(또는 외부)가 StartCasting() 호출 → 충전 시작
///   2. Update에서 매 프레임 시간 누적 → SetProgress() 갱신
///   3. castingTime에 도달하면 NodeBase가 자동 완료 처리
///
/// [리셋 조건 — 게이트키퍼식 디펜스]
///   - 영역 이탈 시 리셋 (resetOnExit)
///   - 플레이어 피격 시 리셋 (resetOnDamage)
///   → 적의 견제를 받으면서 N초 버텨야 함
///
/// [castingTime 활용]
///   - castingTime > 0 : 일반 충전 노드 (10초 충전 등)
///   - castingTime <= 0 : 즉시 활성화 노드 (F 한 번에 끝, 1-1 오벨리스크 식)
///   → 별도 InstantNode 클래스 불필요, CastingNode 하나로 두 모드 커버
///
/// [autoStartOnEnter 옵션]
///   - true : 영역 진입 시 자동 충전 시작 (F 인터랙트 없음)
///   - false : NodeInteract.StartCasting() 호출 필요 (디폴트)
/// ============================================================
public class CastingNode : NodeBase
{
    // ============================================================
    // 인스펙터 설정
    // ============================================================

    [Header("CastingNode 설정")]
    [SerializeField, Tooltip("충전 완료까지 걸리는 시간 (초). 0 이하면 즉시 활성화 노드")]
    private float castingTime = 10f;

    [SerializeField, Tooltip("F 인터랙트 없이 영역 진입 시 자동으로 충전 시작")]
    private bool autoStartOnEnter = false;

    [Header("리셋 조건")]
    [SerializeField, Tooltip("플레이어가 영역을 벗어나면 진행도 리셋")]
    private bool resetOnExit = true;

    [SerializeField, Tooltip("플레이어가 피격당하면 진행도 리셋")]
    private bool resetOnDamage = true;

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>현재 충전 중인지</summary>
    private bool isCasting = false;

    /// <summary>충전 시작부터 누적된 시간</summary>
    private float elapsedTime = 0f;

    // ============================================================
    // 피격 감지용 (PlayerHealth 매 프레임 비교)
    // ============================================================

    private PlayerHealth playerHealth;
    private int lastKnownHP;

    // ============================================================
    // 공개 프로퍼티 (UI / 디버깅용)
    // ============================================================

    public bool IsCasting => isCasting;
    public float CastingTime => castingTime;
    public float ElapsedTime => elapsedTime;

    /// <summary>즉시 활성화 노드 여부 (castingTime <= 0)</summary>
    public bool IsInstantNode => castingTime <= 0f;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    /// <summary>플레이어 컴포넌트 캐싱 (피격 감지용)</summary>
    protected override void Start()
    {
        base.Start();
        CachePlayerHealth();
    }

    /// <summary>
    /// 피격 감지에 필요한 PlayerHealth 컴포넌트 캐싱.
    /// resetOnDamage가 꺼져 있으면 캐싱 생략.
    /// </summary>
    private void CachePlayerHealth()
    {
        if (!resetOnDamage) return;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogWarning($"[CastingNode] '{NodeName}': Player 태그 오브젝트를 찾을 수 없습니다.");
            return;
        }

        playerHealth = playerObj.GetComponent<PlayerHealth>();
        if (playerHealth != null)
            lastKnownHP = playerHealth.curHealth;
    }

    /// <summary>
    /// 매 프레임 충전 진행 처리.
    /// 충전 중이 아니거나 이미 완료된 경우 즉시 종료.
    /// </summary>
    private void Update()
    {
        if (!isCasting) return;
        if (IsCompleted) return;

        // 1. 피격 감지 → 리셋
        if (resetOnDamage && CheckDamageTaken())
        {
            Debug.Log($"[CastingNode] '{NodeName}' 피격으로 충전 리셋");
            ResetCasting();
            return;
        }

        // 2. 영역 이탈 감지 → 리셋
        if (resetOnExit && !IsPlayerInRange)
        {
            Debug.Log($"[CastingNode] '{NodeName}' 영역 이탈로 충전 리셋");
            ResetCasting();
            return;
        }

        // 3. 시간 누적 + 진행도 갱신
        AccumulateTime();
    }

    // ============================================================
    // 피격 감지
    // ============================================================

    /// <summary>
    /// 플레이어 HP가 줄어들었는지 비교해 피격 여부 판정.
    /// (Enemy_Golem_Attack과 동일한 방식)
    /// </summary>
    /// <returns>피격당했으면 true</returns>
    private bool CheckDamageTaken()
    {
        if (playerHealth == null) return false;

        int currentHP = playerHealth.curHealth;
        bool isDamaged = currentHP < lastKnownHP;
        lastKnownHP = currentHP;

        return isDamaged;
    }

    // ============================================================
    // 시간 누적 + 진행도 갱신
    // ============================================================

    /// <summary>
    /// elapsedTime을 누적하고 진행도(0~1)로 변환해 SetProgress 호출.
    /// castingTime <= 0이면 즉시 1.0 반환 (즉시 활성화 모드).
    /// </summary>
    private void AccumulateTime()
    {
        elapsedTime += Time.deltaTime;

        // 즉시 활성화 모드: castingTime이 0 이하면 진행도 1.0
        // 일반 모드: elapsedTime / castingTime
        float progress = IsInstantNode
            ? 1f
            : Mathf.Clamp01(elapsedTime / castingTime);

        SetProgress(progress);
    }

    // ============================================================
    // 영역 진입 — 자동 시작 옵션
    // ============================================================

    /// <summary>
    /// NodeBase에서 호출되는 영역 진입 콜백.
    /// autoStartOnEnter 옵션이 켜져 있으면 자동으로 충전 시작.
    /// </summary>
    protected override void OnPlayerEnter()
    {
        if (!autoStartOnEnter) return;
        if (isCasting) return;
        if (IsCompleted) return;

        StartCasting();
    }

    // ============================================================
    // 외부 인터페이스 — NodeInteract가 호출
    // ============================================================

    /// <summary>
    /// 충전 시작.
    /// - NodeInteract가 F 입력 감지 시 호출
    /// - 영역 밖에 있거나 이미 충전 중이면 무시
    /// - 즉시 활성화 모드(castingTime <= 0)면 다음 Update에서 즉시 완료됨
    /// </summary>
    public void StartCasting()
    {
        if (isCasting) return;
        if (IsCompleted) return;

        // 영역 밖에서는 충전 시작 불가
        if (!IsPlayerInRange)
        {
            Debug.LogWarning($"[CastingNode] '{NodeName}': 영역 밖에서 충전 시도 — 무시");
            return;
        }

        isCasting = true;
        elapsedTime = 0f;

        // 피격 감지 기준점 갱신 (충전 시작 시점부터 비교)
        if (playerHealth != null)
            lastKnownHP = playerHealth.curHealth;

        Debug.Log($"[CastingNode] '{NodeName}' 충전 시작 " +
                  $"({(IsInstantNode ? "즉시 활성화" : $"{castingTime:F1}초")})");
    }

    /// <summary>
    /// 외부에서 강제로 충전을 중단시킬 때 사용 (디버그/특수 상황).
    /// </summary>
    public void StopCasting()
    {
        if (!isCasting) return;
        ResetCasting();
    }

    // ============================================================
    // 리셋 처리
    // ============================================================

    /// <summary>
    /// 충전 상태 + 누적 시간을 0으로 되돌리고 NodeBase의 ResetProgress 호출.
    /// 시각도 Idle 색상으로 페이드.
    /// </summary>
    private void ResetCasting()
    {
        if (!isCasting) return;

        isCasting = false;
        elapsedTime = 0f;
        ResetProgress();
    }

    // ============================================================
    // NodeBase 추상 메서드 구현
    // ============================================================

    /// <summary>
    /// NodeBase의 추상 메서드 구현.
    /// CastingNode는 Update에서 자체적으로 시간을 누적하므로 외부 강제 호출 시에만 사용.
    /// </summary>
    public override void TickProgress()
    {
        if (!isCasting) return;
        AccumulateTime();
    }

    // ============================================================
    // 완료 처리 — 충전 상태 정리 후 NodeBase 위임
    // ============================================================

    /// <summary>
    /// 노드 완료 처리 — 충전 플래그를 끄고 부모의 완료 로직 실행.
    /// </summary>
    protected override void CompleteNode()
    {
        isCasting = false;
        base.CompleteNode();
    }
}
