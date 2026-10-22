/// ============================================================
using System.Collections;
using UnityEngine;

/// LaserBeamDamager — 레이저 빔 1개의 데미지 처리 (자식 GameObject용)
/// ============================================================
/// RotatingLaser의 자식 빔(Cube/Cylinder + Trigger Collider)에 부착.
/// 부모 RotatingLaser의 damage / damageInterval 값을 참조해서 데미지 적용.
///
/// [충돌 감지]
///   OnTriggerStay 사용 — 플레이어가 빔 안에 머무는 동안 쿨다운마다 반복 데미지.
///   (회피 중 무적 처리는 PlayerHealth.TakeDamage 내부에서 자체 처리한다고 가정)
///
/// [필수 세팅]
///   - GameObject에 Trigger Collider (BoxCollider 등) 필요
///   - Player는 Rigidbody 보유 (OnTriggerStay 호출 조건)
///   - 부모에 RotatingLaser가 있어야 데미지 값 가져옴
/// ============================================================
public class LaserBeamDamager : MonoBehaviour
{
    [Header("연결 — 비워두면 부모에서 자동 탐색")]
    [SerializeField, Tooltip("이 빔이 속한 부모 RotatingLaser")]
    private RotatingLaser parentLaser;

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>데미지 활성 여부 (부모가 워밍업 중에는 false)</summary>
    private bool damageEnabled = true;

    /// <summary>마지막으로 데미지를 입힌 시각 (쿨다운 계산)</summary>
    private float lastDamageTime = -999f;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    /// <summary>부모 RotatingLaser 자동 탐색</summary>
    private void Awake()
    {
        if (parentLaser == null)
            parentLaser = GetComponentInParent<RotatingLaser>();

        if (parentLaser == null)
        {
            Debug.LogError($"[LaserBeamDamager] '{gameObject.name}': 부모 RotatingLaser를 찾을 수 없습니다.");
            enabled = false;
        }
    }

    /// <summary>
    /// 플레이어가 빔 안에 머무는 동안 매 물리 프레임 호출.
    /// 쿨다운 충족 시 데미지 적용.
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        if (!damageEnabled) return;
        if (parentLaser == null) return;
        if (!other.CompareTag("Player")) return;

        // 쿨다운 체크
        if (Time.time - lastDamageTime < parentLaser.DamageInterval) return;

        // PlayerHealth 컴포넌트 조회 + 데미지 적용
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null) return;

        playerHealth.TakeDamage(parentLaser.Damage);
        lastDamageTime = Time.time;
    }

    // ============================================================
    // 외부 인터페이스 — 부모가 호출
    // ============================================================

    /// <summary>
    /// 데미지 활성/비활성 토글 (부모 RotatingLaser가 워밍업 중에 OFF로 호출).
    /// 시각적인 빔은 그대로 표시되지만 데미지는 들어가지 않음.
    /// </summary>
    public void SetDamageEnabled(bool enabled)
    {
        damageEnabled = enabled;
    }
}
