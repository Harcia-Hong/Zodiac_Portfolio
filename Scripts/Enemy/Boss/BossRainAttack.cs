// BossRainAttack.cs — 디자이너 지정 블록 기반 천벌 패턴

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 페이즈 전환 특수 패턴 - "천벌"
///
/// [개요]
/// 보스가 사라진 뒤 디자이너가 인스펙터에서 지정한 위치들에 텔레그래프를 동시 생성하고,
/// Fill 완료 시 해당 위치에 번개/검 낙하 + 데미지 판정.
/// 웨이브별로 위치 패턴을 직접 지정 가능 (랜덤 분포 X).
///
/// [디자이너 워크플로우]
/// 1. 씬에 블록 위에 빈 GameObject 배치 (예: "Rain_Wave1_Point1")
/// 2. Boss_1의 BossRainAttack 컴포넌트 인스펙터 열기
/// 3. Waves 리스트 펼쳐서 각 웨이브의 Target Points에 GameObject 드래그
/// 4. (선택) 웨이브별 Fill 시간 / 후딜 override 설정
///
/// [외부 인터페이스 유지]
/// StartRainAttack(), StopRainAttack(), IsRunning, OnRainAttackComplete
/// — BossPhaseController가 호출하는 부분은 변경 없음
/// </summary>
public class BossRainAttack : MonoBehaviour
{
    // =========================================================
    // 데이터 구조
    // =========================================================

    /// <summary>
    /// 단일 웨이브의 데이터 (인스펙터에서 디자인)
    /// </summary>
    [System.Serializable]
    public class RainWaveData
    {
        [Tooltip("웨이브 이름 (디버그 / 인스펙터 가독성)")]
        public string waveName = "Wave";

        [Tooltip("이 웨이브에서 텔레그래프가 떨어질 위치들 (블록 위에 빈 GameObject 배치 후 드래그)")]
        public List<Transform> targetPoints = new List<Transform>();

        [Tooltip("이 웨이브의 Fill 시간 override (0이면 기본값 사용)")]
        public float fillDurationOverride = 0f;

        [Tooltip("이 웨이브 종료 후 대기 시간 override (0이면 기본값 사용)")]
        public float delayAfterWaveOverride = 0f;
    }

    // =========================================================
    // 인스펙터 설정
    // =========================================================

    [Header("웨이브 패턴 설정")]
    [SerializeField, Tooltip("각 웨이브의 텔레그래프 위치 목록 (위에서 아래 순서로 실행)")]
    private List<RainWaveData> waves = new List<RainWaveData>();

    [Header("기본 타이밍 (웨이브가 override 안 할 때)")]
    [SerializeField, Tooltip("기본 Fill 시간 (텔레그래프가 차오르는 시간)")]
    private float baseFillDuration = 1.5f;

    [SerializeField, Tooltip("기본 웨이브 간 대기 시간")]
    private float delayBetweenWaves = 0.5f;

    [Header("텔레그래프 시각")]
    [SerializeField, Tooltip("텔레그래프 반지름 (VFX 스케일 계산 기준)")]
    private float telegraphRadius = 3f;

    [Header("데미지")]
    [SerializeField, Tooltip("낙하 1회당 데미지")]
    private int damage = 40;

    [SerializeField, Tooltip("넉백 힘 (0이면 넉백 없음)")]
    private float knockbackForce = 8f;

    [SerializeField, Tooltip("데미지 판정 반경 (텔레그래프 반지름과 일치 권장)")]
    private float damageRadius = 3f;

    [SerializeField, Tooltip("VFX 생성 후 데미지 판정까지 딜레이 (번개가 바닥에 닿는 시점)")]
    private float impactDamageDelay = 0.2f;

    [SerializeField, Tooltip("플레이어 레이어 마스크")]
    private LayerMask playerLayer;

    [Header("프리팹")]
    [SerializeField, Tooltip("낙하 VFX 프리팹 (번개/검). 내부에 텔레그래프 연출 포함 권장")]
    private GameObject impactVFXPrefab;

    [Header("VFX 속도 제어")]
    [SerializeField, Tooltip("VFX 프리팹의 원본 재생 시간 (실제 클립 길이)")]
    private float vfxOriginalDuration = 2f;

    [SerializeField, Tooltip("VFX 크기 배율 (telegraphRadius * 이 값 = 최종 스케일)")]
    private float vfxScaleMultiplier = 0.5f;

    // =========================================================
    // 내부 상태
    // =========================================================

    private bool isRunning = false;
    private Transform playerTransform;
    private Coroutine rainCoroutine;
    private readonly List<GameObject> activeVFX = new List<GameObject>();

    /// <summary>천벌 패턴 실행 중 여부 (BossPhaseController가 polling)</summary>
    public bool IsRunning => isRunning;

    /// <summary>패턴 완료 시 호출되는 이벤트 (다음 페이즈 진행용)</summary>
    public event System.Action OnRainAttackComplete;

    // =========================================================
    // Public 인터페이스 (외부 호출용)
    // =========================================================

    /// <summary>
    /// 천벌 패턴 시작 (BossPhaseController에서 호출)
    /// 웨이브가 비어있거나 플레이어가 없으면 즉시 실패
    /// </summary>
    public void StartRainAttack()
    {
        if (isRunning) return;

        // 웨이브 데이터 검증
        if (waves == null || waves.Count == 0)
        {
            Debug.LogError("[BossRainAttack] 웨이브가 비어있음 — 인스펙터에서 waves 설정 필요");
            return;
        }

        // 플레이어 찾기 (넉백 적용용)
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;

        if (playerTransform == null)
        {
            Debug.LogError("[BossRainAttack] Player를 찾을 수 없음");
            return;
        }

        isRunning = true;
        rainCoroutine = StartCoroutine(RainAttackCoroutine());

        Debug.Log($"[BossRainAttack] 천벌 패턴 시작 (총 {waves.Count} 웨이브)");
    }

    /// <summary>
    /// 천벌 패턴 강제 중단 (페이즈 전환, 보스 사망 등)
    /// 활성 VFX 모두 제거
    /// </summary>
    public void StopRainAttack()
    {
        if (!isRunning) return;

        if (rainCoroutine != null)
        {
            StopCoroutine(rainCoroutine);
            rainCoroutine = null;
        }

        CleanupAllVFX();
        isRunning = false;
    }

    /// <summary>
    /// 외부에서 Wave 데이터 주입 (BossStage 등에서 스폰 직후 호출)
    /// Prefab 기반 보스가 씬의 Transform 마커를 참조하기 위한 우회 경로
    ///
    /// 사용 예시:
    ///   BossStage.SpawnBoss() {
    ///       GameObject obj = Instantiate(bossPrefab);
    ///       obj.GetComponent<BossRainAttack>().SetWaves(myMarkerSetup);
    ///   }
    /// </summary>
    /// <param name="newWaves">씬 Transform 참조를 포함한 웨이브 데이터 리스트</param>
    public void SetWaves(List<RainWaveData> newWaves)
    {
        if (newWaves == null || newWaves.Count == 0)
        {
            Debug.LogWarning("[BossRainAttack] SetWaves에 빈 리스트가 전달됨");
            return;
        }

        waves = newWaves;
        Debug.Log($"[BossRainAttack] Wave 데이터 주입 완료 ({waves.Count} 웨이브)");
    }

    // =========================================================
    // 메인 코루틴
    // =========================================================

    /// <summary>
    /// 천벌 패턴 메인 루프
    /// - 각 웨이브 순차 실행
    /// - 웨이브별로 위치 추출 → VFX 생성 → 데미지 딜레이 → 데미지 판정 → 후딜
    /// </summary>
    private IEnumerator RainAttackCoroutine()
    {
        for (int waveIdx = 0; waveIdx < waves.Count; waveIdx++)
        {
            RainWaveData wave = waves[waveIdx];

            // 1. 웨이브별 타이밍 결정 (override 우선, 없으면 기본값)
            float fillDuration = wave.fillDurationOverride > 0f
                ? wave.fillDurationOverride
                : baseFillDuration;

            float waveDelay = wave.delayAfterWaveOverride > 0f
                ? wave.delayAfterWaveOverride
                : delayBetweenWaves;

            // 2. 위치 추출 (null Transform 제외)
            List<Vector3> positions = ExtractWavePositions(wave);

            if (positions.Count == 0)
            {
                Debug.LogWarning($"[BossRainAttack] '{wave.waveName}' — 유효 위치 0개, 스킵");
                yield return new WaitForSeconds(waveDelay);
                continue;
            }

            Debug.Log($"[BossRainAttack] '{wave.waveName}' 시작 ({positions.Count}개 위치, Fill: {fillDuration}s)");

            // 3. VFX 동시 생성 — VFX 자체가 텔레그래프 + 낙하 연출 포함
            float vfxSpeed = vfxOriginalDuration / fillDuration;
            SpawnImpactVFXAtPositions(positions, vfxSpeed, fillDuration);

            // 4. 번개 낙하 시점까지 대기 (VFX 내부 타이밍에 맞춤)
            yield return new WaitForSeconds(impactDamageDelay / vfxSpeed);

            // 5. 데미지 판정
            DealDamageAtPositions(positions);

            // 6. 다음 웨이브 대기
            yield return new WaitForSeconds(waveDelay);
        }

        // 패턴 완료 → 콜백 발사
        isRunning = false;
        OnRainAttackComplete?.Invoke();
        Debug.Log("[BossRainAttack] 천벌 패턴 완료");
    }

    // =========================================================
    // 내부 헬퍼
    // =========================================================

    /// <summary>
    /// 웨이브의 Transform 리스트에서 유효한 위치(null 아닌) 추출
    /// </summary>
    private List<Vector3> ExtractWavePositions(RainWaveData wave)
    {
        List<Vector3> positions = new List<Vector3>();

        if (wave.targetPoints == null) return positions;

        foreach (Transform t in wave.targetPoints)
        {
            if (t != null)
                positions.Add(t.position);
        }

        return positions;
    }

    /// <summary>
    /// 지정 위치들에 impactVFXPrefab을 동시 인스턴스화
    /// 스케일 조정 + 속도 조절 + 자동 파괴 처리
    /// </summary>
    private void SpawnImpactVFXAtPositions(List<Vector3> positions, float vfxSpeed, float fillDuration)
    {
        if (impactVFXPrefab == null)
        {
            Debug.LogWarning("[BossRainAttack] impactVFXPrefab이 비어있음");
            return;
        }

        float vfxScale = telegraphRadius * vfxScaleMultiplier;

        foreach (Vector3 pos in positions)
        {
            GameObject vfx = Instantiate(impactVFXPrefab, pos, Quaternion.identity);
            vfx.transform.localScale = Vector3.one * vfxScale;

            AdjustVFXSpeed(vfx, vfxSpeed);

            // 안전한 시간 후 자동 파괴 (FillDuration + 여유)
            Destroy(vfx, fillDuration + 1f);
            activeVFX.Add(vfx);
        }
    }

    /// <summary>
    /// 모든 위치에 OverlapSphere 판정 + 데미지 + 넉백 적용
    /// </summary>
    private void DealDamageAtPositions(List<Vector3> positions)
    {
        foreach (Vector3 pos in positions)
        {
            Collider[] hits = Physics.OverlapSphere(pos, damageRadius, playerLayer);

            foreach (var hit in hits)
            {
                PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
                if (playerHealth == null) continue;

                playerHealth.TakeDamage(damage);

                // 넉백 (Rigidbody 있을 때만)
                if (knockbackForce > 0f)
                {
                    Rigidbody rb = hit.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Vector3 dir = (hit.transform.position - pos).normalized;
                        dir.y = 0f;
                        rb.AddForce(dir * knockbackForce, ForceMode.Impulse);
                    }
                }
            }
        }
    }

    /// <summary>
    /// VFX의 모든 ParticleSystem 재생 속도 일괄 조절
    /// fillDuration에 맞춰 빨리/느리게 재생
    /// </summary>
    private void AdjustVFXSpeed(GameObject vfxObj, float speed)
    {
        ParticleSystem[] particles = vfxObj.GetComponentsInChildren<ParticleSystem>();

        foreach (var ps in particles)
        {
            var main = ps.main;
            main.simulationSpeed = speed;
        }
    }

    /// <summary>
    /// 현재 활성화된 모든 VFX 제거 (강제 중단 시)
    /// </summary>
    private void CleanupAllVFX()
    {
        foreach (var obj in activeVFX)
        {
            if (obj != null) Destroy(obj);
        }
        activeVFX.Clear();
    }

    private void OnDestroy()
    {
        CleanupAllVFX();
    }

    // =========================================================
    // 디버그 (씬 뷰 시각화)
    // =========================================================

    /// <summary>
    /// 씬 뷰에서 각 웨이브 위치를 색으로 구분해 표시
    /// 와이어 원 = 데미지 범위 / 작은 큐브 = 웨이브 마커
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (waves == null) return;

        // 웨이브별 색상 (구분용)
        Color[] waveColors = new Color[]
        {
            Color.red, Color.yellow, Color.green, Color.cyan, Color.magenta, Color.blue
        };

        for (int i = 0; i < waves.Count; i++)
        {
            Color c = waveColors[i % waveColors.Length];
            Gizmos.color = c;

            if (waves[i].targetPoints == null) continue;

            foreach (Transform t in waves[i].targetPoints)
            {
                if (t == null) continue;

                // 데미지 범위 원
                Gizmos.DrawWireSphere(t.position, telegraphRadius);

                // 웨이브 식별 마커 (작은 큐브)
                Gizmos.DrawCube(t.position + Vector3.up * 0.5f, Vector3.one * 0.3f);
            }
        }
    }
}
