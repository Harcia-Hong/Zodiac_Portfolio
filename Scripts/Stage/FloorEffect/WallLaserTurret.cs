// WallLaserTurret.cs — 벽 고정 주기 발동 레이저 포탑

using System.Collections;
using UnityEngine;

/// <summary>
/// 벽에 고정되어 주기적으로 직선 레이저를 발사하는 해저드
///
/// [동작 흐름]
/// 1. 초기 지연 후 루프 시작
/// 2. 루프:
///    a. 경고 단계 — warningBeam (얇은 라인) 표시, warningDuration 대기
///    b. 발사 단계 — mainBeam (두꺼운 빔) 표시 + 데미지 판정 시작
///    c. 발사 중 damageInterval마다 OverlapBox로 영역 안 플레이어 데미지
///    d. fireDuration 종료 → mainBeam 끄기 + 데미지 정지
///    e. 쿨다운
/// 3. 반복
///
/// [시각 시스템]
/// - warningBeam / mainBeam은 자식 GameObject (VFX 프리팹 또는 직접 모델)
/// - 스크립트는 SetActive로만 on/off (회전/이동 없음)
/// - 빔 방향은 laserOriginPoint의 forward를 따름
///
/// [데미지 판정]
/// - OverlapBox로 빔 영역 안의 플레이어 감지
/// - 발사 중 damageInterval마다 1회 데미지 (도트 방식)
/// - 회피 인센티브 + 회피 보상 명확
/// </summary>
public class WallLaserTurret : MonoBehaviour
{
    // =========================================================
    // 주기 타이밍
    // =========================================================

    [Header("주기 타이밍")]
    [SerializeField, Tooltip("첫 발동 전 지연 (여러 포탑 시간차용)")]
    private float initialDelay = 0f;

    [SerializeField, Tooltip("경고 단계 시간 — 얇은 라인으로 경로 표시")]
    private float warningDuration = 0.8f;

    [SerializeField, Tooltip("발사 단계 시간 — 두꺼운 빔 + 데미지")]
    private float fireDuration = 1.5f;

    [SerializeField, Tooltip("발사 종료 후 다음 발동까지 쿨다운")]
    private float cooldownDuration = 2.0f;

    // =========================================================
    // 데미지 설정
    // =========================================================

    [Header("데미지 설정")]
    [SerializeField, Tooltip("1틱 데미지량")]
    private int damage = 10;

    [SerializeField, Tooltip("발사 중 데미지 적용 주기 (초)")]
    private float damageInterval = 0.3f;

    [SerializeField, Tooltip("플레이어 레이어")]
    private LayerMask playerLayer;

    // =========================================================
    // 빔 영역 설정
    // =========================================================

    [Header("빔 영역")]
    [SerializeField, Tooltip("빔 시작 위치 (포구, 없으면 이 GameObject 위치)")]
    private Transform laserOriginPoint;

    [SerializeField, Tooltip("빔 최대 길이 (m)")]
    private float laserMaxLength = 20f;

    [SerializeField, Tooltip("빔 두께 (가로 폭)")]
    private float laserWidth = 0.5f;

    [SerializeField, Tooltip("빔 두께 (세로 높이)")]
    private float laserHeight = 1f;

    // =========================================================
    // 시각 오브젝트 (VFX)
    // =========================================================

    [Header("시각 오브젝트")]
    [SerializeField, Tooltip("경고 단계 빔 시각 (얇은 라인 또는 페이드된 VFX)")]
    private GameObject warningBeamObject;

    [SerializeField, Tooltip("발사 단계 빔 시각 (두꺼운 메인 빔 VFX)")]
    private GameObject mainBeamObject;

    // =========================================================
    // 동작 옵션
    // =========================================================

    [Header("동작 옵션")]
    [SerializeField, Tooltip("씬 시작 시 자동 발동")]
    private bool autoStart = true;

    // =========================================================
    // 내부 상태
    // =========================================================

    private bool isRunning = false;

    // =========================================================
    // Unity 생명주기
    // =========================================================

    /// <summary>
    /// 초기 셋업 — 빔 둘 다 끄기 + originPoint 폴백
    /// </summary>
    private void Awake()
    {
        // originPoint 미설정 시 자기 자신 사용
        if (laserOriginPoint == null)
            laserOriginPoint = transform;

        // 시작 시 빔 모두 OFF
        if (warningBeamObject != null) warningBeamObject.SetActive(false);
        if (mainBeamObject != null) mainBeamObject.SetActive(false);
    }

    private void Start()
    {
        if (autoStart)
            StartTurret();
    }

    private void OnDisable()
    {
        StopTurret();
    }

    // =========================================================
    // Public API
    // =========================================================

    /// <summary>
    /// 포탑 가동 시작
    /// </summary>
    public void StartTurret()
    {
        if (isRunning) return;
        isRunning = true;
        StartCoroutine(TurretLoop());
    }

    /// <summary>
    /// 포탑 강제 정지 + 빔 즉시 끄기
    /// </summary>
    public void StopTurret()
    {
        isRunning = false;
        StopAllCoroutines();

        if (warningBeamObject != null) warningBeamObject.SetActive(false);
        if (mainBeamObject != null) mainBeamObject.SetActive(false);
    }

    // =========================================================
    // 메인 루프
    // =========================================================

    /// <summary>
    /// 포탑 무한 사이클 — 경고 → 발사 → 쿨다운 반복
    /// </summary>
    private IEnumerator TurretLoop()
    {
        // 초기 지연
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (isRunning)
        {
            yield return WarningPhase();
            yield return FirePhase();
            yield return new WaitForSeconds(cooldownDuration);
        }
    }

    // =========================================================
    // 단계별 처리
    // =========================================================

    /// <summary>
    /// 경고 단계 — 얇은 라인으로 발사 경로 미리 표시
    /// 플레이어가 빔 경로에서 빠질 시간을 줌
    /// </summary>
    private IEnumerator WarningPhase()
    {
        if (warningBeamObject != null)
            warningBeamObject.SetActive(true);

        yield return new WaitForSeconds(warningDuration);

        if (warningBeamObject != null)
            warningBeamObject.SetActive(false);
    }

    /// <summary>
    /// 발사 단계 — 메인 빔 표시 + 주기적 데미지 판정
    /// fireDuration 동안 damageInterval마다 OverlapBox로 영역 검사
    /// </summary>
    private IEnumerator FirePhase()
    {
        if (mainBeamObject != null)
            mainBeamObject.SetActive(true);

        float elapsed = 0f;
        float nextDamageTime = 0f;

        while (elapsed < fireDuration)
        {
            // 데미지 틱 타이밍
            if (elapsed >= nextDamageTime)
            {
                DealDamageInBeamArea();
                nextDamageTime = elapsed + damageInterval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 발사 종료 — 빔 끄기
        if (mainBeamObject != null)
            mainBeamObject.SetActive(false);
    }

    // =========================================================
    // 데미지 처리
    // =========================================================

    /// <summary>
    /// 빔 영역 안의 플레이어를 OverlapBox로 감지 → 데미지
    /// 박스 중심은 origin + forward × (length/2)
    /// 박스 크기는 (width, height, length)
    /// </summary>
    private void DealDamageInBeamArea()
    {
        // 빔 중심 위치 (포구에서 forward 방향으로 length/2 만큼)
        Vector3 forward = laserOriginPoint.forward;
        Vector3 center = laserOriginPoint.position + forward * (laserMaxLength / 2f);

        // 박스 크기 (half-extents = 절반 값)
        Vector3 halfExtents = new Vector3(
            laserWidth / 2f,
            laserHeight / 2f,
            laserMaxLength / 2f
        );

        // 박스 회전 = 포구 방향
        Quaternion orientation = laserOriginPoint.rotation;

        // 영역 안 플레이어 검색
        Collider[] hits = Physics.OverlapBox(center, halfExtents, orientation, playerLayer);

        foreach (var hit in hits)
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph != null && !ph.isDead)
            {
                ph.TakeDamage(damage);
                return; // 1마리만 (싱글 플레이어)
            }
        }
    }

    // =========================================================
    // 디버그 시각화
    // =========================================================

    /// <summary>
    /// 씬 뷰에서 빔 영역 시각화 (와이어 박스)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Transform origin = laserOriginPoint != null ? laserOriginPoint : transform;

        // 빔 박스 그리기
        Vector3 forward = origin.forward;
        Vector3 center = origin.position + forward * (laserMaxLength / 2f);
        Vector3 size = new Vector3(laserWidth, laserHeight, laserMaxLength);

        // Gizmos.matrix로 회전 적용
        Matrix4x4 prevMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, origin.rotation, Vector3.one);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Vector3.zero, size);

        Gizmos.matrix = prevMatrix;
    }
}
