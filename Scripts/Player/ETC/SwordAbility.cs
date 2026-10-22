using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 검술 전용 스킬 인디케이터 시스템
/// - Q 스킬: 검기 발사 방향 표시
/// - E 스킬: 지상 강타 범위 표시
/// - 기존 Ability에서 검술 전용으로 분리
/// </summary>
public class SwordAbility : MonoBehaviour
{
    [Header("Q 스킬 인디케이터 (검기 발사)")]
    [SerializeField] private Canvas qSkillCanvas;
    [SerializeField] private Image qSkillDirectionIndicator;

    [Header("E 스킬 인디케이터 (지상 강타)")]
    [SerializeField] private Canvas eSkillCanvas;
    [SerializeField] private Image eSkillRangeIndicator;
    [SerializeField] private float maxESkillDistance = 7f;

    // 상태 관리
    private bool isQSkillFixedMode = false;     // Q스킬이 고정 모드인지 추적
    private bool isESkillLockOnMode = false;    // E스킬이 락온 모드인지
    private Vector3 eSkillFixedPosition;        // 락온 시 고정 위치

    /// <summary>초기화</summary>
    private void Start()
    {
        InitializeIndicators();
    }

    /// <summary>인디케이터 초기 설정</summary>
    private void InitializeIndicators()
    {
        // Q 스킬 인디케이터 초기화
        if (qSkillCanvas != null)
        {
            qSkillCanvas.enabled = false;
        }
        if (qSkillDirectionIndicator != null)
        {
            qSkillDirectionIndicator.enabled = false;
        }

        // E 스킬 인디케이터 초기화
        if (eSkillCanvas != null)
        {
            eSkillCanvas.enabled = false;
        }
        if (eSkillRangeIndicator != null)
        {
            eSkillRangeIndicator.enabled = false;
        }

        Debug.Log("[SwordAbility] 검술 인디케이터 시스템 초기화 완료");
    }

    /// <summary>프레임 업데이트</summary>
    private void Update()
    {
        // E스킬이 마우스 추적 모드일 때만 위치 업데이트
        if (eSkillCanvas != null && eSkillCanvas.enabled && !isESkillLockOnMode)
        {
            UpdateESkillMousePosition();
        }
    }

    // =============================================================================
    // Q 스킬 인디케이터 (검기 발사)
    // =============================================================================

    /// <summary>
    /// Q스킬 인디케이터 표시 - PlayerSwordSkill에서 호출
    /// 고정된 방향으로 화살표 표시 후 자동 삭제
    /// </summary>
    /// <param name="direction">검기 발사 방향</param>
    /// <param name="duration">표시 지속시간</param>
    public void ShowQSkillIndicator(Vector3 direction, float duration)
    {
        Debug.Log($"[SwordAbility] Q스킬 인디케이터 표시: 방향={direction}, 지속시간={duration}초");

        // 고정 모드 활성화
        isQSkillFixedMode = true;

        // 다른 스킬 인디케이터들 숨기기
        HideAllIndicators();

        // Q스킬 인디케이터 활성화
        if (qSkillCanvas != null)
            qSkillCanvas.enabled = true;
        if (qSkillDirectionIndicator != null)
            qSkillDirectionIndicator.enabled = true;

        // 고정된 방향으로 회전 설정
        SetQSkillDirection(direction);

        // 지정된 시간 후 자동 숨김
        StartCoroutine(HideQSkillIndicatorAfterDelay(duration));
    }

    /// <summary>Q스킬 인디케이터 방향 설정</summary>
    /// <param name="direction">설정할 방향</param>
    private void SetQSkillDirection(Vector3 direction)
    {
        if (qSkillCanvas == null || direction == Vector3.zero) return;

        // Y축 회전만 적용 (XZ 평면에서의 방향)
        direction.y = 0;
        direction.Normalize();

        // Canvas를 해당 방향으로 회전
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        targetRotation.eulerAngles = new Vector3(0, targetRotation.eulerAngles.y, 0);

        qSkillCanvas.transform.rotation = targetRotation;

        Debug.Log($"[SwordAbility] Q스킬 방향 설정: {targetRotation.eulerAngles.y}도");
    }

    /// <summary>지정된 시간 후 Q스킬 인디케이터 숨김</summary>
    /// <param name="delay">대기 시간</param>
    private IEnumerator HideQSkillIndicatorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Q스킬 인디케이터 숨김
        if (qSkillCanvas != null)
            qSkillCanvas.enabled = false;

        if (qSkillDirectionIndicator != null)
            qSkillDirectionIndicator.enabled = false;

        // 고정 모드 해제
        isQSkillFixedMode = false;

        Debug.Log("[SwordAbility] Q스킬 인디케이터 자동 숨김 및 고정모드 해제");
    }

    // =============================================================================
    // E 스킬 인디케이터 (지상 강타)
    // =============================================================================

    /// <summary>
    /// E스킬 인디케이터 표시 - PlayerSwordSkill에서 호출
    /// 원형 범위 인디케이터 (마우스 추적 또는 락온 위치 고정)
    /// </summary>
    /// <param name="targetPosition">스킬 타겟 위치</param>
    public void ShowESkillIndicator(Vector3 targetPosition)
    {
        Debug.Log($"[SwordAbility] E스킬 인디케이터 표시: 위치={targetPosition}");

        // 다른 스킬 인디케이터들 숨기기
        HideAllIndicators();

        // E스킬 인디케이터 활성화
        if (eSkillCanvas != null)
            eSkillCanvas.enabled = true;
        if (eSkillRangeIndicator != null)
            eSkillRangeIndicator.enabled = true;

        // 락온 모드인지 확인
        PlayerLockOn lockOn = FindFirstObjectByType<PlayerLockOn>();
        if (lockOn != null && lockOn.isLockOn && lockOn.currentTarget != null)
        {
            // 락온 모드: 고정 위치
            isESkillLockOnMode = true;
            eSkillFixedPosition = targetPosition;
            SetESkillPosition(targetPosition);
        }
        else
        {
            // 마우스 추적 모드
            isESkillLockOnMode = false;
        }

        Debug.Log($"[SwordAbility] E스킬 모드: {(isESkillLockOnMode ? "락온 고정" : "마우스 추적")}");
    }

    /// <summary>E스킬 인디케이터 숨기기</summary>
    public void HideESkillIndicator()
    {
        Debug.Log("[SwordAbility] E스킬 인디케이터 숨김");

        if (eSkillCanvas != null)
            eSkillCanvas.enabled = false;

        if (eSkillRangeIndicator != null)
            eSkillRangeIndicator.enabled = false;

        // 모드 초기화
        isESkillLockOnMode = false;
    }

    /// <summary>E스킬 위치 설정 (고정 모드용)</summary>
    /// <param name="position">설정할 위치</param>
    private void SetESkillPosition(Vector3 position)
    {
        if (eSkillCanvas == null) return;

        float fixedY = transform.position.y;
        eSkillCanvas.transform.position = new Vector3(position.x, fixedY, position.z);
    }

    /// <summary>E스킬 마우스 추적 위치 업데이트</summary>
    private void UpdateESkillMousePosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            Vector3 targetPosition = hit.point;

            // 플레이어와의 거리 제한
            Vector3 playerPosition = transform.position;
            float distance = Vector3.Distance(playerPosition, targetPosition);

            if (distance > maxESkillDistance)
            {
                Vector3 direction = (targetPosition - playerPosition).normalized;
                targetPosition = playerPosition + direction * maxESkillDistance;
            }

            SetESkillPosition(targetPosition);
        }
    }

    // =============================================================================
    // 공통 기능
    // =============================================================================

    /// <summary>모든 인디케이터 숨기기 - 충돌 방지</summary>
    private void HideAllIndicators()
    {
        // Q스킬 인디케이터
        if (qSkillCanvas != null)
            qSkillCanvas.enabled = false;
        if (qSkillDirectionIndicator != null)
            qSkillDirectionIndicator.enabled = false;

        // E스킬 인디케이터
        if (eSkillCanvas != null)
            eSkillCanvas.enabled = false;
        if (eSkillRangeIndicator != null)
            eSkillRangeIndicator.enabled = false;

        // 모드 초기화
        isQSkillFixedMode = false;
        isESkillLockOnMode = false;
    }

    /// <summary>현재 E스킬 타겟 위치 반환</summary>
    /// <returns>현재 E스킬이 조준하고 있는 위치</returns>
    public Vector3 GetESkillTargetPosition()
    {
        if (isESkillLockOnMode)
        {
            return eSkillFixedPosition;
        }
        else if (eSkillCanvas != null)
        {
            return eSkillCanvas.transform.position;
        }
        else
        {
            return transform.position;
        }
    }
}
