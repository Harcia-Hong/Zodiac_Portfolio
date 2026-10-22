// BossGroundThorns.cs — 전체

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 대지의 가시 패턴 (페이즈 전환 패턴)
/// 
/// 흐름 (3회 반복):
///   1. 보스 주변 원형 텔레그래프 Fill → 충격파 데미지
///   2. 필드 곳곳에 Circle 텔레그래프 다수 → Fill → 가시 폭발 데미지
///   3회차 Circle 생성 시점에 isExecuting = false
///   → BossPhaseController가 전환 완료 처리 → 보스 행동 복귀
///   → 3회차 가시는 백그라운드에서 계속 진행
/// 
/// 무적 없음, BossPatternExecutor의 커스텀 패턴으로 호출
/// </summary>
public class BossGroundThorns : MonoBehaviour
{
    [Header("반복 설정")]
    [SerializeField, Tooltip("총 반복 횟수")]
    private int totalRounds = 3;

    [Header("충격파 설정 (보스 주변)")]
    [SerializeField, Tooltip("충격파 텔레그래프 Fill 시간")]
    private float shockwaveFillDuration = 1.0f;

    [SerializeField, Tooltip("충격파 범위")]
    private float shockwaveRadius = 5f;

    [SerializeField, Tooltip("충격파 데미지")]
    private int shockwaveDamage = 30;

    [SerializeField, Tooltip("충격파 넉백")]
    private float shockwaveKnockback = 10f;

    [Header("가시 설정 (필드 곳곳)")]
    [SerializeField, Tooltip("가시 텔레그래프 Fill 시간")]
    private float thornFillDuration = 1.5f;

    [SerializeField, Tooltip("가시 개수 (시작)")]
    private int baseThornCount = 5;

    [SerializeField, Tooltip("라운드마다 추가 개수")]
    private int thornCountIncrease = 3;

    [SerializeField, Tooltip("가시 텔레그래프 반지름")]
    private float thornRadius = 2.5f;

    [SerializeField, Tooltip("가시끼리 최소 간격")]
    private float thornMinSpacing = 3f;

    [SerializeField, Tooltip("가시 데미지")]
    private int thornDamage = 25;

    [SerializeField, Tooltip("가시 넉백")]
    private float thornKnockback = 6f;

    [Header("위치 생성")]
    [SerializeField, Tooltip("플레이어 주변 배치 비율")]
    private float playerFocusRatio = 0.6f;

    [SerializeField, Tooltip("플레이어 주변 반경")]
    private float playerNearRadius = 7f;

    [SerializeField, Tooltip("필드 전체 반경")]
    private float fieldRadius = 15f;

    [SerializeField, Tooltip("플레이어 레이어")]
    private LayerMask playerLayer;

    [Header("프리팹")]
    [SerializeField, Tooltip("텔레그래프 외곽 프리팹")]
    private GameObject telegraphBorderPrefab;

    [SerializeField, Tooltip("텔레그래프 Fill 프리팹")]
    private GameObject telegraphFillPrefab;

    [SerializeField, Tooltip("충격파 VFX 프리팹")]
    private GameObject shockwaveVFXPrefab;

    [SerializeField, Tooltip("가시 폭발 VFX 프리팹")]
    private GameObject thornVFXPrefab;

    [Header("타이밍")]
    [SerializeField, Tooltip("변신 후 첫 충격파까지 대기")]
    private float enterDelay = 0.5f;

    [SerializeField, Tooltip("충격파 후 가시 생성까지 대기")]
    private float shockToThornDelay = 0.3f;

    [SerializeField, Tooltip("가시 폭발 후 다음 라운드까지 대기")]
    private float roundDelay = 0.8f;

    [SerializeField, Tooltip("VFX 생성 후 데미지까지 딜레이")]
    private float impactDamageDelay = 0.2f;

    // 컴포넌트
    private Animator anim;
    private Transform playerTransform;

    // 상태
    private bool isExecuting = false;
    private Coroutine thornsCoroutine;
    private List<GameObject> activeTelegraphs = new List<GameObject>();

    /// <summary>현재 실행 중인지 (BossPatternExecutor가 체크)</summary>
    public bool IsExecuting => isExecuting;

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
    }

    #region 외부 인터페이스

    /// <summary>
    /// 패턴 실행 (BossPatternExecutor에서 호출)
    /// </summary>
    public void Execute()
    {
        if (isExecuting) return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;

        thornsCoroutine = StartCoroutine(GroundThornsRoutine());
    }

    /// <summary>강제 중단</summary>
    public void Cancel()
    {
        if (thornsCoroutine != null)
        {
            StopCoroutine(thornsCoroutine);
            thornsCoroutine = null;
        }

        CleanupAllTelegraphs();

        if (anim != null)
            anim.SetBool("isPattern2", false);

        isExecuting = false;
    }

    #endregion

    #region 메인 루틴

    /// <summary>
    /// 대지의 가시 전체 흐름
    /// 변신 → 3회 반복 (충격파 + 가시)
    /// 3회차 가시 생성 시점에 isExecuting = false → 보스 행동 복귀
    /// 가시 폭발은 백그라운드에서 계속 진행
    /// </summary>
    private IEnumerator GroundThornsRoutine()
    {
        isExecuting = true;

        // 변신 (Hide2)
        if (anim != null)
        {
            anim.SetBool("Walk", false);
            anim.SetBool("isPattern2", true);
        }

        var nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav != null && nav.enabled)
        {
            nav.ResetPath();
            nav.velocity = Vector3.zero;
        }

        yield return new WaitForSeconds(enterDelay);

        // 충격파 1회
        yield return StartCoroutine(ShockwavePhase());

        yield return new WaitForSeconds(shockToThornDelay);

        // 가시 3회 반복
        for (int round = 0; round < totalRounds; round++)
        {
            bool isFinalRound = (round == totalRounds - 1);
            int thornCount = baseThornCount + (thornCountIncrease * round);

            Debug.Log($"[GroundThorns] 가시 {round + 1}/{totalRounds} — {thornCount}개");

            if (isFinalRound)
            {
                // 3회차: 가시 생성 → 보스 즉시 복귀 (가시는 백그라운드)
                StartCoroutine(ThornPhase(thornCount));

                if (anim != null)
                    anim.SetBool("isPattern2", false);

                isExecuting = false;
                Debug.Log("[GroundThorns] 3회차 — 보스 행동 복귀!");
            }
            else
            {
                yield return StartCoroutine(ThornPhase(thornCount));
                yield return new WaitForSeconds(roundDelay);
            }
        }

        Debug.Log("[GroundThorns] 패턴 완료");
    }

    #endregion

    #region Phase A — 충격파

    /// <summary>
    /// 보스 주변 원형 텔레그래프 Fill → 충격파 데미지
    /// </summary>
    private IEnumerator ShockwavePhase()
    {
        Vector3 bossPos = transform.position;

        // 텔레그래프 생성
        List<TelegraphData> telegraphs = SpawnTelegraphsAt(
            new List<Vector3> { bossPos }, shockwaveRadius, shockwaveFillDuration);

        // Fill 완료 대기
        yield return new WaitForSeconds(shockwaveFillDuration);

        // 충격파 VFX
        if (shockwaveVFXPrefab != null)
        {
            GameObject vfx = Instantiate(shockwaveVFXPrefab, bossPos, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        yield return new WaitForSeconds(impactDamageDelay);

        // 데미지 판정
        DealDamageAtPositions(new List<Vector3> { bossPos }, shockwaveRadius,
            shockwaveDamage, shockwaveKnockback);

        // 텔레그래프 정리
        CleanupTelegraphs(telegraphs);

        Debug.Log("[GroundThorns] 충격파 완료");
    }

    #endregion

    #region Phase B — 가시

    /// <summary>
    /// 필드 곳곳에 Circle 텔레그래프 → Fill → 가시 폭발 데미지
    /// </summary>
    private IEnumerator ThornPhase(int count)
    {
        List<Vector3> positions = GeneratePositions(count);

        List<TelegraphData> telegraphs = SpawnTelegraphsAt(
            positions, thornRadius, thornFillDuration);

        // Fill 완료 대기
        yield return new WaitForSeconds(thornFillDuration);

        // 가시 VFX
        foreach (Vector3 pos in positions)
        {
            if (thornVFXPrefab != null)
            {
                GameObject vfx = Instantiate(thornVFXPrefab, pos, Quaternion.identity);
                Destroy(vfx, 3f);
            }
        }

        yield return new WaitForSeconds(impactDamageDelay);

        // 데미지 판정
        DealDamageAtPositions(positions, thornRadius, thornDamage, thornKnockback);

        // 텔레그래프 정리
        CleanupTelegraphs(telegraphs);

        Debug.Log($"[GroundThorns] 가시 {count}개 폭발 완료");
    }

    #endregion

    #region 텔레그래프 관리

    private struct TelegraphData
    {
        public GameObject borderObj;
        public GameObject fillObj;
    }

    /// <summary>지정 위치들에 텔레그래프 생성 + Fill 시작</summary>
    private List<TelegraphData> SpawnTelegraphsAt(List<Vector3> positions, float radius, float fillDuration)
    {
        List<TelegraphData> dataList = new List<TelegraphData>();

        foreach (Vector3 pos in positions)
        {
            TelegraphData data = new TelegraphData();
            Vector3 spawnPos = pos + Vector3.up * 0.05f;
            Quaternion rotation = Quaternion.Euler(90f, 0f, 0f);
            float diameter = radius * 2f;

            if (telegraphBorderPrefab != null)
            {
                data.borderObj = Instantiate(telegraphBorderPrefab, spawnPos, rotation);
                data.borderObj.transform.localScale = Vector3.one * diameter;
                activeTelegraphs.Add(data.borderObj);
            }

            if (telegraphFillPrefab != null)
            {
                data.fillObj = Instantiate(telegraphFillPrefab, spawnPos, rotation);
                data.fillObj.transform.localScale = Vector3.zero;
                activeTelegraphs.Add(data.fillObj);
                StartCoroutine(FillAnimation(data.fillObj, diameter, fillDuration));
            }

            dataList.Add(data);
        }

        return dataList;
    }

    /// <summary>Fill 애니메이션 (Scale 0 → 목표)</summary>
    private IEnumerator FillAnimation(GameObject fillObj, float targetSize, float duration)
    {
        if (fillObj == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (fillObj == null) yield break;

            float progress = Mathf.Clamp01(elapsed / duration);
            fillObj.transform.localScale = Vector3.one * Mathf.Lerp(0f, targetSize, progress);
            yield return null;
        }

        if (fillObj != null)
            fillObj.transform.localScale = Vector3.one * targetSize;
    }

    private void CleanupTelegraphs(List<TelegraphData> dataList)
    {
        foreach (var data in dataList)
        {
            if (data.borderObj != null) { activeTelegraphs.Remove(data.borderObj); Destroy(data.borderObj); }
            if (data.fillObj != null) { activeTelegraphs.Remove(data.fillObj); Destroy(data.fillObj); }
        }
    }

    private void CleanupAllTelegraphs()
    {
        foreach (var obj in activeTelegraphs)
            if (obj != null) Destroy(obj);
        activeTelegraphs.Clear();
    }

    #endregion

    #region 데미지 판정

    private void DealDamageAtPositions(List<Vector3> positions, float radius, int damage, float knockback)
    {
        foreach (Vector3 pos in positions)
        {
            Collider[] hits = Physics.OverlapSphere(pos, radius, playerLayer);
            foreach (var hit in hits)
            {
                PlayerHealth ph = hit.GetComponent<PlayerHealth>();
                if (ph != null)
                {
                    ph.TakeDamage(damage);
                    if (knockback > 0f)
                    {
                        Rigidbody rb = hit.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            Vector3 dir = (hit.transform.position - pos).normalized;
                            dir.y = 0f;
                            rb.AddForce(dir * knockback, ForceMode.Impulse);
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region 위치 생성

    private List<Vector3> GeneratePositions(int count)
    {
        List<Vector3> positions = new List<Vector3>();
        if (playerTransform == null) return positions;

        Vector3 playerPos = playerTransform.position;
        playerPos.y = 0f;

        int nearCount = Mathf.RoundToInt(count * playerFocusRatio);
        int farCount = count - nearCount;

        GenerateInArea(positions, playerPos, playerNearRadius, nearCount);
        GenerateInArea(positions, transform.position, fieldRadius, farCount);

        return positions;
    }

    private void GenerateInArea(List<Vector3> existing, Vector3 center, float radius, int count)
    {
        int maxAttempts = 30;
        for (int i = 0; i < count; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector2 rnd = Random.insideUnitCircle * radius;
                Vector3 candidate = center + new Vector3(rnd.x, 0f, rnd.y);

                if (IsPositionValid(existing, candidate))
                {
                    if (Physics.Raycast(candidate + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
                        candidate.y = hit.point.y;
                    existing.Add(candidate);
                    placed = true;
                    break;
                }
            }
            if (!placed)
            {
                Vector2 fb = Random.insideUnitCircle * radius;
                existing.Add(center + new Vector3(fb.x, 0f, fb.y));
            }
        }
    }

    private bool IsPositionValid(List<Vector3> existing, Vector3 candidate)
    {
        foreach (Vector3 pos in existing)
        {
            float dist = Vector3.Distance(
                new Vector3(pos.x, 0f, pos.z),
                new Vector3(candidate.x, 0f, candidate.z));
            if (dist < thornMinSpacing) return false;
        }
        return true;
    }

    #endregion

    private void OnDestroy()
    {
        CleanupAllTelegraphs();
    }
}