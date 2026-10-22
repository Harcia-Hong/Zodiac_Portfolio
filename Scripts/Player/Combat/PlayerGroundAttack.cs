using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 지상 공격 — 검(Melee) 좌클릭 단발 공격.
/// 
/// 동작:
///   좌클릭 1회 = 1타 (콤보 없음).
///   우클릭으로 캔슬 가능 (PlayerFlashSlash로 이어짐).
/// 
/// 영혼게이지 연동:
///   히트박스 윈도우에서 적 1마리 이상 명중 시 StanceGaugeSystem.AddSoulGauge(soulGaugeOnHit) 1회 호출.
///   허공 휘두름(명중 0)은 적립 없음.
///   한 번 휘둘러서 다수 명중해도 1회만 적립 (광역 보너스 X — 추후 강화 항목으로 빼는 게 깔끔).
/// 
/// 활 모드 차단:
///   ExecuteAttack 진입 시점에 MeleeCharacter.IsActive 체크 → 활 모드일 때는 모든 처리 차단.
///   별도 분기 필요 없음.
/// </summary>
public class PlayerGroundAttack : MonoBehaviour
{
    #region Serialized Fields
    [Header("Sound")]
    [Tooltip("검 휘두를 때 재생할 효과음 (비워두면 사운드 없이 작동)")]
    [SerializeField] private AudioClip swingSfx;

    // 캐싱: 같은 GameObject의 MeleeCharacter (활성 체크용)
    private MeleeCharacter meleeCharacter;

    [Header("Attack Settings - 기본 공격")]
    [SerializeField, Tooltip("공격 애니메이션 트리거 이름")]
    private string attackTrigger = "Attack1";

    [SerializeField, Tooltip("공격 전체 지속 시간 (초)")]
    private float attackDuration = 0.4f; // 1타 애니메이션 총 길이

    [SerializeField, Tooltip("공격 시 전진 거리")]
    private float forwardDistance = 0.3f;

    [SerializeField, Tooltip("전진 이동 속도")]
    private float forwardDuration = 0.15f;  // 전진 속도도 빠르게

    [Header("Collision & Layers")]
    [SerializeField, Tooltip("적 레이어")]
    private LayerMask enemyLayer = -1;

    [SerializeField, Tooltip("벽 레이어")]
    private LayerMask wallLayer = -1;

    [Header("Cancel System")]
    [SerializeField, Tooltip("우클릭 캔슬 허용")]
    private bool allowRightClickCancel = true;

    [Header("Arrows")]
    [SerializeField, Tooltip("좌클릭 명중 시 화살 적립량 (1회 공격당). 기본 1, 강화로 증가 가능.")]
    private int arrowsOnHit = 1;
    #endregion

    #region Component References
    // 컴포넌트 참조
    private Animator anim;
    private PlayerController playerController;
    private PlayerLockOn lockOn;
    private Weapon weapon;
    private Rigidbody rigid;
    private PlayerCombatStateMachine combatStateMachine;
    #endregion

    #region Combo State Variables
    // 콤보는 폐기됐지만 hitEnemies(중복 명중 방지) 자료구조는 유지 — 광역 OverlapSphere가 같은 적 두 번 잡는 것 방지용.
    private Coroutine comboCoroutine;
    private HashSet<GameObject> hitEnemies = new HashSet<GameObject>();
    #endregion

    #region Movement Variables
    // 전진 이동 제어
    private bool isMovingForward = false;
    private Vector3 moveStartPos;
    private Vector3 moveTargetPos;
    #endregion

    #region Public Properties
    /// <summary>현재 지상 공격 중인지</summary>
    public bool isGroundAttacking { get; private set; } = false;

    /// <summary>지상 공격 준비 상태인지</summary>
    public bool isGroundAttackReady { get; private set; } = true;

    // 다른 스크립트와의 호환성을 위한 프로퍼티
    public bool isAttacking => isGroundAttacking;
    public bool isFireReady => isGroundAttackReady;
    #endregion

    #region Unity Lifecycle
    /// <summary>컴포넌트 초기화</summary>
    private void Awake()
    {
        meleeCharacter = GetComponent<MeleeCharacter>();
        InitializeComponents();
    }

    /// <summary>매 프레임 업데이트</summary>
    private void Update()
    {
        // 공격 중 홀드/캔슬 입력 체크
        if (isGroundAttacking)
        {
            CheckCancelInput();
        }

        // 전진 중 충돌 체크
        if (isMovingForward)
        {
            CheckMovementCollision();
        }
    }
    #endregion

    #region Initialization
    /// <summary>컴포넌트 참조 설정</summary>
    private void InitializeComponents()
    {
        anim = GetComponentInChildren<Animator>();
        playerController = GetComponent<PlayerController>();
        lockOn = GetComponent<PlayerLockOn>();
        weapon = GetComponentInChildren<Weapon>();
        rigid = GetComponent<Rigidbody>();
        combatStateMachine = GetComponent<PlayerCombatStateMachine>();

        if (anim == null)
            Debug.LogError("[PlayerGroundAttack] Animator를 찾을 수 없습니다!");

        if (combatStateMachine == null)
            Debug.LogWarning("[PlayerGroundAttack] PlayerCombatStateMachine이 없습니다. 추가해주세요!");
    }
    #endregion

    #region Public Interface
    /// <summary>외부에서 호출: 지상 공격 시작 (좌클릭)</summary>
    public void StartGroundCombo()
    {
        if (!CanGroundAttack())
        {
            Debug.Log("[PlayerGroundAttack] 공격 불가능 상태");
            return;
        }

        // 상태 초기화
        isGroundAttacking = true;
        isGroundAttackReady = false;
        hitEnemies.Clear();

        // 준비 작업
        ResetAllTriggers();
        RotateToTarget();

        // 단발 공격 코루틴 실행
        comboCoroutine = StartCoroutine(ComboRoutine());
    }

    /// <summary>지상 공격 가능 여부 확인</summary>
    public bool CanGroundAttack()
    {
        if (playerController == null)
            return false;

        return !isGroundAttacking &&
               isGroundAttackReady &&
               !playerController.isDodging &&
               !playerController.isSkillCasting;
    }

    /// <summary>지상 공격 강제 취소</summary>
    public void CancelGroundAttack()
    {
        if (!isGroundAttacking) return;
        EndGroundCombo();
    }
    #endregion

    #region Combo System - Click/Hold Hybrid
    /// <summary>단발 공격 루틴 — 1회 휘두름 후 종료</summary>
    private IEnumerator ComboRoutine()
    {
        // 1. 현재 공격 속도 가져오기 (기본 1.0)
        float attackSpeed = 1.0f;
        if (PlayerStats.Instance != null)
        {
            attackSpeed = PlayerStats.Instance.AttackSpeed;
        }

        // 2. 애니메이션 속도 조절
        if (anim != null) anim.speed = attackSpeed;

        // 3. 공격 실행
        ExecuteAttack(attackSpeed);

        // 4. 공격 속도에 비례한 대기 (0.4초 / 2 = 0.2초)
        yield return new WaitForSeconds(attackDuration / attackSpeed);

        // 5. 공격 종료
        EndGroundCombo();
    }

    /// <summary>
    /// 공격 실행 — 타겟 조준 + 애니메이션 트리거 + 히트박스 코루틴 + 사운드.
    /// 근거리 캐릭터 비활성 시 즉시 차단 (활 모드 전환 시 잔존 호출 방지).
    /// </summary>
    private void ExecuteAttack(float speedMultiplier)
    {
        // 근거리 캐릭터 비활성이면 모든 처리 차단 (사운드 + 데미지 + 애니메이션)
        if (meleeCharacter != null && !meleeCharacter.IsActive)
        {
            return;
        }

        RotateToTarget();
        anim.SetTrigger(attackTrigger);

        // 검 휘두르기 사운드 재생
        if (swingSfx != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(swingSfx);
        }

        StartCoroutine(HitBoxWindow(speedMultiplier));
    }
    #endregion

    #region Input Handling
    /// <summary>캔슬 입력 체크 (우클릭)</summary>
    private void CheckCancelInput()
    {
        if (!allowRightClickCancel)
            return;

        // 우클릭 입력 감지
        if (Input.GetMouseButtonDown(1))
        {
            Debug.Log("[PlayerGroundAttack] 우클릭 캔슬 입력 감지");
            CancelGroundAttack();
        }
    }
    #endregion

    #region Hit Detection
    /// <summary>히트박스 타이밍 제어</summary>
    private IEnumerator HitBoxWindow(float speedMultiplier)
    {
        // 원래 0.05초 대기 -> 속도만큼 나눔
        yield return new WaitForSeconds(0.05f / speedMultiplier);

        EnableHitBox();

        // 원래 0.2초 지속 -> 속도만큼 나눔
        yield return new WaitForSeconds(0.2f / speedMultiplier);

        DisableHitBox();
    }

    /// <summary>
    /// 히트박스 활성화 — 적 탐지 + 데미지 + 영혼게이지 적립.
    /// 1회 호출 = 1회 공격의 1회 히트 윈도우. N마리 동시 명중 시 데미지는 N번 들어가지만
    /// 영혼게이지 적립은 1회로 제한 (정책: 1회 공격당 +1).
    /// </summary>
    private void EnableHitBox()
    {
        if (weapon == null) return;

        Vector3 attackPos = transform.position + transform.forward * 1f;
        Collider[] hits = Physics.OverlapSphere(attackPos, 1.5f, enemyLayer);

        bool anyHit = false;

        foreach (var hit in hits)
        {
            GameObject targetObj = hit.gameObject;
            if (hitEnemies.Contains(targetObj)) continue;

            IDamageable damageable = targetObj.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                // [참고] 치명타는 Weapon.GetDamage 내부에서 이미 처리됨
                int damage = weapon.GetDamage(true);
                damageable.ApplySkillDamage(damage, HitEffectType.Slashing_Normal);
                hitEnemies.Add(targetObj);
                anyHit = true;
            }
        }

        // 화살 적립 — 한 번 휘둘러서 1마리라도 맞았으면 +arrowsOnHit, 0마리면 적립 없음
        if (anyHit && StanceGaugeSystem.Instance != null)
        {
            StanceGaugeSystem.Instance.AddArrows(arrowsOnHit);
        }
    }

    /// <summary>히트박스 비활성화</summary>
    private void DisableHitBox()
    {
        // 현재는 특별한 처리 없음
    }
    #endregion

    #region Movement & Rotation
    /// <summary>전진 이동 처리 (현재 미사용 — 추후 부활 가능성 위해 보존)</summary>
    /*private void MoveForward(int attackIndex)
    {
        if (attackIndex >= forwardDistances.Count)
            return;

        float distance = forwardDistances[attackIndex];
        if (distance <= 0f)
            return;

        // 이동 방향 결정 (락온 우선)
        Vector3 dir = GetMoveDirection();

        // 타겟 위치 계산 (Y축 고정)
        Vector3 targetPos = transform.position + new Vector3(dir.x, 0, dir.z) * distance;
        targetPos.y = transform.position.y;

        // 벽 충돌 체크
        CheckWallCollision(ref targetPos, distance);

        // DoTween으로 부드러운 이동
        transform.DOMove(targetPos, forwardDuration).SetEase(Ease.OutCubic);
    }*/

    /// <summary>이동 방향 계산</summary>
    private Vector3 GetMoveDirection()
    {
        // 락온 타겟이 있으면 그쪽으로
        if (lockOn != null && lockOn.isLockOn && lockOn.currentTarget != null)
        {
            return (lockOn.currentTarget.position - transform.position).normalized;
        }

        // 없으면 전방
        return transform.forward;
    }

    /// <summary>벽 충돌 체크 및 경로 보정</summary>
    private void CheckWallCollision(ref Vector3 targetPos, float distance)
    {
        Vector3 direction = (targetPos - transform.position).normalized;

        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, distance, wallLayer))
        {
            // 벽 앞에서 정지하도록 보정
            targetPos = hit.point - direction * 0.1f;
        }
    }

    /// <summary>전진 중 적 충돌 체크</summary>
    private void CheckMovementCollision()
    {
        // 전방 0.5f 거리에서 적 충돌 체크
        if (Physics.Raycast(transform.position, transform.forward, 0.5f, enemyLayer))
        {
            // 적과 충돌하면 이동 정지 (애니메이션은 계속)
            isMovingForward = false;
        }
    }

    /// <summary>타겟 방향 회전</summary>
    private void RotateToTarget()
    {
        Vector3 dir = GetLookDirection();
        dir.y = 0;

        if (dir != Vector3.zero)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            transform.rotation = rot;
        }
    }

    /// <summary>바라볼 방향 계산</summary>
    private Vector3 GetLookDirection()
    {
        // 락온 타겟이 있으면 그쪽으로
        if (lockOn != null && lockOn.isLockOn && lockOn.currentTarget != null)
        {
            return (lockOn.currentTarget.position - transform.position).normalized;
        }

        // 없으면 마우스 방향
        if (playerController != null)
        {
            return playerController.GetMouseDirection();
        }

        return transform.forward;
    }
    #endregion

    #region Combo End
    /// <summary>지상 공격 종료 처리</summary>
    private void EndGroundCombo()
    {
        if (comboCoroutine != null) { StopCoroutine(comboCoroutine); comboCoroutine = null; }

        isGroundAttacking = false;
        hitEnemies.Clear();

        // [중요] 애니메이션 속도 원상복구 (안 그러면 달리기도 빨라짐)
        if (anim != null) anim.speed = 1.0f;

        if (combatStateMachine != null) combatStateMachine.ReturnToIdle();

        StartCoroutine(AttackCooldown());
    }

    /// <summary>공격 쿨다운</summary>
    private IEnumerator AttackCooldown()
    {
        // 쿨타임도 공격 속도에 비례해서 감소시킬지 결정
        // 여기선 아주 짧은 쿨다운(0.1f)이라 고정으로 둡니다.
        isGroundAttackReady = false;
        yield return new WaitForSeconds(0.1f);
        isGroundAttackReady = true;
    }
    #endregion

    #region Utility
    /// <summary>모든 애니메이터 트리거 리셋</summary>
    private void ResetAllTriggers()
    {
        if (anim == null)
            return;

        anim.ResetTrigger(attackTrigger);
        anim.ResetTrigger("Dodge");
        anim.ResetTrigger("Skill_Q");
        anim.ResetTrigger("Skill_E");
    }
    #endregion

    #region Debug Visualization
    /// <summary>기즈모로 공격 범위 표시</summary>
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
            return;

        // 공격 범위 표시
        Gizmos.color = Color.red;
        Vector3 attackPos = transform.position + transform.forward * 1f;
        Gizmos.DrawWireSphere(attackPos, 1.5f);

        // 전진 방향 표시
        Gizmos.color = Color.blue;
    }
    #endregion
}
