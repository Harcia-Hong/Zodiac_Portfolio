// BossStage.cs — 보스 스폰 + Wave 주입

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 1-3 보스전 컨트롤러
/// - StageBase 상속 (인트로 UI, 포탈 스폰 처리는 베이스가 담당)
/// - 클리어 조건: 보스 처치 (Boss.SetDeathCallback으로 감지)
/// - GlobalTimer는 Stage 1-1에서 이미 시작됨 → 여기서 리셋 X
/// - 보스 Prefab은 씬 참조를 못 가지므로, 천벌 패턴의 마커 Transform들은
///   여기 BossStage 인스펙터에 등록 → 스폰 직후 보스에게 주입
///
/// 흐름:
///   씬 진입 → OnStageStart() → 인트로 UI ("1-3 / 최후의 보스를 처치하라!")
///   → StartMission() → 보스 스폰 + 천벌 Wave 데이터 주입
///   → 플레이어 vs 보스
///   → 보스 처치 → HandleBossDeath() → OnStageCleared() → 포탈 스폰
/// </summary>
public class BossStage : StageBase
{
    // ============================================================
    // Inspector 설정
    // ============================================================

    [Header("Stage 1-3 보스 설정")]
    [SerializeField, Tooltip("스폰할 보스 프리팹 (Boss 컴포넌트 포함)")]
    private GameObject bossPrefab;

    [SerializeField, Tooltip("보스 스폰 위치 (없으면 이 Stage 오브젝트의 위치)")]
    private Transform bossSpawnPoint;

    [Header("천벌 패턴 — 마커 위치 데이터")]
    [SerializeField, Tooltip(
        "스폰된 보스의 BossRainAttack에 주입될 웨이브별 마커 Transform 목록.\n" +
        "Prefab이 씬 참조를 못 가지므로, 여기서 설정해서 런타임에 보스에게 전달함.")]
    private List<BossRainAttack.RainWaveData> rainWaves = new List<BossRainAttack.RainWaveData>();

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>스폰된 보스 인스턴스 (디버그/외부 참조용)</summary>
    private Boss spawnedBoss;

    // ============================================================
    // 생명주기
    // ============================================================

    /// <summary>
    /// Stage 1-3 진입 시 초기화
    /// - GlobalTimer는 리셋 X (Stage 1-1에서 시작된 누적 타이머 유지)
    /// - 보스 스폰은 StartMission에서 진행 (인트로 종료 후)
    /// </summary>
    protected override void OnStageStart()
    {
        Debug.Log("[BossStage] Stage 1-3 보스전 초기화");
    }

    // ============================================================
    // 미션 시작 (외부 호출용 — 인트로 종료 시 자동 호출)
    // ============================================================

    /// <summary>
    /// 미션 시작 — StageBase 인트로 종료 콜백에서 자동 호출
    /// 1) 베이스: isMissionStarted = true
    /// 2) 자식: 보스 스폰 + 사망 콜백 등록 + Wave 데이터 주입
    /// </summary>
    public override void StartMission()
    {
        base.StartMission();
        SpawnBoss();
    }

    // ============================================================
    // 보스 스폰
    // ============================================================

    /// <summary>
    /// 보스 프리팹을 스폰 위치에 인스턴스화
    /// - Boss 컴포넌트 확보 + 사망 콜백 등록
    /// - BossRainAttack에 마커 Wave 데이터 주입 (씬 참조 우회용)
    /// </summary>
    private void SpawnBoss()
    {
        // 프리팹 누락 방어
        if (bossPrefab == null)
        {
            Debug.LogError("[BossStage] bossPrefab이 설정되지 않았습니다. " +
                           "Inspector에서 보스 프리팹을 연결하세요.");
            return;
        }

        // 스폰 위치 결정 (spawnPoint 없으면 자기 자신 transform 사용)
        Vector3 spawnPos = (bossSpawnPoint != null) ? bossSpawnPoint.position : transform.position;
        Quaternion spawnRot = (bossSpawnPoint != null) ? bossSpawnPoint.rotation : Quaternion.identity;

        // 보스 인스턴스화
        GameObject bossObj = Instantiate(bossPrefab, spawnPos, spawnRot);

        // Boss 컴포넌트 확보
        spawnedBoss = bossObj.GetComponent<Boss>();
        if (spawnedBoss == null)
        {
            Debug.LogError($"[BossStage] 프리팹 '{bossPrefab.name}'에 Boss 컴포넌트가 없습니다.");
            Destroy(bossObj);
            return;
        }

        // 사망 시 콜백 등록 (인스턴스 단위 — static event보다 안전)
        spawnedBoss.SetDeathCallback(HandleBossDeath);

        // 🔑 천벌 패턴 Wave 데이터 주입
        // Prefab이 씬의 마커 Transform을 참조할 수 없으므로,
        // 스폰 직후 여기서 직접 주입해야 함
        InjectRainWaveData(bossObj);

        Debug.Log($"[BossStage] 보스 '{bossPrefab.name}' 스폰 완료");
    }

    /// <summary>
    /// 스폰된 보스의 BossRainAttack 컴포넌트에 마커 Wave 데이터 주입
    /// - 인스펙터의 rainWaves가 비어있으면 경고만 출력 (천벌 패턴이 작동 안 함)
    /// - BossRainAttack이 없는 보스면 스킵 (천벌 안 쓰는 보스도 호환)
    /// </summary>
    private void InjectRainWaveData(GameObject bossObj)
    {
        BossRainAttack rainAttack = bossObj.GetComponent<BossRainAttack>();

        if (rainAttack == null)
        {
            // 천벌 패턴 없는 보스도 허용
            Debug.Log("[BossStage] 보스에 BossRainAttack 없음 - 천벌 데이터 주입 스킵");
            return;
        }

        if (rainWaves == null || rainWaves.Count == 0)
        {
            Debug.LogWarning("[BossStage] rainWaves가 비어있음 - 인스펙터에서 웨이브 설정 필요");
            return;
        }

        rainAttack.SetWaves(rainWaves);
    }

    // ============================================================
    // 클리어 트리거 — 보스 처치 시 호출
    // ============================================================

    /// <summary>
    /// 보스 사망 콜백 핸들러 (Boss.SetDeathCallback으로 등록됨)
    /// 보스가 죽으면 즉시 호출되어 스테이지 클리어 처리
    /// </summary>
    private void HandleBossDeath()
    {
        Debug.Log("[BossStage] 보스 처치 신호 수신 → 스테이지 클리어 처리");

        // 베이스 클리어 처리 (clearDelay 대기 후 포탈 스폰)
        OnStageCleared();
    }

    // ============================================================
    // 클리어 후처리
    // ============================================================

    /// <summary>
    /// Stage 클리어 시 추가 처리
    /// - 보스는 자체적으로 사망 애니메이션 후 3초 뒤 Destroy되므로 추가 정리 불필요
    /// - 베이스: clearDelay 대기 → 포탈 스폰
    /// </summary>
    protected override void OnStageCleared()
    {
        // 보스는 자체 HandleDeath에서 정리 — 추가 호출 X
        base.OnStageCleared();
    }
}
