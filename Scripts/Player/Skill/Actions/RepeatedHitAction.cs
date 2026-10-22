using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 반복 타격 액션 — 캐릭터 전방 한 지점에서 N회 데미지 판정 + 이펙트.
/// 
/// 동작:
///   1) 마우스 방향으로 회전
///   2) 애니메이션 트리거 (속도 조절 가능)
///   3) N회 반복: 캐릭터 전방 지점에 OverlapSphere 판정 → 데미지 + 이펙트
///   4) 각 판정 사이 hitInterval 대기
/// 
/// 활용:
///   - 연속 찌르기 (찌르기 모션 + N회 데미지/이펙트)
///   - 빠른 난타 류 스킬
/// 
/// 강화 확장 자리:
///   - 타격 횟수 / 간격 / 범위 (인스펙터)
///   - 데미지 (damageOverride)
/// </summary>

/// <summary>RepeatedHitAction의 캐릭터 회전 방향 모드.</summary>
public enum RotateMode
{
    /// <summary>마우스 커서 방향.</summary>
    Mouse,
    /// <summary>주변 가장 가까운 적 방향. 없으면 현재 forward 유지.</summary>
    NearestEnemy,
    /// <summary>회전 안 함 (현재 forward 유지).</summary>
    None
}


[CreateAssetMenu(menuName = "Skill/Action/Repeated Hit")]
public class RepeatedHitAction : SkillAction
{
    [Header("회전 모드")]
    [Tooltip("시전 시 캐릭터가 향할 방향. Mouse=마우스 방향 / NearestEnemy=가장 가까운 적 / None=회전 안 함.")]
    public RotateMode rotateMode = RotateMode.Mouse;

    [Tooltip("NearestEnemy 모드일 때 적 탐색 반경 (m).")]
    public float enemySearchRadius = 8f;

    [Header("판정 위치 (캐릭터 기준)")]
    [Tooltip("캐릭터 전방으로 떨어진 판정 지점 거리 (m).")]
    public float forwardOffset = 1.5f;

    [Tooltip("판정 반경 (m).")]
    public float hitRadius = 1.5f;

    [Tooltip("적 레이어.")]
    public LayerMask enemyLayer = -1;

    [Header("반복 횟수")]
    [Tooltip("타격 횟수.")]
    public int hitCount = 4;

    [Tooltip("각 판정 사이 간격 (초). 짧을수록 빠른 연타.")]
    public float hitInterval = 0.1f;

    [Tooltip("첫 판정까지 초기 지연 (초). 애니메이션 임팩트 프레임 맞추는 용도.")]
    public float startDelay = 0.1f;

    [Header("애니메이션")]
    [Tooltip("Animator 트리거 이름. 비우면 모션 없음.")]
    public string animTrigger = "doStab";

    [Tooltip("애니메이션 재생 속도. 2~3 = 빠른 찌르기 느낌.")]
    public float animSpeed = 2f;

    [Header("이펙트")]
    [Tooltip("각 타격 시 생성될 이펙트 프리팹. 없어도 동작.")]
    public GameObject hitEffectPrefab;

    [Tooltip("이펙트 자동 제거 시간 (초).")]
    public float effectDuration = 1.5f;

    [Header("히트 처리")]
    [Tooltip("적중 시 표시할 히트 이펙트 타입.")]
    public HitEffectType hitEffect = HitEffectType.Slashing_Normal;

    [Header("사운드")]
    [Tooltip("시전 시작 시 1회 재생.")]
    public AudioClip castSfx;

    [Tooltip("각 타격마다 재생.")]
    public AudioClip hitSfx;

    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        PlayerController pc = user.GetComponent<PlayerController>();
        if (pc == null)
        {
            Debug.LogError("[RepeatedHitAction] PlayerController 없음");
            yield break;
        }

        // 회전 방향 결정 (모드별)
        Vector3 origin = user.transform.position;
        Vector3 direction = ResolveRotateDirection(user, pc, origin);

        // 캐릭터 회전 (None 모드면 회전 안 함)
        if (rotateMode != RotateMode.None && direction != Vector3.zero)
            user.transform.rotation = Quaternion.LookRotation(direction);

        // 애니메이션 트리거 + 속도 조절
        Animator anim = user.GetComponentInChildren<Animator>();
        if (anim != null && !string.IsNullOrEmpty(animTrigger))
        {
            anim.speed = animSpeed;
            anim.SetTrigger(animTrigger);
        }

        // 시전 사운드
        PlaySfx(castSfx);

        // 초기 지연 (애니메이션 임팩트 프레임까지)
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        // 데미지 결정 (1회당)
        int damagePerHit = (damageOverride > 0) ? Mathf.RoundToInt(damageOverride) : 30;

        // N회 반복 판정
        for (int i = 0; i < hitCount; i++)
        {
            // 판정 위치 = 캐릭터 전방 (찌를 때마다 새로 계산 — 캐릭터가 약간 이동/회전해도 따라감)
            Vector3 hitPos = user.transform.position + user.transform.forward * forwardOffset;

            DealDamage(hitPos, damagePerHit);
            SpawnHitEffect(hitPos, user.transform.forward);
            PlaySfx(hitSfx);

            // 마지막이 아니면 간격 대기
            if (i < hitCount - 1)
                yield return new WaitForSeconds(hitInterval);
        }

        // 애니메이션 속도 복원
        if (anim != null) anim.speed = 1f;
    }

    /// <summary>회전 모드에 따른 방향 벡터 반환. NearestEnemy 모드에서 적이 없으면 forward 폴백.</summary>
    private Vector3 ResolveRotateDirection(GameObject user, PlayerController pc, Vector3 origin)
    {
        switch (rotateMode)
        {
            case RotateMode.Mouse:
                Vector3 mouseWorldPos = pc.GetMouseWorldPosition();
                Vector3 toMouse = new Vector3(mouseWorldPos.x, origin.y, mouseWorldPos.z) - origin;
                return toMouse.sqrMagnitude > 0.001f ? toMouse.normalized : user.transform.forward;

            case RotateMode.NearestEnemy:
                Vector3 nearest = FindNearestEnemyDirection(origin, enemySearchRadius);
                return nearest != Vector3.zero ? nearest : user.transform.forward;

            case RotateMode.None:
            default:
                return user.transform.forward;
        }
    }

    /// <summary>지정 반경 안에서 가장 가까운 적 방향 반환 (XZ 평면). 없으면 Vector3.zero.</summary>
    private Vector3 FindNearestEnemyDirection(Vector3 origin, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(origin, radius, enemyLayer);

        float minSqrDist = float.MaxValue;
        Vector3 nearestDir = Vector3.zero;

        foreach (var hit in hits)
        {
            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable == null || damageable.IsDead) continue;

            float sqrDist = (hit.transform.position - origin).sqrMagnitude;
            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist;
                Vector3 dir = hit.transform.position - origin;
                dir.y = 0f;
                nearestDir = dir.normalized;
            }
        }

        return nearestDir;
    }

    /// <summary>한 지점에서 OverlapSphere로 데미지 판정. 1회 판정 내 같은 적 중복 방지.</summary>
    private void DealDamage(Vector3 pos, int damage)
    {
        Collider[] hits = Physics.OverlapSphere(pos, hitRadius, enemyLayer);
        HashSet<GameObject> hitTargets = new HashSet<GameObject>();

        foreach (var hit in hits)
        {
            GameObject obj = hit.gameObject;
            if (hitTargets.Contains(obj)) continue;
            hitTargets.Add(obj);

            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.ApplySkillDamage(damage, hitEffect);
            }
        }
    }

    private void SpawnHitEffect(Vector3 pos, Vector3 forward)
    {
        if (hitEffectPrefab == null) return;
        Quaternion rot = (forward != Vector3.zero) ? Quaternion.LookRotation(forward) : Quaternion.identity;
        GameObject effect = Instantiate(hitEffectPrefab, pos, rot);
        Destroy(effect, effectDuration);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(clip);
    }
}
