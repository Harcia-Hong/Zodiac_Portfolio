// EnemySpawnHandler.cs — 적 스폰 시 등장 애니메이션 + 무적/행동 정지 처리

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 적이 스폰될 때 등장 연출(Spawn 애니메이션)을 재생하고
/// 그 동안 행동 정지 + 무적 처리를 담당하는 컴포넌트
///
/// [동작 흐름]
/// 1. 활성화되자마자(OnEnable) Spawn 시퀀스 시작
/// 2. NavMeshAgent / Enemy / Collider 비활성화 (못 움직이고 못 맞음)
/// 3. doSpawn 트리거 → Spawn 애니메이션 재생
/// 4. spawnDuration만큼 대기
/// 5. 모든 컴포넌트 복구 → 정상 활동 시작
///
/// [선택적 사용]
/// - 이 컴포넌트가 없는 적은 기존처럼 즉시 활동 시작 (호환성 보장)
/// - Spawn 애니 클립이 없으면 트리거만 무시되고 대기 시간은 동일
///
/// [주의]
/// - prefab에 이 컴포넌트를 붙이면 그 즉시 모든 인스턴스에 스폰 연출 적용됨
/// - 보스나 특수 적은 별도 등장 연출이 있을 수 있으므로 선택해서 부착
/// </summary>
public class EnemySpawnHandler : MonoBehaviour
{
    // =========================================================
    // 인스펙터 설정
    // =========================================================

    [Header("스폰 애니메이션 설정")]
    [SerializeField, Tooltip("Spawn 애니메이션 트리거 이름 (Animator와 일치 필수)")]
    private string spawnAnimTrigger = "doSpawn";

    [SerializeField, Tooltip("Spawn 애니메이션 전체 길이 (초). 이 시간 동안 무적 + 정지")]
    private float spawnDuration = 1.0f;

    [Header("무적 처리 옵션")]
    [SerializeField, Tooltip("스폰 동안 콜라이더 비활성화 (true면 피격/충돌 모두 무시)")]
    private bool disableColliderDuringSpawn = true;

    [SerializeField, Tooltip("스폰 동안 NavMeshAgent 비활성화 (true면 이동/회피 정지)")]
    private bool disableNavDuringSpawn = true;

    [SerializeField, Tooltip("스폰 동안 Enemy 컴포넌트 비활성화 (true면 FSM 정지 = 추적/공격 차단)")]
    private bool disableEnemyDuringSpawn = true;

    // =========================================================
    // 컴포넌트 캐싱
    // =========================================================

    private Animator animator;
    private NavMeshAgent nav;
    private Collider col;
    private Enemy enemy;

    // =========================================================
    // 내부 상태
    // =========================================================

    /// <summary>현재 스폰 시퀀스 진행 중인지 (외부에서 조회 가능)</summary>
    public bool IsSpawning { get; private set; } = false;

    // =========================================================
    // 초기화
    // =========================================================

    /// <summary>
    /// 컴포넌트 자동 캐싱
    /// </summary>
    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        nav = GetComponent<NavMeshAgent>();
        col = GetComponent<Collider>();
        enemy = GetComponent<Enemy>();
    }

    /// <summary>
    /// 활성화 시 스폰 시퀀스 시작
    /// OnEnable을 사용하는 이유: Instantiate된 직후뿐 아니라
    /// 오브젝트 풀링으로 재활성화될 때도 동일하게 동작하기 위함
    /// </summary>
    private void OnEnable()
    {
        StartCoroutine(SpawnRoutine());
    }

    // =========================================================
    // 스폰 시퀀스
    // =========================================================

    /// <summary>
    /// 스폰 시퀀스 본체
    /// 1. 행동 정지 (Nav/Enemy 비활성화) + 무적 (Collider 비활성화)
    /// 2. Spawn 애니메이션 트리거
    /// 3. spawnDuration 대기
    /// 4. 모든 컴포넌트 복구
    /// </summary>
    private IEnumerator SpawnRoutine()
    {
        IsSpawning = true;

        // 1. 행동 정지 + 무적 처리
        ApplySpawnState(true);

        // 2. Spawn 애니메이션 재생
        // 애니메이터에 doSpawn 트리거가 없으면 그냥 무시됨 (안전)
        if (animator != null && !string.IsNullOrEmpty(spawnAnimTrigger))
        {
            animator.SetTrigger(spawnAnimTrigger);
        }

        // 3. 애니메이션 길이만큼 대기
        yield return new WaitForSeconds(spawnDuration);

        // 4. 모든 컴포넌트 복구
        ApplySpawnState(false);

        IsSpawning = false;
    }

    /// <summary>
    /// 스폰 상태 적용/해제 — 컴포넌트들의 enabled 일괄 토글
    /// </summary>
    /// <param name="isSpawning">true: 스폰 중 (비활성화) / false: 정상 (활성화)</param>
    private void ApplySpawnState(bool isSpawning)
    {
        // Collider — 피격/충돌 차단 또는 복구
        if (disableColliderDuringSpawn && col != null)
        {
            col.enabled = !isSpawning;
        }

        // NavMeshAgent — 이동 차단 또는 복구
        // 주의: NavMeshAgent를 disable했다가 enable할 때 위치 동기화 필요할 수 있음
        if (disableNavDuringSpawn && nav != null)
        {
            if (isSpawning)
            {
                nav.enabled = false;
            }
            else
            {
                nav.enabled = true;
                // 비활성화 동안 이동했을 가능성 거의 없지만 안전하게 위치 동기화
                if (nav.isOnNavMesh == false)
                    nav.Warp(transform.position);
            }
        }

        // Enemy 컴포넌트 — FSM 정지 (Update 차단 = 추적/공격 차단)
        if (disableEnemyDuringSpawn && enemy != null)
        {
            enemy.enabled = !isSpawning;
        }
    }

    // =========================================================
    // 외부 API (선택적 사용)
    // =========================================================

    /// <summary>
    /// 스폰 연출을 강제로 즉시 종료시키고 정상 활동 상태로 전환
    /// (특수 상황: 보스 등장 컷씬 종료 시점 등에서 외부에서 호출)
    /// </summary>
    public void SkipSpawn()
    {
        if (!IsSpawning) return;

        StopAllCoroutines();
        ApplySpawnState(false);
        IsSpawning = false;
    }
}
