using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 방사형 투사체 공격
/// - 변신 → 투사체 웨이브 발사 (점점 증가 + 홀짝 교차) → 복귀
/// - BossPatternExecutor에서 커스텀 패턴으로 호출
/// </summary>
public class BossRadialAttack : MonoBehaviour
{
    [Header("웨이브 설정")]
    [SerializeField, Tooltip("총 발사 웨이브 수")]
    private int totalWaves = 5;

    [SerializeField, Tooltip("첫 웨이브 투사체 수")]
    private int startProjectileCount = 3;

    [SerializeField, Tooltip("웨이브당 투사체 증가량")]
    private int countIncreasePerWave = 2;

    [SerializeField, Tooltip("웨이브 사이 간격 (초)")]
    private float waveCooldown = 1.0f;

    [Header("투사체 설정")]
    [SerializeField, Tooltip("투사체 프리팹")]
    private GameObject projectilePrefab;

    [SerializeField, Tooltip("투사체 초기 속도 (느리게 시작)")]
    private float projectileStartSpeed = 2f;

    [SerializeField, Tooltip("투사체 최종 속도 (점점 빨라짐)")]
    private float projectileMaxSpeed = 12f;

    [SerializeField, Tooltip("최종 속도 도달까지 시간 (초)")]
    private float accelerationTime = 1.0f;

    [SerializeField, Tooltip("투사체 데미지")]
    private int projectileDamage = 15;

    [SerializeField, Tooltip("투사체 수명 (초)")]
    private float projectileLifetime = 6f;

    [SerializeField, Tooltip("투사체 생성 높이")]
    private float spawnHeight = 1.0f;

    [Header("교차 패턴")]
    [SerializeField, Tooltip("짝수 웨이브 각도 오프셋 (도)")]
    private float evenWaveAngleOffset = 15f;

    [Header("애니메이션")]
    [SerializeField, Tooltip("변신 진입 후 대기 시간 (초)")]
    private float hideEnterDuration = 0.5f;

    [SerializeField, Tooltip("마지막 웨이브 후 복귀까지 대기 (초)")]
    private float endDelay = 0.5f;

    // 컴포넌트
    private Animator anim;

    // 상태
    private bool isExecuting = false;
    private Coroutine attackCoroutine;

    /// <summary>현재 실행 중인지</summary>
    public bool IsExecuting => isExecuting;

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
    }

    #region 외부 인터페이스

    /// <summary>방사형 공격 실행</summary>
    public void Execute()
    {
        if (!isExecuting && gameObject.activeInHierarchy)
        {
            attackCoroutine = StartCoroutine(RadialAttackRoutine());
        }
    }

    /// <summary>강제 중단</summary>
    public void Cancel()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        // 변신 해제
        if (anim != null)
            anim.SetBool("isPattern", false);

        isExecuting = false;
    }

    #endregion

    #region 메인 루틴

    /// <summary>
    /// 변신 → 웨이브 발사 → 복귀
    /// </summary>
    private IEnumerator RadialAttackRoutine()
    {
        isExecuting = true;

        // 1. 변신 ON (Bool로 유지)
        if (anim != null)
            anim.SetBool("isPattern", true);

        yield return new WaitForSeconds(hideEnterDuration);

        // 2. 변신 완료 후 발사 전 대기 (추가)
        yield return new WaitForSeconds(0.4f);

        // 3. 웨이브 반복 발사
        for (int wave = 0; wave < totalWaves; wave++)
        {
            int count = startProjectileCount + (countIncreasePerWave * wave);
            float angleOffset = (wave % 2 == 1) ? evenWaveAngleOffset : 0f;

            FireWave(count, angleOffset);

            Debug.Log($"[ThornBarrage] 웨이브 {wave + 1}/{totalWaves} — {count}발");

            if (wave < totalWaves - 1)
                yield return new WaitForSeconds(waveCooldown);
        }

        // 4. 마지막 웨이브 후 잠깐 대기
        yield return new WaitForSeconds(endDelay);

        // 5. 변신 OFF → Idle 복귀
        if (anim != null)
            anim.SetBool("isPattern", false);

        isExecuting = false;

        Debug.Log("[ThornBarrage] 패턴 완료");
    }

    #endregion

    #region 투사체 발사

    /// <summary>단일 웨이브 — 균등 각도로 투사체 생성</summary>
    private void FireWave(int count, float angleOffset)
    {
        if (projectilePrefab == null) return;

        float angleStep = 360f / count;
        Vector3 spawnPos = transform.position + Vector3.up * spawnHeight;

        for (int i = 0; i < count; i++)
        {
            float angle = angleOffset + (angleStep * i);
            float rad = angle * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;

            SpawnProjectile(spawnPos, direction);
        }
    }

    /// <summary>투사체 1개 생성 + 가속 컴포넌트 부착</summary>
    private void SpawnProjectile(Vector3 position, Vector3 direction)
    {
        GameObject projObj = Instantiate(projectilePrefab, position, Quaternion.LookRotation(direction));

        // 가속 컴포넌트 추가 (느리게 → 빠르게)
        AcceleratingProjectile accel = projObj.GetComponent<AcceleratingProjectile>();
        if (accel == null)
            accel = projObj.AddComponent<AcceleratingProjectile>();

        accel.Initialize(direction, projectileStartSpeed, projectileMaxSpeed,
            accelerationTime, projectileDamage, projectileLifetime);
    }

    #endregion
}