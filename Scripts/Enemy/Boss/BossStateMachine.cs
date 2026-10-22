using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 전용 상태머신
/// - EnemyStateMachine을 상속하여 기존 Idle 상태 재활용
/// - 공격 흐름을 보스 전용(패턴 선택 → 실행)으로 교체
/// - 페이즈 전환 상태 추가
/// - 패턴 쿨타임 및 선택 이력 관리
/// </summary>
public class BossStateMachine : EnemyStateMachine
{
    // ===== 보스 전용 참조 =====

    /// <summary>보스 데이터 SO</summary>
    public BossDataSO BossData { get; private set; }

    /// <summary>페이즈 컨트롤러</summary>
    public BossPhaseController PhaseController { get; private set; }

    /// <summary>패턴 실행기</summary>
    public BossPatternExecutor PatternExecutor { get; private set; }

    // ===== 보스 전용 상태 =====

    /// <summary>보스 전용 Idle 상태 (BossDataSO 기반 탐지)</summary>
    public BossIdleState BossIdleState { get; private set; }

    /// <summary>보스 전용 추적 상태 (패턴 선택 상태로 전환)</summary>
    public BossChasingState BossChasingState { get; private set; }

    /// <summary>패턴 선택 + 실행 상태</summary>
    public BossPatternSelectState PatternSelectState { get; private set; }

    /// <summary>페이즈 전환 연출 상태</summary>
    public BossPhaseTransitionState PhaseTransitionState { get; private set; }

    // ===== 패턴 쿨타임 관리 =====

    // 각 패턴별 다음 사용 가능 시간
    private Dictionary<BossAttackPatternSO, float> patternCooldownEndTimes = new Dictionary<BossAttackPatternSO, float>();

    /// <summary>마지막으로 사용한 패턴 (연속 사용 방지용)</summary>
    public BossAttackPatternSO LastUsedPattern { get; private set; }

    #region 생성자

    /// <summary>
    /// 보스 상태머신 생성자
    /// - 기존 EnemyStateMachine 초기화 후 보스 전용 참조 및 상태 추가
    /// </summary>
    public BossStateMachine(Enemy enemy, BossDataSO bossData,
        BossPhaseController phaseController, BossPatternExecutor patternExecutor)
        : base(enemy)
    {
        BossData = bossData;
        PhaseController = phaseController;
        PatternExecutor = patternExecutor;

        // 보스 전용 상태 생성
        BossIdleState = new BossIdleState(this);
        BossChasingState = new BossChasingState(this);
        PatternSelectState = new BossPatternSelectState(this);
        PhaseTransitionState = new BossPhaseTransitionState(this);

        // 페이즈 전환 이벤트 구독
        if (PhaseController != null)
        {
            PhaseController.OnPhaseTransitionStart += HandlePhaseTransitionStart;
            PhaseController.OnPhaseTransitionEnd += HandlePhaseTransitionEnd;
        }
    }

    #endregion

    #region 타겟 설정

    /// <summary>
    /// 타겟 설정 (BossIdleState에서 호출)
    /// BossStateMachine은 EnemyStateMachine을 상속하므로 protected Target에 접근 가능
    /// </summary>
    public void SetTarget(Transform target)
    {
        Target = target;
    }

    #endregion

    #region 페이즈 전환 이벤트 핸들러

    /// <summary>
    /// 페이즈 전환 시작 시 호출
    /// - 현재 상태 무관하게 강제로 전환 상태로 진입
    /// </summary>
    private void HandlePhaseTransitionStart(int newPhaseIndex)
    {
        Debug.Log($"[BossStateMachine] 페이즈 전환 감지 → PhaseTransitionState 진입 (페이즈 {newPhaseIndex})");
        ChangeState(PhaseTransitionState);
    }

    /// <summary>
    /// 페이즈 전환 완료 시 호출
    /// - 전환 상태에서 추적 상태로 복귀
    /// </summary>
    private void HandlePhaseTransitionEnd(int newPhaseIndex)
    {
        Debug.Log($"[BossStateMachine] 페이즈 전환 완료 → BossChasingState 복귀");

        // 패턴 쿨타임 초기화 (새 페이즈 시작이니까)
        ClearAllPatternCooldowns();

        ChangeState(BossChasingState);
    }

    #endregion

    #region 패턴 쿨타임 관리

    /// <summary>
    /// 특정 패턴의 쿨타임 설정
    /// </summary>
    public void SetPatternCooldown(BossAttackPatternSO pattern)
    {
        if (pattern == null) return;

        float cooldownEnd = Time.time + pattern.cooldown;
        patternCooldownEndTimes[pattern] = cooldownEnd;
        LastUsedPattern = pattern;

        Debug.Log($"[BossStateMachine] 패턴 '{pattern.patternName}' 쿨타임 설정: {pattern.cooldown}초");
    }

    /// <summary>
    /// 특정 패턴이 쿨타임 중인지 확인
    /// </summary>
    public bool IsPatternOnCooldown(BossAttackPatternSO pattern)
    {
        if (pattern == null) return false;

        if (patternCooldownEndTimes.TryGetValue(pattern, out float endTime))
        {
            return Time.time < endTime;
        }

        return false;
    }

    /// <summary>
    /// 모든 패턴 쿨타임 초기화
    /// </summary>
    public void ClearAllPatternCooldowns()
    {
        patternCooldownEndTimes.Clear();
        LastUsedPattern = null;
        Debug.Log("[BossStateMachine] 모든 패턴 쿨타임 초기화");
    }

    #endregion

    #region 패턴 선택

    /// <summary>
    /// 현재 페이즈에서 사용 가능한 패턴 중 하나를 가중치 기반으로 선택
    /// - 쿨타임 중인 패턴 제외
    /// - 거리 조건 불만족 패턴 제외
    /// - 연속 사용 방지 옵션 적용
    /// </summary>
    /// <returns>선택된 패턴 SO (없으면 null)</returns>
    public BossAttackPatternSO SelectPattern()
    {
        // 현재 페이즈의 패턴 목록 가져오기
        BossAttackPatternSO[] availablePatterns = PhaseController?.GetCurrentPatterns();
        if (availablePatterns == null || availablePatterns.Length == 0)
        {
            Debug.LogWarning("[BossStateMachine] 사용 가능한 패턴이 없습니다!");
            return null;
        }

        // 플레이어와의 거리 계산
        float distanceToPlayer = float.MaxValue;
        if (Target != null)
        {
            distanceToPlayer = Vector3.Distance(Enemy.transform.position, Target.position);
        }

        // 사용 가능한 패턴 필터링 + 가중치 합산
        List<BossAttackPatternSO> candidates = new List<BossAttackPatternSO>();
        float totalWeight = 0f;

        foreach (var pattern in availablePatterns)
        {
            if (pattern == null) continue;

            // 쿨타임 체크
            if (IsPatternOnCooldown(pattern)) continue;

            // 거리 조건 체크
            if (!pattern.IsInRange(distanceToPlayer)) continue;

            // 연속 사용 방지
            if (BossData != null && BossData.preventSamePatternRepeat
                && pattern == LastUsedPattern && availablePatterns.Length > 1)
            {
                continue;
            }

            candidates.Add(pattern);
            totalWeight += pattern.selectionWeight;
        }

        // 후보가 없으면 null
        if (candidates.Count == 0)
        {
            Debug.Log("[BossStateMachine] 사용 가능한 패턴 후보가 없음 (전부 쿨타임 or 거리 불만족)");
            return null;
        }

        // 가중치 기반 랜덤 선택
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var pattern in candidates)
        {
            currentWeight += pattern.selectionWeight;
            if (randomValue <= currentWeight)
            {
                Debug.Log($"[BossStateMachine] 패턴 선택: '{pattern.patternName}' (후보 {candidates.Count}개 중)");
                return pattern;
            }
        }

        // 폴백: 첫 번째 후보 반환
        return candidates[0];
    }

    #endregion

    #region 이동 속도 오버라이드

    /// <summary>
    /// 보스 데이터 기반 이동 속도 반환 (페이즈 배율 적용)
    /// </summary>
    public float GetBossMoveSpeed()
    {
        if (BossData == null) return 4.0f;

        float baseSpeed = BossData.baseMoveSpeed;
        float phaseMultiplier = PhaseController?.GetCurrentMoveSpeedMultiplier() ?? 1.0f;

        return baseSpeed * phaseMultiplier;
    }

    #endregion
}