using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 활 공격용 빔 효과 (Hitscan, 관통형).
/// 화살 대신 발사되는 즉시 타격 빔 — RaycastAll로 경로상 모든 적을 관통하며 데미지,
/// 벽을 만나면 거기서 멈추고 LineRenderer로 흰→빨강 빔을 그렸다가 페이드아웃.
/// 
/// 사용법:
///   외부에서 Fire(direction, damage, range, hitEffect) 호출
///   PlayerArrowAttack.FireArrow() 안에서 Instantiate 대신 이걸 호출.
/// 
/// 머티리얼 설정 전제:
///   - URP Particles/Unlit, Transparent + Additive, Emission HDR 켜짐
///   - URP Volume에 Bloom 활성화
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ArrowBeam : MonoBehaviour
{
    // ============================================================
    // Serialized Fields - Refs
    // ============================================================
    [Header("Refs")]
    [SerializeField, Tooltip("빔이 발사될 시작점. 비우면 이 오브젝트 자신의 transform 사용")]
    private Transform muzzle;

    [SerializeField, Tooltip("빔이 감지할 레이어 (Enemy + Wall 등 데미지/차폐 대상 모두 포함)")]
    private LayerMask hitMask = ~0;

    [SerializeField, Tooltip("이 레이어를 만나면 빔이 거기서 멈춤 (보통 Wall). 비워두면 적도 관통 후 무한 진행")]
    private LayerMask blockMask;

    // ============================================================
    // Serialized Fields - Visual
    // ============================================================
    [Header("Visual")]
    [SerializeField, Tooltip("빔이 보여지는 시간 (초)")]
    private float beamDuration = 0.12f;

    [SerializeField, Tooltip("발사 직후 빔 두께")]
    private float startWidth = 0.4f;

    [SerializeField, Tooltip("발사 직후 빔 색 — 머티리얼 Emission과 곱해짐")]
    [ColorUsage(true, true)]
    private Color startColor = Color.white;

    [SerializeField, Tooltip("페이드 끝 시점 빔 색 — 보통 빨강")]
    [ColorUsage(true, true)]
    private Color endColor = new Color(1f, 0.2f, 0.3f, 1f);

    // ============================================================
    // Internal State
    // ============================================================
    private LineRenderer lr;
    private Coroutine currentRoutine;

    // 한 발에서 같은 적을 여러 콜라이더로 중복 타격하지 않기 위한 집합
    // (멤버 변수로 재사용해 GC 압박 줄임)
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

    // ============================================================
    // Unity Lifecycle
    // ============================================================
    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.enabled = false;
        if (muzzle == null) muzzle = transform;
    }

    // ============================================================
    // Public Interface
    // ============================================================

    /// <summary>
    /// 빔 발사 — 관통 데미지 판정 + 시각 효과 모두 수행.
    /// PlayerArrowAttack 등 외부에서 이 메서드 하나만 호출하면 됨.
    /// </summary>
    /// <param name="direction">발사 방향</param>
    /// <param name="damage">관통 중 맞는 각 적에게 적용할 데미지</param>
    /// <param name="range">최대 사거리</param>
    /// <param name="hitEffect">적중 시 표시할 히트 이펙트 타입</param>
    public void Fire(Vector3 direction, int damage, float range, HitEffectType hitEffect)
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(FireRoutine(direction.normalized, damage, range, hitEffect));
    }

    // ============================================================
    // Core Routine
    // ============================================================

    /// <summary>
    /// 발사 1회 — RaycastAll로 경로상 모든 콜라이더 수집 후 거리순으로 처리.
    /// 적은 관통하며 데미지, 벽(blockMask) 만나면 빔이 거기서 끊김.
    /// </summary>
    private IEnumerator FireRoutine(Vector3 dir, int damage, float range, HitEffectType hitEffect)
    {
        Vector3 start = muzzle.position;
        Vector3 end = start + dir * range;

        // ---- 관통 데미지 판정 ----
        hitTargets.Clear();

        RaycastHit[] hits = Physics.RaycastAll(
            start, dir, range, hitMask, QueryTriggerInteraction.Collide);

        // 가까운 순으로 정렬 — 벽이 가까이 있으면 그 뒤 적은 안 맞아야 함
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        float beamEndDistance = range;

        foreach (var hit in hits)
        {
            // 벽 등 차폐 레이어 만나면 거기서 빔 끝남
            if (IsBlocker(hit.collider))
            {
                beamEndDistance = hit.distance;
                break;
            }

            // 적/파괴오브젝트에 데미지 (중복 방지)
            IDamageable damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead && hitTargets.Add(damageable))
            {
                damageable.ApplySkillDamage(damage, hitEffect);
            }
        }

        end = start + dir * beamEndDistance;

        // ---- 시각 효과 ----
        lr.enabled = true;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        float t = 0f;
        while (t < beamDuration)
        {
            t += Time.deltaTime;
            float k = t / beamDuration;

            // 색: 흰 → 빨강 / 두께: startWidth → 0
            Color c = Color.Lerp(startColor, endColor, k);
            float w = Mathf.Lerp(startWidth, 0f, k);

            lr.startColor = c;
            lr.endColor = c;
            lr.startWidth = w;
            lr.endWidth = w;

            yield return null;
        }

        lr.enabled = false;
        currentRoutine = null;
    }

    /// <summary>blockMask에 포함된 레이어인지 — 빔이 멈춰야 할 차폐 콜라이더 판정</summary>
    private bool IsBlocker(Collider col)
    {
        return (blockMask.value & (1 << col.gameObject.layer)) != 0;
    }
}
