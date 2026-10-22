// Enemy_Suicide_Attack.cs — 자폭 공격 (Kill Knight Zeeker, Windblown Bomber 스타일)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자폭 공격 컴포넌트
///
/// [패턴]
/// 1. 평소엔 빠르게 플레이어 추적 (FSM의 ChasingState)
/// 2. 트리거 거리 진입 → StartAttack 호출
/// 3. NavMesh 정지 + 빨간 점멸 시작 (점점 빨라짐 — 긴장감)
/// 4. AttackTelegraph(Circle)로 폭발 범위 미리 표시
/// 5. 카운트다운 끝 → OverlapSphere로 데미지 + 폭발 VFX 생성
/// 6. 자기 자신 즉시 사망 처리
///
/// [전략성]
/// - HP 낮음 → 닿기 전에 처치 가능 (그러면 폭발 안 함)
/// - StopAttack으로 카운트다운 중 죽으면 폭발 취소
///
/// [참고 게임]
/// - Kill Knight Zeeker, Windblown Bomber
///
/// [의존성]
/// - EnemyAttackBase (player/nav/anim/enemy 자동 캐싱)
/// - AttackTelegraph (Circle 타입 사용)
/// - Enemy 컴포넌트 (TakeDamage 호출로 사망 처리)
/// </summary>
public class Enemy_Suicide_Attack : EnemyAttackBase
{
    // =========================================================
    // 트리거 / 폭발 설정
    // =========================================================

    [Header("트리거 / 폭발 설정")]
    [SerializeField, Tooltip("이 거리 이내로 플레이어가 들어오면 카운트다운 시작")]
    private float triggerRange = 1.5f;

    [SerializeField, Tooltip("폭발 반경 — 이 안에 있는 플레이어는 데미지")]
    private float explosionRadius = 3f;

    [SerializeField, Tooltip("폭발 데미지")]
    private int explosionDamage = 35;

    [SerializeField, Tooltip("카운트다운 시간 (초) — 회피 가능 시간")]
    private float countdownDuration = 1.0f;

    // =========================================================
    // 점멸 효과
    // =========================================================

    [Header("점멸 효과 (긴장감)")]
    [SerializeField, Tooltip("점멸 시 사용할 색상 (보통 빨강)")]
    private Color blinkColor = Color.red;

    [SerializeField, Tooltip("점멸 시작 시 한 번 깜빡이는 데 걸리는 시간 (느린 시작)")]
    private float blinkInitialInterval = 0.25f;

    [SerializeField, Tooltip("점멸 끝날 때 한 번 깜빡이는 데 걸리는 시간 (빠른 끝)")]
    private float blinkFinalInterval = 0.05f;

    // =========================================================
    // VFX
    // =========================================================

    [Header("폭발 VFX")]
    [SerializeField, Tooltip("폭발 시 생성할 VFX prefab (없어도 동작은 함)")]
    private GameObject explosionVFXPrefab;

    [SerializeField, Tooltip("폭발 VFX 자동 파괴까지 시간")]
    private float vfxLifetime = 2f;

    // =========================================================
    // 레이어
    // =========================================================

    [Header("레이어")]
    [SerializeField, Tooltip("플레이어 레이어 (폭발 데미지 대상)")]
    private LayerMask playerLayer;

    // =========================================================
    // 컴포넌트 캐싱
    // =========================================================

    /// <summary>Telegraph 컴포넌트 (자폭 전용 단순 버전)</summary>
    private SuicideTelegraph telegraph;

    /// <summary>모델의 모든 렌더러 (점멸용) — Awake에서 캐싱</summary>
    private Renderer[] renderers;

    /// <summary>각 렌더러의 원본 색상 (점멸 후 복원용)</summary>
    private List<Color> originalColors = new List<Color>();

    /// <summary>각 렌더러의 인스턴스화된 머티리얼 (원본 보호)</summary>
    private List<Material> instanceMaterials = new List<Material>();

    // =========================================================
    // 초기화
    // =========================================================

    /// <summary>
    /// 컴포넌트 캐싱 + 점멸용 머티리얼 인스턴스화
    /// base.Awake()에서 player/nav/anim/enemy 자동 캐싱
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        telegraph = GetComponent<SuicideTelegraph>();

        // 모든 자식 렌더러 캐싱 (점멸 효과용)
        // Renderer를 사용하면 SkinnedMeshRenderer/MeshRenderer 모두 자동 포함됨
        renderers = GetComponentsInChildren<Renderer>(true);

        // 머티리얼 인스턴스화 + 원본 색상 기억
        // material 프로퍼티는 자동으로 인스턴스를 만들어 반환 (원본 에셋 보호)
        foreach (var r in renderers)
        {
            if (r.material != null)
            {
                instanceMaterials.Add(r.material);
                // _Color 프로퍼티가 없는 셰이더도 있으니 안전하게 처리
                Color baseColor = r.material.HasProperty("_Color") ? r.material.color : Color.white;
                originalColors.Add(baseColor);
            }
        }

        if (telegraph == null)
            Debug.LogWarning($"[{name}] AttackTelegraph 컴포넌트가 없습니다.");
    }

    // =========================================================
    // IEnemyAttack — abstract 오버라이드
    // =========================================================

    /// <summary>
    /// 공격 시작 — FSM에서 호출 (트리거 거리 진입 시)
    /// 카운트다운 시퀀스 시작
    /// </summary>
    public override void StartAttack()
    {
        if (isAttacking || !gameObject.activeInHierarchy) return;
        if (!IsPlayerValid()) return;

        StartCoroutine(SuicideRoutine());
    }

    /// <summary>공격 사거리 = 트리거 거리</summary>
    public override float GetAttackRange() => triggerRange;

    /// <summary>쿨타임 — 자폭은 1회용이라 의미 없지만 인터페이스 준수</summary>
    public override float GetCooldownTime() => 0f;

    /// <summary>
    /// 공격 강제 종료 — 카운트다운 중 죽으면 호출됨
    /// 점멸 색상 복원 + Telegraph 취소 (폭발은 발생 안 함 — 전략성)
    /// </summary>
    public override void StopAttack()
    {
        // 점멸로 변경된 색상 원복
        RestoreOriginalColors();

        // Telegraph 진행 중이면 취소
        if (telegraph != null)
            telegraph.Cancel();

        base.StopAttack();
    }

    // =========================================================
    // 자폭 시퀀스
    // =========================================================

    /// <summary>
    /// 자폭 메인 루틴
    ///
    /// [3단계]
    /// Phase 1: 정지 + Telegraph + 점멸 카운트다운
    /// Phase 2: 폭발 (OverlapSphere 데미지 + VFX)
    /// Phase 3: 자기 사망 처리
    /// </summary>
    private IEnumerator SuicideRoutine()
    {
        isAttacking = true;

        // ============= Phase 1: 정지 + Telegraph + 점멸 =============

        // 1-1. NavMesh 정지 (제자리에서 카운트다운)
        DisableNavMesh();

        // 1-2. Telegraph 표시 (폭발 범위 = 카운트다운 시간 동안 차오름)
        if (telegraph != null)
        {
            telegraph.Show(transform.position, explosionRadius, countdownDuration);
        }

        // 1-3. 점멸 시작 (countdownDuration 동안 점점 빨라지는 깜빡임)
        // 점멸 코루틴은 Phase 2 진입 시 자동 종료됨
        yield return StartCoroutine(BlinkRoutine(countdownDuration));

        // 취소 체크 (StopAttack으로 끊겼을 경우)
        if (!isAttacking) yield break;

        // ============= Phase 2: 폭발 =============

        Explode();

        // ============= Phase 3: 자기 사망 처리 =============

        // Enemy의 TakeDamage로 자살 처리 → HandleDeath가 알아서 정리
        // (Die 애니, Collider/NavMesh 비활성화, Destroy 등)
        IDamageable damageable = enemy as IDamageable;
        damageable?.TakeDamage(99999);

        isAttacking = false;
    }

    /// <summary>
    /// 점멸 효과 — 시간이 지날수록 점점 빨라짐 (긴장감)
    /// 시작은 blinkInitialInterval, 끝은 blinkFinalInterval로 보간
    /// </summary>
    /// <param name="duration">총 점멸 시간 (= 카운트다운 시간)</param>
    private IEnumerator BlinkRoutine(float duration)
    {
        float elapsed = 0f;
        bool isRed = false;

        while (elapsed < duration)
        {
            // 진행도(0~1)에 따라 점멸 간격을 보간 (시간 갈수록 빨라짐)
            float t = elapsed / duration;
            float currentInterval = Mathf.Lerp(blinkInitialInterval, blinkFinalInterval, t);

            // 색상 토글
            isRed = !isRed;
            ApplyBlinkColor(isRed);

            yield return new WaitForSeconds(currentInterval);
            elapsed += currentInterval;
        }

        // 점멸 종료 시 마지막엔 빨강 상태로 마무리 (폭발 직전 강조)
        ApplyBlinkColor(true);
    }

    /// <summary>
    /// 모든 인스턴스 머티리얼에 점멸 색상 적용
    /// </summary>
    /// <param name="useBlinkColor">true: 빨간 점멸 색상 / false: 원본 색상</param>
    private void ApplyBlinkColor(bool useBlinkColor)
    {
        for (int i = 0; i < instanceMaterials.Count; i++)
        {
            Material mat = instanceMaterials[i];
            if (mat == null || !mat.HasProperty("_Color")) continue;

            mat.color = useBlinkColor ? blinkColor : originalColors[i];
        }
    }

    /// <summary>
    /// 원본 색상으로 즉시 복원 (StopAttack 등에서 안전 정리용)
    /// </summary>
    private void RestoreOriginalColors()
    {
        for (int i = 0; i < instanceMaterials.Count; i++)
        {
            Material mat = instanceMaterials[i];
            if (mat == null || !mat.HasProperty("_Color")) continue;

            mat.color = originalColors[i];
        }
    }

    // =========================================================
    // 폭발 처리
    // =========================================================

    /// <summary>
    /// 폭발 실행 — 데미지 판정 + VFX 생성
    /// </summary>
    private void Explode()
    {
        // 1. OverlapSphere로 폭발 반경 안의 플레이어 검출
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, playerLayer);

        foreach (var hit in hits)
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph != null && !ph.isDead)
            {
                ph.TakeDamage(explosionDamage);
                break; // 한 명만 (실제로 플레이어는 1명이지만 안전장치)
            }
        }

        // 2. 폭발 VFX 생성 (있을 때만)
        if (explosionVFXPrefab != null)
        {
            GameObject vfx = Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, vfxLifetime);
        }
    }

    // =========================================================
    // 디버그 (씬 뷰 시각화)
    // =========================================================

    /// <summary>
    /// 씬 뷰에서 트리거 거리 / 폭발 반경 시각화
    /// 노랑: 트리거 거리 / 빨강: 폭발 반경
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 트리거 거리 (이 안 들어오면 자폭 시작)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerRange);

        // 폭발 반경
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
