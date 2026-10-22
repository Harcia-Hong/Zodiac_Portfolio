using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX; // VisualEffect 컴포넌트 사용 (VFX Graph 런타임 제어용)

/// <summary>
/// 검기(VFX Graph) 제어 컨트롤러
/// - 그래프는 수정하지 않고, 컴포넌트 단에서 Play/Stop만 제어
/// - 모드:
///   1) SwingTracked : 제자리/일반 스윙. '스윙이 실제로 발생'할 때만 재생
///   2) DirectionalBurst : 돌진/이동형. 시작 방향으로 1회 터뜨리고 끝
/// 
/// 참고 API:
/// - VisualEffect.Play(): VFX Graph 재생 시작
/// - VisualEffect.Stop(): VFX Graph 재생 정지 (Stop 후 그래프 설정에 따라 잔광이 자연 감쇠)
/// - Instantiate(): 프리팹(원본 에셋)을 런타임에 복제해 씬에 생성
/// - Quaternion.LookRotation(): 어떤 방향을 '전방'으로 하는 회전을 만들어줌
/// </summary>

public class SlashVFXController : MonoBehaviour
{
    // 공개 모드 //
    public enum Mode
    {
        SwingTracked,   // 지상 / 공중 등 제자리, 소폭 이동 공격
        DirectionalBurst  // Rush, Spin 등 방향성이 있는 공격 1회 재생
    };

    [Header("Reference")]
    [Tooltip("검 끝(팁) 위치, 보통 Weapon 하위에 연결")]
    public Transform swordTip;

    [Tooltip("플레이어 루트(또는 그립) Transform. 전역 이동 성분을 제거하기 위해 사용")]
    public Transform rootOrGrip;

    [Tooltip("SwingTracked용: SwordTip 밑에 자식으로 배치한 VisualEffect")]
    public VisualEffect attachedVFX;

    [Tooltip("DirectionalBurst용: 씬에 임시로 뿌릴 VFX 프리팹(에셋 원본 프리팹)")]
    public GameObject burstVFXPrefab;

    [Header("Swing Detection (게이트)")]
    [Tooltip("스윙으로 판단할 최소 상대 이동 거리(미터). 너무 작으면 이동/노이즈에도 켜질 수 있음")]
    public float distanceThreshold = 0.02f; // 2cm

    [Tooltip("스윙으로 판단할 최소 각속도(도). 회전 변화가 클 때만 켜도록 보조 게이트")]
    public float angleThresholdDeg = 3f;

    [Tooltip("한 번 켜졌으면 최소한 이 시간 동안은 유지하여 깜빡임 방지(초)")]
    public float minActiveTime = 0.08f;

    [Tooltip("방향 스냅시 수평면(XZ) 기준으로만 정렬할지 (대부분의 핵&슬래시 게임은 XZ 기준 권장)")]
    public bool snapOnXZ = true;

    [Tooltip("SwingTracked에서 방향 계산 시 최근 프레임의 상대 이동을 몇 개 평균낼지")]
    public int recentDirSampleCount = 3;

    [Header("DirectionalBurst Settings")]
    [Tooltip("DirectionalBurst 재생 후 임시 VFX를 파괴할 때까지의 시간(초). 에셋 감쇠 시간을 고려해서 설정")]
    public float burstLifetime = 0.5f;

    // --- 내부 상태 ---
    private bool _armed;                  // 공격 유효 프레임 내 여부 (힛박스 ON 구간)
    private Mode _mode;                   // 현재 모드
    private bool _isPlaying;              // attachedVFX가 현재 재생 중인지
    private float _lastPlayTime;          // 마지막 Play 시각(Time.time)
    private Vector3 _prevTipPos;
    private Vector3 _prevRootPos;
    private Quaternion _prevWeaponRot;

    // 최근 상대방향 누적(평균 추출용)
    private readonly Queue<Vector3> _recentRelDirs = new Queue<Vector3>();

    // --- 외부에서 부르는 API ---

    /// <summary>
    /// 공격 유효 프레임 시작. 
    /// SwingTracked: 스윙이 감지되면 Play.
    /// DirectionalBurst: 시작 방향으로 1회 인스턴스 재생.
    /// </summary>
    /// <param name="mode">모드 선택</param>
    /// <param name="fixedDirection">
    /// DirectionalBurst에서 사용할 '고정 방향'(월드). null이면 rootOrGrip.forward(수평 투영) 사용
    /// </param>
    public void Arm(Mode mode, Vector3? fixedDirection = null)
    {
        _mode = mode;
        _armed = true;
        _recentRelDirs.Clear();

        CachePreviousSamples(); // 프레임 간 비교를 위한 초기 샘플 저장

        if (mode == Mode.DirectionalBurst)
        {
            PlayDirectionalBurst(fixedDirection);
        }
        // SwingTracked는 여기서 바로 Play하지 않고,
        // LateUpdate에서 '스윙 게이트' 통과 시 Play.
    }

    /// <summary>
    /// 공격 유효 프레임 종료/캔슬. 즉시 Stop 및 상태 리셋.
    /// </summary>
    public void Disarm()
    {
        _armed = false;
        StopAttachedIfPlaying();
        _recentRelDirs.Clear();
    }

    // --- 유니티 라이프사이클 ---

    private void Awake()
    {
        // 레퍼런스 누락 방지용 경고 (런타임 크래시 회피)
        if (!swordTip) Debug.LogWarning("[SlashVFXController] SwordTip이 연결되지 않았습니다.", this);
        if (!rootOrGrip) Debug.LogWarning("[SlashVFXController] Root/Grip이 연결되지 않았습니다.", this);
        if (!attachedVFX) Debug.LogWarning("[SlashVFXController] attachedVFX가 연결되지 않았습니다. (SwingTracked 불가)", this);
        if (!burstVFXPrefab) Debug.LogWarning("[SlashVFXController] burstVFXPrefab이 연결되지 않았습니다. (DirectionalBurst 불가)", this);
    }

    private void OnDisable()
    {
        // 오브젝트 비활성/파괴 시 VFX가 남아 깜박이지 않도록 안전하게 정리
        StopAttachedIfPlaying();
    }

    private void LateUpdate()
    {
        // 애니메이션/물리 적용이 끝난 '프레임 최종 상태' 기준으로 계산하기 위해 LateUpdate에서 처리
        if (!_armed || _mode != Mode.SwingTracked || !attachedVFX || !swordTip || !rootOrGrip)
        {
            CachePreviousSamples();
            return;
        }

        // 1) 프레임간 이동 벡터 계산
        Vector3 curTip = swordTip.position;
        Vector3 curRoot = rootOrGrip.position;

        Vector3 deltaTip = curTip - _prevTipPos;     // 검 팁의 월드 이동
        Vector3 deltaRoot = curRoot - _prevRootPos;  // 루트의 월드 이동 (플레이어가 움직인 성분)
        Vector3 deltaRel = deltaTip - deltaRoot;     // '스윙으로 인한 상대 이동'만 분리

        // 2) 각속도(회전 변화량) 계산 (도)
        float angleDelta = 0f;
        if (rootOrGrip) // 무기 전체의 회전 변화를 대략 반영
        {
            Quaternion currentRot = rootOrGrip.rotation;
            angleDelta = Quaternion.Angle(_prevWeaponRot, currentRot); // 두 회전의 차이를 도 단위로 반환
            _prevWeaponRot = currentRot;
        }

        // 3) 게이트 판단 (히스테리시스 최소화: 단순 임계 + 최소 유지시간)
        bool swingByDistance = deltaRel.magnitude >= distanceThreshold;
        bool swingByAngle = angleDelta >= angleThresholdDeg;
        bool shouldPlay = swingByDistance || swingByAngle;

        if (shouldPlay)
        {
            // 최근 상대방향 누적(평균을 내어 방향 스냅의 안정성 확보)
            Vector3 dir = deltaRel;
            if (dir.sqrMagnitude > 0.000001f)
            {
                Vector3 unit = dir.normalized;
                EnqueueRecentDir(unit);
            }

            // 아직 재생 중이 아니면 시작
            if (!_isPlaying)
            {
                // 방향 스냅 후 로컬 회전 맞추기
                Vector3 snapped = GetSnappedDirection(GetAverageRecentDir(), snapOnXZ);
                if (snapped.sqrMagnitude > 0.000001f)
                {
                    // LookRotation: 전달한 벡터를 '전방(=Z+)'으로 하는 회전 생성
                    attachedVFX.transform.rotation = Quaternion.LookRotation(snapped, Vector3.up);
                }

                attachedVFX.Play(); // 그래프 수정 없이 Play 호출
                _isPlaying = true;
                _lastPlayTime = Time.time;
            }
        }
        else
        {
            // 스윙이 멈췄다면, 최소 유지시간이 지났을 때만 Stop하여 깜빡임 방지
            if (_isPlaying && (Time.time - _lastPlayTime) >= minActiveTime)
            {
                attachedVFX.Stop();
                _isPlaying = false;
                _recentRelDirs.Clear();
            }
        }

        // 4) 샘플 업데이트
        _prevTipPos = curTip;
        _prevRootPos = curRoot;
    }

    // --- 내부 유틸 ---

    private void StopAttachedIfPlaying()
    {
        if (attachedVFX && _isPlaying)
        {
            attachedVFX.Stop();
        }
        _isPlaying = false;
    }

    private void CachePreviousSamples()
    {
        if (swordTip) _prevTipPos = swordTip.position;
        if (rootOrGrip)
        {
            _prevRootPos = rootOrGrip.position;
            _prevWeaponRot = rootOrGrip.rotation;
        }
    }

    /// <summary>
    /// DirectionalBurst: 시작 순간의 '고정 방향'으로 임시 인스턴스를 월드에 뿌리고 파괴
    /// </summary>
    private void PlayDirectionalBurst(Vector3? fixedDirection)
    {
        if (!burstVFXPrefab || !swordTip) return;

        // 1) 생성 위치/회전 계산
        Vector3 pos = swordTip.position;

        Vector3 dir = fixedDirection ?? GetDefaultForwardOnXZ();
        if (dir.sqrMagnitude < 0.000001f) dir = Vector3.forward; // 안전장치
        dir = SnapDirection8(dir, snapOnXZ); // 수평/수직/대각 스냅

        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

        // 2) 프리팹 인스턴스 생성(월드에 독립 생성)
        // Instantiate: 게임 오브젝트를 런타임에 복제하여 씬에 배치하는 함수
        GameObject inst = Object.Instantiate(burstVFXPrefab, pos, rot);
        var vfx = inst.GetComponent<VisualEffect>();
        if (vfx) vfx.Play();

        // 3) 수명 후 파괴
        Object.Destroy(inst, Mathf.Max(0.01f, burstLifetime));
    }

    private void EnqueueRecentDir(Vector3 unitDir)
    {
        _recentRelDirs.Enqueue(unitDir);
        while (_recentRelDirs.Count > Mathf.Max(1, recentDirSampleCount))
            _recentRelDirs.Dequeue();
    }

    private Vector3 GetAverageRecentDir()
    {
        if (_recentRelDirs.Count == 0) return Vector3.zero;
        Vector3 sum = Vector3.zero;
        foreach (var d in _recentRelDirs) sum += d;
        return sum.normalized;
    }

    private Vector3 GetDefaultForwardOnXZ()
    {
        if (rootOrGrip)
        {
            Vector3 fwd = rootOrGrip.forward;
            if (snapOnXZ) fwd.y = 0f;
            return fwd.normalized;
        }
        return Vector3.forward;
    }

    /// <summary>
    /// 최근 평균 방향을 8방(좌/우/전/후 + 대각 4개) 중 가장 가까운 방향으로 스냅
    /// </summary>
    private Vector3 GetSnappedDirection(Vector3 avgDir, bool flatXZ)
    {
        if (avgDir.sqrMagnitude < 0.000001f) return Vector3.zero;
        return SnapDirection8(avgDir, flatXZ);
    }

    private static readonly Vector3[] _dirs8 =
    {
        new Vector3( 1, 0,  0).normalized,  // +X
        new Vector3(-1, 0,  0).normalized,  // -X
        new Vector3( 0, 0,  1).normalized,  // +Z
        new Vector3( 0, 0, -1).normalized,  // -Z
        new Vector3( 1, 0,  1).normalized,  // +X+Z
        new Vector3( 1, 0, -1).normalized,  // +X-Z
        new Vector3(-1, 0,  1).normalized,  // -X+Z
        new Vector3(-1, 0, -1).normalized,  // -X-Z
    };

    private static Vector3 SnapDirection8(Vector3 dir, bool flatXZ)
    {
        Vector3 d = dir;
        if (flatXZ) d.y = 0f;
        if (d.sqrMagnitude < 0.000001f) return Vector3.zero;
        d.Normalize();

        // 가장 dot가 큰(=가장 가까운) 8방 벡터 선택
        float best = -2f;
        Vector3 bestDir = Vector3.forward;
        for (int i = 0; i < _dirs8.Length; i++)
        {
            float dot = Vector3.Dot(d, _dirs8[i]);
            if (dot > best)
            {
                best = dot;
                bestDir = _dirs8[i];
            }
        }
        return bestDir;
    }

}
