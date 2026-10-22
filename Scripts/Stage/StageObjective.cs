using UnityEngine;

public class StageObjective : MonoBehaviour
{
    [Header("목표 정보")]
    [Tooltip("목표 이름")]
    public string objectiveName = "적 처치";

    [Tooltip("상세 설명")]
    public string description = "모든 적을 처치하세요";

    [Tooltip("목표 타입")]
    public ObjectiveType type = ObjectiveType.KillEnemies;

    [Header("목표 설정 값")]
    [Tooltip("목표 수치")]
    public int targetCount = 5;

    [Tooltip("대상의 Tag")]
    public string targetTag = "Enemy";

    [Tooltip("목표 위치(목표 지점 도달, 탈출 등에 사용)")]
    public Transform targetLocation;

    [Tooltip("위치를 기반으로 하는 목표의 판정 변경")]
    public float targetRadius = 5f;

    [Header("선택적인 조건 제시")]
    [Tooltip("반드시 수행해야 하는지 체크")]
    public bool isOptional = false;

    [Tooltip("실패 시 스테이지 실패 여부 판단")]
    public bool isFailCondition = false;

    [Header("진행 사항 (런타임 전용)")]
    [SerializeField, Tooltip("현재 진행도")]
    private int currentCount = 0;

    [SerializeField, Tooltip("목표 완료 여부")]
    private bool isCompleted = false;

    /// <summary>스테이지 목표 타입 열거</summary>
    public enum ObjectiveType
    {
        [Tooltip("적 처치 (가장 일반적인 목표)")]
        KillEnemies,

        [Tooltip("목표 아이템 수집")]
        CollectItems,

        [Tooltip("목표 위치 도달")]
        ReachLocation,

        [Tooltip("생존")]
        SurviveTime,

        [Tooltip("탈출")]
        Escape,

        [Tooltip("특성 인물 보호")]
        ProtectTarget,

        [Tooltip("보스 처치")]
        DefeatBoss
    }

    // =============================================================================
    // 공개 인터페이스 (런타임 사용)
    // =============================================================================

    ///<summary>목표 초기화 (스테이지 시작~ 하면 호출되요)</summary>
    public void Initialize()
    {
        currentCount = 0;
        isCompleted = false;

        Debug.Log($"[StageObjective] 목표 초기화: {objectiveName}");
    }

    /// <summary>진행도 업데이트</summary>
    /// <param name="amount">증가량 (기본값 1)</param>
    public void UpdateProgress(int amount = 1)
    {
        if (isCompleted) return;

        currentCount += amount;
        currentCount = Mathf.Clamp(currentCount, 0, targetCount);

        Debug.Log($"[StageObjective] {objectiveName} 진행: {currentCount}/{targetCount}");

        // 목표 달성 체크
        if (currentCount >= targetCount)
        {
            CompleteObjective();
        }
    }

    /// <summary>목표 강제 완료</summary>
    public void CompleteObjective()
    {
        if (isCompleted) return;

        isCompleted = true;
        Debug.Log($"[StageObjective] 목표 완료: {objectiveName}");

        // 목표 완료 이벤트 (필요시 추가)
        //OnObjectiveCompleted?.Invoke(this);
    }

    /// <summary>목표 실패 처리</summary>
    public void FailObjective()
    {
        if (!isCompleted) return;

        Debug.Log($"[StageObjective] 목표 실패: {objectiveName}");

        // 실패 조건이면 스테이지 실패 트리거
        if (isFailCondition)
        {
            OnObjectiveFailed?.Invoke(this);
        }
    }

    /// <summary>진행률 반환 (0.0 ~ 1.0)</summary>
    public float GetProgressRatio()
    {
        if (targetCount <= 0) return 1f;
        return (float)currentCount / targetCount;
    }

    /// <summary>목표 완료 여부 반환</summary>
    public bool IsCompleted() => isCompleted;

    /// <summary>현재 진행도 반환</summary>
    public int GetCurrentCount() => currentCount;

    /// <summary>목표 수치 반환</summary>
    public int GetTargetCount() => targetCount;

    /// <summary>목표 타입별 표시 텍스트 생성</summary>
    public string GetDisplayText()
    {
        switch (type)
        {
            case ObjectiveType.KillEnemies:
            case ObjectiveType.CollectItems:
            case ObjectiveType.DefeatBoss:
                return $"{objectiveName}: {currentCount}/{targetCount}";

            case ObjectiveType.ReachLocation:
            case ObjectiveType.Escape:
            case ObjectiveType.ProtectTarget:
            case ObjectiveType.SurviveTime:
                return isCompleted ? $"{objectiveName}: 완료" : objectiveName;

            default:
                return $"{objectiveName}: {currentCount}/{targetCount}";
        }
    }

    // =============================================================================
    // 이벤트 시스템
    // =============================================================================

    /// <summary>목표 완료 이벤트</summary>
    public System.Action<StageObjective> OnObjectiveCompleted;

    /// <summary>목표 실패 이벤트</summary>
    public System.Action<StageObjective> OnObjectiveFailed;

    // =============================================================================
    // 에디터 전용 기능
    // =============================================================================

    /// <summary>목표 유효성 검사</summary>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(objectiveName)) return false;

        // 위치 기반 목표는 위치가 필요
        if ((type == ObjectiveType.ReachLocation || type == ObjectiveType.Escape) && targetLocation == null)
        {
            Debug.LogError($"[StageObjective] {type} 목표는 targetLocation이 필요합니다: {objectiveName}");
            return false;
        }

        // 카운트 기반 목표는 targetCount가 필요
        if ((type == ObjectiveType.KillEnemies || type == ObjectiveType.CollectItems || type == ObjectiveType.DefeatBoss)
            && targetCount <= 0)
        {
            Debug.LogError($"[StageObjective] {type} 목표는 targetCount가 0보다 커야 합니다: {objectiveName}");
            return false;
        }

        return true;
    }

    /// <summary>디버그 정보 출력</summary>
    [ContextMenu("목표 정보 출력")]
    public void LogObjectiveInfo()
    {
        Debug.Log($"=== 목표 정보: {objectiveName} ===");
        Debug.Log($"타입: {type}");
        Debug.Log($"진행: {currentCount}/{targetCount}");
        Debug.Log($"완료: {isCompleted}");
        Debug.Log($"선택적: {isOptional}");
        Debug.Log($"실패조건: {isFailCondition}");

        if (targetLocation != null)
            Debug.Log($"목표 위치: {targetLocation.name} (반경: {targetRadius})");
    }


}
