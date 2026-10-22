using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// 보스 메인 컨트롤러
/// - Enemy.cs와 독립적으로 동작하는 보스 전용 컨트롤러
/// - BossStateMachine 기반 FSM 구동
/// - 무적 상태 데미지 차단, 페이즈 전환 연동
/// - BossHPBarUI 연동 (이름/칭호, 페이즈 경계선, 잔상바)
/// 
/// 사용법:
///   보스 프리팹에 이 컴포넌트만 붙이면 됨 (Enemy.cs 대신 사용)
///   필요 컴포넌트: Rigidbody, BoxCollider, NavMeshAgent, Animator,
///                  EnemyHealth, BossPatternExecutor, BossPhaseController, AttackTelegraph
/// </summary>
public class Boss : MonoBehaviour, IDamageable
{
    [Header("보스 데이터")]
    [SerializeField] private BossDataSO bossData;
    public BossDataSO BossData => bossData;

    [Header("애니메이션 데이터")]
    [SerializeField] private EnemyAnimationData animationData;
    public EnemyAnimationData AnimationData => animationData;

    [Header("Sound")]
    [Tooltip("보스 등장 시 재생할 효과음 (페이드 인급 임팩트 사운드)")]
    [SerializeField] private AudioClip appearSfx;

    [Tooltip("보스 사망 시 재생할 효과음")]
    [SerializeField] private AudioClip deathSfx;

    [Tooltip("보스 피격 시 재생할 효과음 (잡몹과 다른 묵직한 사운드 권장)")]
    [SerializeField] private AudioClip hitSfx;

    [Header("UI 참조")]
    [SerializeField, Tooltip("보스 HP바 UI 프리팹 (Canvas에 자동 생성됨)")]
    private BossHPBarUI bossHPBarPrefab;
    [SerializeField] private Transform hpAnchor;

    // 런타임 생성된 HP바 인스턴스
    private BossHPBarUI bossHPBarUI;

    [Header("이펙트 참조")]
    [SerializeField] private EnemyHitEffectManager hitEffectManager;
    [SerializeField] private GameObject deathVFXPrefab;

    // ===== 컴포넌트 참조 (public 읽기 전용) =====
    public Rigidbody rigid { get; private set; }
    public BoxCollider boxCollider { get; private set; }
    public NavMeshAgent nav { get; private set; }
    public Animator animator { get; private set; }
    public EnemyHealth health { get; private set; }

    // ===== 보스 전용 컴포넌트 =====
    private BossPatternExecutor patternExecutor;
    private BossPhaseController phaseController;

    // ===== FSM =====
    private BossStateMachine stateMachine;

    // ===== 상태 플래그 =====
    public bool isDead = false;

    // ===== 이벤트 및 콜백 =====
    public static event System.Action OnBossDeath;
    private System.Action deathCallback;
    private float lastHitTime = 0f;
    private float hitCooldown = 0.2f;

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeComponents();
        InitializeBossComponents();
        InitializeStateMachine();
    }

    private void Start()
    {
        InitializeHealth();
        InitializeUI();

        // 애니메이션 데이터 초기화
        if (animationData != null)
            animationData.Initialize();

        // FSM 시작 - Idle 상태로 시작
        stateMachine.ChangeState(stateMachine.BossIdleState);

        // 보스 등장 사운드 재생
        if (appearSfx != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(appearSfx);
        }

        Debug.Log($"[Boss] '{bossData?.bossName}' 초기화 완료");
    }

    private void Update()
    {
        if (isDead) return;

        // FSM 업데이트
        stateMachine.HandleInput();
        stateMachine.Update();

        // HP바 업데이트
        UpdateBossHPUI();
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        stateMachine.PhysicsUpdate();
        FreezeVelocity();
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (phaseController != null)
        {
            phaseController.OnPhaseTransitionStart -= OnPhaseTransitionUI;
        }
    }

    #endregion

    #region 초기화

    /// <summary>
    /// 기본 컴포넌트 초기화
    /// </summary>
    private void InitializeComponents()
    {
        rigid = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        nav = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        health = GetComponent<EnemyHealth>();

        if (hitEffectManager == null)
            hitEffectManager = GetComponent<EnemyHitEffectManager>();
    }

    /// <summary>
    /// 보스 전용 컴포넌트 초기화
    /// </summary>
    private void InitializeBossComponents()
    {
        patternExecutor = GetComponent<BossPatternExecutor>();
        if (patternExecutor == null)
        {
            Debug.LogError("[Boss] BossPatternExecutor 컴포넌트가 없습니다!");
        }

        phaseController = GetComponent<BossPhaseController>();
        if (phaseController == null)
        {
            Debug.LogError("[Boss] BossPhaseController 컴포넌트가 없습니다!");
        }
    }

    /// <summary>
    /// BossStateMachine 초기화
    /// </summary>
    private void InitializeStateMachine()
    {
        stateMachine = new BossStateMachine(
            CreateEnemyProxy(),
            bossData,
            phaseController,
            patternExecutor
        );
    }

    /// <summary>
    /// BossStateMachine이 기존 EnemyStateMachine을 상속하므로
    /// Enemy 참조가 필요함 → 프록시용 Enemy 컴포넌트 반환
    /// 같은 오브젝트에 Enemy가 없으면 임시로 추가
    /// </summary>
    private Enemy CreateEnemyProxy()
    {
        Enemy enemyProxy = GetComponent<Enemy>();

        if (enemyProxy == null)
        {
            enemyProxy = gameObject.AddComponent<Enemy>();
            enemyProxy.enabled = false;
        }

        return enemyProxy;
    }

    /// <summary>
    /// 보스 데이터 기반 체력 초기화
    /// </summary>
    private void InitializeHealth()
    {
        if (health != null && bossData != null)
        {
            health.SetMaxHealth(bossData.maxHealth);
            Debug.Log($"[Boss] 체력 설정: {bossData.maxHealth}");
        }
    }

    /// <summary>
    /// 보스 HP바 UI 초기화
    /// - 프리팹이 자체 Canvas를 가진 경우: 독립 인스턴스로 생성
    /// - 프리팹이 Canvas가 없는 경우: 씬의 기존 Canvas 자식으로 생성
    /// </summary>
    private void InitializeUI()
    {
        if (bossHPBarPrefab == null || bossData == null || health == null)
            return;

        // 프리팹이 자체 Canvas를 가지고 있는지 확인
        bool hasOwnCanvas = bossHPBarPrefab.GetComponent<Canvas>() != null;

        if (hasOwnCanvas)
        {
            // 자체 Canvas가 있으면 독립적으로 생성 (부모 없이)
            bossHPBarUI = Instantiate(bossHPBarPrefab);
        }
        else
        {
            // 자체 Canvas가 없으면 씬의 기존 Canvas 자식으로 생성
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Boss] 씬에 Canvas가 없습니다!");
                return;
            }
            bossHPBarUI = Instantiate(bossHPBarPrefab, canvas.transform);
        }

        // HP바 초기화 (이름, 칭호, 체력, 페이즈 경계선)
        bossHPBarUI.Initialize(bossData, health.maxHealth);

        // 페이즈 전환 시 UI 강조 효과 연결
        if (phaseController != null)
        {
            phaseController.OnPhaseTransitionStart += OnPhaseTransitionUI;
        }

        Debug.Log($"[Boss] HP바 UI 생성 완료 (자체 Canvas: {hasOwnCanvas})");
    }
    
    /// <summary>
    /// 페이즈 전환 시 UI 경계선 강조 효과
    /// </summary>
    private void OnPhaseTransitionUI(int newPhaseIndex)
    {
        if (bossHPBarUI != null)
            bossHPBarUI.OnPhaseTransition(newPhaseIndex);
    }

    #endregion

    #region 데미지 처리

    /// <summary>
    /// 근접 무기 충돌 데미지 처리
    /// - 무적 상태일 때 데미지 차단
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (isDead) return;
        if (Time.time - lastHitTime < hitCooldown) return;

        if (other.CompareTag("Melee"))
        {
            // 무적 체크
            if (phaseController != null && phaseController.IsInvincible)
            {
                Debug.Log("[Boss] 무적 상태 - 데미지 차단");
                ShowBlockedEffect();
                return;
            }

            Weapon weapon = other.GetComponent<Weapon>();
            bool isCritical = false;
            int damage = 10;

            if (weapon != null)
            {
                if (PlayerStats.Instance != null)
                    isCritical = PlayerStats.Instance.RollCritical();

                damage = weapon.GetDamage(false);
                if (isCritical)
                    damage = Mathf.RoundToInt(damage * 1.5f);
            }

            Vector3 reactVec = transform.position - other.transform.position;
            reactVec.y = 0f;

            ProcessDamage(damage, reactVec, isCritical);
            lastHitTime = Time.time;
        }
    }

    /// <summary>
    /// 데미지 처리 메인 로직
    /// </summary>
    private void ProcessDamage(int damage, Vector3 reactVec, bool isCritical = false)
    {
        if (health == null) return;

        // 무적 체크 (이중 안전장치)
        if (phaseController != null && phaseController.IsInvincible)
        {
            ShowBlockedEffect();
            return;
        }

        // 체력 감소
        health.TakeDamage(damage);

        // 데미지 텍스트 표시
        ShowDamageText(damage, isCritical);

        // 사망 체크
        if (health.isDead)
        {
            HandleDeath(reactVec);
        }
    }

    /// <summary>
    /// 데미지 텍스트 표시
    /// </summary>
    private void ShowDamageText(int damage, bool isCritical)
    {
        if (DamageTextManager.Instance == null) return;

        Vector3 textPos = hpAnchor != null ? hpAnchor.position : transform.position;
        DamageTextManager.Instance.Show(textPos, damage, isCritical);
    }

    /// <summary>
    /// 무적 시 차단 이펙트 (흰색 깜빡임 등)
    /// </summary>
    private void ShowBlockedEffect()
    {
        // TODO: 무적 시 시각 피드백 (쉴드 이펙트, 텍스트 등)
    }

    #endregion

    #region IDamageable 구현

    /// <summary>
    /// IDamageable - 스킬 등 외부 시스템에서 호출
    /// </summary>
    void IDamageable.TakeDamage(int damage)
    {
        if (phaseController != null && phaseController.IsInvincible)
        {
            ShowBlockedEffect();
            return;
        }

        Vector3 reactVec = transform.forward * -1f;
        ProcessDamage(damage, reactVec, false);
    }

    /// <summary>
    /// IDamageable - 스킬 데미지 (이펙트 포함)
    /// </summary>
    public void ApplySkillDamage(int damage, HitEffectType effectType)
    {
        if (phaseController != null && phaseController.IsInvincible)
        {
            ShowBlockedEffect();
            return;
        }

        Vector3 reactVec = stateMachine.Target != null
            ? transform.position - stateMachine.Target.position
            : -transform.forward;
        reactVec.y = 0f;

        if (hitEffectManager != null)
            hitEffectManager.PlayHitEffect(effectType, transform.position, -reactVec.normalized);

        // 보스 피격 사운드 (무적 상태 통과한 경우만 재생)
        if (hitSfx != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(hitSfx);
        }

        ProcessDamage(damage, reactVec);
    }

    public bool IsDead => isDead;
    public Transform GetTransform() => transform;

    #endregion

    #region 사망 처리

    /// <summary>
    /// 보스 사망 처리
    /// - 패턴 중단 → 사망 연출 → 보상 지급 → 정리
    /// </summary>
    private void HandleDeath(Vector3 reactVec)
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"[Boss] '{bossData?.bossName}' 처치!");

        // 보스 사망 사운드 재생
        if (deathSfx != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(deathSfx);
        }

        // 1. 진행 중인 패턴 즉시 중단
        if (patternExecutor != null && patternExecutor.IsAttacking())
            patternExecutor.StopAttack();

        // 2. AI 비활성화
        if (nav != null) nav.enabled = false;

        // 3. 충돌체 비활성화
        if (boxCollider != null) boxCollider.enabled = false;

        // 4. 사망 애니메이션
        if (animator != null) animator.SetTrigger("doDie");

        // 5. 물리 정지
        if (rigid != null)
        {
            rigid.linearVelocity = Vector3.zero;
            rigid.angularVelocity = Vector3.zero;
            rigid.isKinematic = true;
        }

        // 6. 사망 VFX
        if (deathVFXPrefab != null)
            Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);

        // 7. 보상 지급
        DropBossRewards();

        // 8. 이벤트 및 콜백
        deathCallback?.Invoke();
        OnBossDeath?.Invoke();

        // 9. HP바 숨김
        if (bossHPBarUI != null)
            bossHPBarUI.Hide();

        // 10. 일정 시간 후 오브젝트 제거
        Destroy(gameObject, 3f);
    }

    /// <summary>
    /// 보스 전용 보상 드롭
    /// </summary>
    private void DropBossRewards()
    {
        if (bossData == null) return;

        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.GainEXP(bossData.expReward);
            PlayerStats.Instance.AddCoin(bossData.goldReward);
            PlayerStats.Instance.AddStardust(bossData.stardustReward);
        }

        if (MagnetLootSpawner.Instance != null)
        {
            MagnetLootSpawner.Instance.SpawnFromMonster(
                transform.position,
                bossData.expReward,
                100f,
                100f
            );
        }

        Debug.Log($"[Boss] 보상 지급 - EXP: {bossData.expReward}, 골드: {bossData.goldReward}, 별가루: {bossData.stardustReward}");
    }

    #endregion

    #region UI 업데이트

    /// <summary>
    /// 보스 HP바 매 프레임 업데이트
    /// - BossHPBarUI에 현재 체력 전달 (부드러운 감소는 UI 내부에서 처리)
    /// </summary>
    private void UpdateBossHPUI()
    {
        if (bossHPBarUI != null && health != null)
        {
            bossHPBarUI.UpdateHP(health.curHealth);
        }
    }

    #endregion

    #region 물리 처리

    /// <summary>
    /// 물리 속도 고정 (패턴 실행 중이 아닐 때)
    /// </summary>
    private void FreezeVelocity()
    {
        if (rigid != null && !isDead && !rigid.isKinematic)
        {
            rigid.linearVelocity = Vector3.zero;
            rigid.angularVelocity = Vector3.zero;
        }
    }

    #endregion

    #region 외부 인터페이스

    /// <summary>
    /// 사망 콜백 설정 (StageManager에서 호출)
    /// </summary>
    public void SetDeathCallback(System.Action callback)
    {
        deathCallback = callback;
    }

    /// <summary>
    /// 현재 페이즈 인덱스 반환
    /// </summary>
    public int GetCurrentPhase()
    {
        return phaseController != null ? phaseController.CurrentPhaseIndex : 0;
    }

    /// <summary>
    /// 무적 상태 여부
    /// </summary>
    public bool IsInvincible()
    {
        return phaseController != null && phaseController.IsInvincible;
    }

    #endregion

    #region 디버그

    [ContextMenu("보스 상태 확인")]
    public void LogBossStatus()
    {
        float healthRatio = health != null ? (float)health.curHealth / health.maxHealth : 0f;

        Debug.Log("=== 보스 상태 ===");
        Debug.Log($"이름: {bossData?.bossName}");
        Debug.Log($"체력: {health?.curHealth}/{health?.maxHealth} ({healthRatio:P1})");
        Debug.Log($"페이즈: {GetCurrentPhase()}");
        Debug.Log($"무적: {IsInvincible()}");
        Debug.Log($"패턴 실행 중: {patternExecutor?.IsAttacking()}");
        Debug.Log($"사망: {isDead}");
    }

    private void OnDrawGizmosSelected()
    {
        if (bossData == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, bossData.detectRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, bossData.chasingRange);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, bossData.leashRange);
    }

    #endregion
}
