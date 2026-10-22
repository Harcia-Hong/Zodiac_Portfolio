using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// 얼음 기둥 — 땅에서 솟구치며 주변 적에게 데미지 + 둔화.
/// 
/// 동작:
///   1) Awake: 스폰 위치를 최종 위치로 기억, 자기 자신은 땅 아래로 이동
///   2) Start: riseDuration 동안 위로 솟구침 (DOTween)
///   3) 솟구침 완료 시 OverlapSphere로 주변 적 판정 → 데미지 + 둔화
///   4) stayDuration 후 자동 소멸
/// 
/// 외부 호출:
///   Initialize(damage) — LineSpawnAction에서 호출. damage를 동적으로 설정.
/// 
/// 강화 확장 자리 (불씨로 비틀 가능):
///   - 데미지/범위/솟구침 속도/잔존 (인스펙터)
///   - 빙결/폭발/연쇄 효과 (메서드 추가)
/// </summary>
public class IcePillar : MonoBehaviour
{
    [Header("솟구침 타이밍")]
    [SerializeField, Tooltip("솟구침 시작 시 땅 아래로 묻히는 깊이 (m). 메시 높이만큼 권장.")]
    private float riseHeight = 2f;

    [SerializeField, Tooltip("솟구침 완료까지 걸리는 시간 (초). 짧을수록 임팩트.")]
    private float riseDuration = 0.2f;

    [SerializeField, Tooltip("솟구침 완료 후 잔존 시간 (초). 잔존 동안 시각만 남고 데미지는 1회로 끝.")]
    private float stayDuration = 1.0f;

    [Header("데미지 판정")]
    [SerializeField, Tooltip("데미지 기본값. Initialize로 덮어쓰기 가능 (SkillData damageOverride).")]
    private int damage = 50;

    [SerializeField, Tooltip("데미지 판정 반경 (m).")]
    private float damageRadius = 1.5f;

    [SerializeField, Tooltip("적 레이어.")]
    private LayerMask enemyLayer = -1;

    [SerializeField, Tooltip("적중 시 표시할 히트 이펙트 타입.")]
    private HitEffectType hitEffect = HitEffectType.Slashing_Normal;

    [Header("둔화 효과")]
    [SerializeField, Tooltip("둔화 적용 여부.")]
    private bool applySlow = true;

    [SerializeField, Range(0.1f, 1f), Tooltip("이동속도 배율. 0.5 = 50% 둔화.")]
    private float slowMultiplier = 0.5f;

    [SerializeField, Tooltip("둔화 지속시간 (초).")]
    private float slowDuration = 2f;

    // 런타임 상태
    private Vector3 finalPosition;
    private bool hasDealtDamage = false;

    // ============================================================
    // Public API
    // ============================================================

    /// <summary>외부에서 데미지 값을 동적으로 설정. LineSpawnAction에서 SkillData 기반으로 호출.</summary>
    public void Initialize(int dmg)
    {
        damage = dmg;
    }

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    private void Awake()
    {
        // 스폰된 위치 = 최종 솟구침 위치로 기억
        finalPosition = transform.position;

        // 자기 자신은 일단 땅 아래로 묻어둠 (솟구침 효과용)
        transform.position = finalPosition - Vector3.up * riseHeight;
    }

    private void Start()
    {
        // 솟구침 트윈 — Y만 이동
        transform.DOMoveY(finalPosition.y, riseDuration)
            .SetEase(Ease.OutCubic)
            .OnComplete(OnRiseComplete);
    }

    private void OnDestroy()
    {
        // DOTween 정리 (메모리 누수 방지)
        transform.DOKill();
    }

    // ============================================================
    // 솟구침 완료 후 처리
    // ============================================================

    /// <summary>솟구침 완료 시 호출 — 1회 데미지 판정 + 잔존 후 소멸.</summary>
    private void OnRiseComplete()
    {
        DealDamage();
        Destroy(gameObject, stayDuration);
    }

    /// <summary>주변 적 OverlapSphere 판정 → 데미지 + 둔화 적용.</summary>
    private void DealDamage()
    {
        if (hasDealtDamage) return;
        hasDealtDamage = true;

        Collider[] hits = Physics.OverlapSphere(transform.position, damageRadius, enemyLayer);
        HashSet<GameObject> hitTargets = new HashSet<GameObject>(); // 같은 적 중복 방지

        foreach (var hit in hits)
        {
            GameObject targetObj = hit.gameObject;
            if (hitTargets.Contains(targetObj)) continue;
            hitTargets.Add(targetObj);

            // 데미지
            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.ApplySkillDamage(damage, hitEffect);

                // 둔화 (Enemy에 둔화 메서드가 있다고 가정 — 없으면 컴파일 에러로 짚힘)
                if (applySlow)
                {
                    Enemy enemy = hit.GetComponent<Enemy>();
                    if (enemy != null)
                    {
                        // TODO: Enemy.ApplySlow 메서드 확인. 없으면 추가 작업 필요.
                        // enemy.ApplySlow(slowMultiplier, slowDuration);
                    }
                }
            }
        }
    }

    // ============================================================
    // Debug
    // ============================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1.0f, 0.5f); // 얼음 톤
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}
