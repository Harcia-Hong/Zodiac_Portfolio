using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터 피격 반응 시스템
/// - 데이터(EnemySO) 설정에 따라 경직 여부 결정
/// - 슈퍼아머일 경우 색상 변화만 처리
/// </summary>
public class EnemyHitReactionSystem : MonoBehaviour
{
    [Header("피격 설정")]
    [SerializeField, Tooltip("피격 경직 시간")]
    private float hitStunDuration = 0.3f;

    [Header("시각 효과")]
    [SerializeField, Tooltip("피격 시 색상")]
    private Color hitColor = Color.red;

    // 컴포넌트 참조
    private Enemy enemyScript;
    private Renderer enemyRenderer;
    private Material originalMaterial;
    private NavMeshAgent navAgent;
    private Animator anim;
    private Color originalColor;

    private void Awake()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        enemyScript = GetComponent<Enemy>();
        enemyRenderer = GetComponentInChildren<Renderer>();
        navAgent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();

        if (enemyRenderer != null)
        {
            originalMaterial = enemyRenderer.material;
            originalColor = originalMaterial.color;
        }
    }

    /// <summary>
    /// 피격 처리 메인 메서드
    /// </summary>
    public void AddDamage(float damage)
    {
        // 1. 시각적 피드백 (빨간색 깜빡임) - 이건 무조건 실행
        StartCoroutine(HitColorFeedback());

        // 2. 경직(Stun) 여부 확인
        // 데이터가 없거나, enableHitStun이 꺼져있으면 경직 스킵 (슈퍼아머)
        if (enemyScript.Data != null && !enemyScript.Data.enableHitStun)
        {
            // Debug.Log($"[{gameObject.name}] 슈퍼아머 발동 (경직 무시)");
            return;
        }

        // --- 아래는 경직이 켜져 있을 때만 실행 ---

        // 3. 모든 행동 즉시 중단
        ForceStopCurrentAction();

        // 4. 피격 애니메이션 재생
        PlayHitAnimation();

        // 5. 경직 시간 동안 Idle 상태 유지
        ApplyHitStun(hitStunDuration);

        // Debug.Log($"[{gameObject.name}] 피격 경직 적용 ({hitStunDuration}초)");
    }

    private void ForceStopCurrentAction()
    {
        // NavMeshAgent 이동 중단
        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
        }

        // 공격 중단 (IEnemyAttack 인터페이스 활용)
        IEnemyAttack[] attackComponents = GetComponents<IEnemyAttack>();
        foreach (var attackComponent in attackComponents)
        {
            if (attackComponent != null && attackComponent.IsAttacking())
            {
                attackComponent.StopAttack();
            }
        }
    }

    private void PlayHitAnimation()
    {
        if (anim != null)
        {
            anim.SetTrigger("doHit");
        }
    }

    private IEnumerator HitColorFeedback()
    {
        if (enemyRenderer != null && originalMaterial != null)
        {
            originalMaterial.color = hitColor;
            yield return new WaitForSeconds(0.1f);
            originalMaterial.color = originalColor;
        }
    }

    private void ApplyHitStun(float duration)
    {
        if (enemyScript != null)
        {
            enemyScript.SetTemporaryIdle(duration);
        }
    }

    [ContextMenu("테스트: 피격 효과")]
    public void TestHitReaction()
    {
        AddDamage(10);
    }
}
