using System.Collections;
using UnityEngine;

/// ============================================================
/// RotatingLaser.cs — 회전 십자 레이저 해저드 (Stage 1-2 1단계)
/// ============================================================
/// NodeStage에서 1번 노드 활성화 시 등장하는 첫 번째 환경 해저드.
///
/// [구조]
///   RotatingLaserHazard (부모 — 이 스크립트 부착, 회전 담당)
///      ├─ Beam_North (자식 — LaserBeamDamager 부착, Trigger Collider)
///      ├─ Beam_East
///      ├─ Beam_South
///      └─ Beam_West
///   부모만 회전 → 자식 빔 4개가 십자 형태 유지하며 함께 회전
///
/// [데미지 흐름]
///   자식 빔의 OnTriggerStay → 부모의 damage / damageInterval 참조 → 데미지 적용
///   (쿨다운으로 같은 플레이어 반복 데미지 제어)
///
/// [워밍업 옵션]
///   warmupDuration 동안 시각적으로 켜진 채 데미지는 비활성 →
///   플레이어가 위험 신호 인지할 시간 확보 (즉사 방지)
///
/// [한 파일 두 클래스]
///   - RotatingLaser     : 부모 GameObject용 (회전 + 데미지 값 관리)
///   - LaserBeamDamager  : 자식 빔용 (충돌 감지 + 데미지 처리)
/// ============================================================
public class RotatingLaser : MonoBehaviour
{
    // ============================================================
    // 인스펙터 설정 — 회전
    // ============================================================

    [Header("회전")]
    [SerializeField, Tooltip("초당 회전 각도 (deg/sec). 음수 = 반시계 방향")]
    private float rotateSpeed = 60f;

    [SerializeField, Tooltip("회전 축 (쿼터뷰는 보통 Y축 = 위쪽)")]
    private Vector3 rotateAxis = Vector3.up;

    // ============================================================
    // 인스펙터 설정 — 데미지
    // ============================================================

    [Header("데미지")]
    [SerializeField, Tooltip("레이저에 닿을 때 1회 데미지")]
    private int damage = 10;

    [SerializeField, Tooltip("같은 플레이어 반복 데미지 쿨다운 (초)")]
    private float damageInterval = 0.5f;

    // ============================================================
    // 인스펙터 설정 — 등장 연출
    // ============================================================

    [Header("등장 연출 (옵션)")]
    [SerializeField, Tooltip("활성화 후 데미지가 켜지기까지 대기 시간 " +
                             "(시각적으로 켜진 채 대기 → 위험 신호 인지 시간 확보)")]
    private float warmupDuration = 0.5f;

    // ============================================================
    // 외부 공개 — 자식 LaserBeamDamager가 참조
    // ============================================================

    /// <summary>플레이어에게 줄 데미지 (자식 빔이 참조)</summary>
    public int Damage => damage;

    /// <summary>같은 플레이어 반복 데미지 쿨다운 (자식 빔이 참조)</summary>
    public float DamageInterval => damageInterval;

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>현재 데미지가 활성화되어 있는지 (워밍업 중에는 false)</summary>
    private bool isDamageActive = false;

    /// <summary>워밍업 코루틴 참조 (중복 방지)</summary>
    private Coroutine warmupCoroutine;

    /// <summary>자식 LaserBeamDamager 캐싱 (Awake에서 1회)</summary>
    private LaserBeamDamager[] beamDamagers;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    /// <summary>자식 빔 데미저 캐싱</summary>
    private void Awake()
    {
        beamDamagers = GetComponentsInChildren<LaserBeamDamager>(includeInactive: true);

        if (beamDamagers == null || beamDamagers.Length == 0)
        {
            Debug.LogWarning($"[RotatingLaser] '{gameObject.name}': 자식 LaserBeamDamager가 없습니다. " +
                             "자식 빔 GameObject에 LaserBeamDamager를 부착하세요.");
        }
    }

    /// <summary>
    /// 활성화될 때마다 호출 — NodeStage가 SetActive(true)할 때 트리거.
    /// 워밍업 시작 (warmupDuration > 0일 때).
    /// </summary>
    private void OnEnable()
    {
        // 시작 시 데미지 비활성 (워밍업 끝나야 활성)
        SetDamageActive(false);

        if (warmupDuration > 0f)
        {
            warmupCoroutine = StartCoroutine(WarmupRoutine());
        }
        else
        {
            // 워밍업 없이 즉시 데미지 ON
            SetDamageActive(true);
        }
    }

    /// <summary>비활성화 시 코루틴 정리</summary>
    private void OnDisable()
    {
        if (warmupCoroutine != null)
        {
            StopCoroutine(warmupCoroutine);
            warmupCoroutine = null;
        }
    }

    /// <summary>매 프레임 부모 회전 → 자식 빔도 함께 회전</summary>
    private void Update()
    {
        transform.Rotate(rotateAxis, rotateSpeed * Time.deltaTime, Space.Self);
    }

    // ============================================================
    // 워밍업 — 시각적으로 켜진 채 데미지 OFF 시간
    // ============================================================

    /// <summary>warmupDuration 동안 데미지 비활성, 끝나면 활성</summary>
    private IEnumerator WarmupRoutine()
    {
        yield return new WaitForSeconds(warmupDuration);

        SetDamageActive(true);
        warmupCoroutine = null;
    }

    /// <summary>모든 자식 빔의 데미지 ON/OFF 일괄 토글</summary>
    private void SetDamageActive(bool active)
    {
        isDamageActive = active;

        if (beamDamagers == null) return;

        foreach (var damager in beamDamagers)
        {
            if (damager != null)
                damager.SetDamageEnabled(active);
        }
    }

    // ============================================================
    // 외부 인터페이스 — 디버그/특수 상황
    // ============================================================

    /// <summary>회전 속도 동적 변경 (단계별 강화 등에 사용 가능)</summary>
    public void SetRotateSpeed(float speed)
    {
        rotateSpeed = speed;
    }

    /// <summary>외부에서 즉시 데미지 ON/OFF (디버그/이벤트용)</summary>
    public void SetActiveImmediate(bool active)
    {
        if (warmupCoroutine != null)
        {
            StopCoroutine(warmupCoroutine);
            warmupCoroutine = null;
        }

        SetDamageActive(active);
    }
}
