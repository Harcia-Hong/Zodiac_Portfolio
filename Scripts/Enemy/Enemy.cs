using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;

/**
 * Enemy 메인 컨트롤러 - FSM 기반으로 완전 리팩토링
 * - AI 로직은 모두 EnemyStateMachine에 위임
 * - 컴포넌트 관리와 데이터만 담당
 * - 기존 코드와의 호환성 유지
 */
public class Enemy : MonoBehaviour, IDamageable
{
    [Header("Enemy Data")]
    [SerializeField] private EnemySO enemyData;
    public EnemySO Data => enemyData; // StateMachine에서 접근용

    [Header("Enemy Type (기존 호환성)")]
    public EnemyType enemyType = EnemyType.Standard;
    public EnemyRank enemyRank = EnemyRank.Normal;

    [Header("레벨 시스템")]
    [SerializeField, Tooltip("적 레벨 (스폰 시 StageEnemySpawner가 설정)")]
    private int level = 1;
    public int Level => level;

    // 원본 스펙 값 (SetLevel 누적 곱셈 방지용)
    private int originalMaxHealth;
    private int originalMeleeDamage;
    private int originalRangedDamage;

    // 감속 디버프 관리 — 화살비 등 스킬에서 호출
    private Coroutine slowDebuffCoroutine;
    private float baseSpeedModifier = 1f;  // 디버프 시작 전 modifier 백업 (보통 1f)
    private bool isSlowDebuffActive = false;

    // 스턴 디버프 — 폭렬 미사일 등 스킬에서 호출
    public bool isStunned = false;
    private Coroutine stunCoroutine;

    [Header("Animation Data")]
    [SerializeField] private EnemyAnimationData animationData;
    public EnemyAnimationData AnimationData => animationData; // StateMachine에서 접근용

    [Header("UI References")]
    public GameObject hpUIPrefab;
    public Transform hpAnchor;

    [Header("Effect References")]
    [SerializeField] private EnemyHitEffectManager hitEffectManager;
    [SerializeField] private EnemyVisualModifier visualModifier;

    [Header("루트 설정")]
    [SerializeField] private int expReward = 15;
    [SerializeField] private float stardustDropChance = 30f;
    [SerializeField] private float goldDropChance = 50f;
    
    public Rigidbody rigid { get; private set; }
    public Collider boxCollider { get; private set; }
    public Material mat { get; private set; }
    public NavMeshAgent nav { get; private set; }
    public Animator animator { get; private set; }
    public EnemyHealth health { get; private set; }

    // UI 관련
    private GameObject hpUIInstance;
    private Slider hpSlider;

    // FSM 핵심
    private EnemyStateMachine stateMachine;

    // 기존 코드와의 호환성을 위한 상태 변수들
    public bool isHit = false;
    public bool isGroggy = false;
    public bool isDead = false;

    // 이벤트 및 콜백
    public static event System.Action OnEnemyDeath;
    private System.Action deathCallback;
    private float lastHitTime = 0f;
    private float hitCooldown = 0.3f;

    #region Unity Lifecycle

    private void Awake()
    {
        // SO 복제 - 다른 Enemy와 데이터 공유 방지 (레벨별 스펙 곱셈을 위해)
        InstantiateEnemyData();

        InitializeComponents();
        InitializeStateMachine();

        if (hitEffectManager == null)
        {
            hitEffectManager = GetComponent<EnemyHitEffectManager>();
        }

        if (visualModifier == null) visualModifier = GetComponent<EnemyVisualModifier>();
    }

    /// <summary>
    /// EnemySO 인스턴스화 + 원본 값 캐싱
    /// - 각 Enemy가 자기만의 SO 복사본을 갖도록 함 (다른 Enemy 영향 방지)
    /// - 원본 maxHealth, meleeDamage, rangedDamage를 캐싱해서 SetLevel이 누적 곱셈 안 되게 함
    /// </summary>
    private void InstantiateEnemyData()
    {
        if (enemyData == null) return;

        // 1. 원본 값 캐싱 (SO 복제 전에)
        originalMaxHealth = enemyData.maxHealth;
        originalMeleeDamage = enemyData.meleeDamage;
        originalRangedDamage = enemyData.rangedDamage;

        // 2. SO 인스턴스화 (이 Enemy만의 복사본)
        enemyData = Instantiate(enemyData);
    }

    private void Start()
    {
        InitializeUI();
        RegisterToManager();

        ApplyRankVisual();

        // FSM 시작 - Idle 상태로 시작
        stateMachine.ChangeState(stateMachine.IdleState);
    }

    private void Update()
    {
        // 죽었으면 FSM 중지
        if (isDead) return;

        if (isStunned)
        {
            UpdateHPUI();
            return;
        }

        // FSM 업데이트 (핵심!)
        stateMachine.HandleInput();
        stateMachine.Update();

        // UI 업데이트
        UpdateHPUI();

        // 기존 호환성을 위한 처리
        UpdateCompatibilityFlags();
    }

    private void FixedUpdate()
    {
        // FSM 물리 업데이트
        stateMachine.PhysicsUpdate();

        // 기존 물리 처리
        FreezeVelocity();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// 컴포넌트 초기화
    /// </summary>
    private void InitializeComponents()
    {
        // 필수 컴포넌트들
        rigid = GetComponent<Rigidbody>();
        boxCollider = GetComponent<Collider>();
        nav = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        health = GetComponent<EnemyHealth>();

        // 렌더러에서 머티리얼 가져오기
        MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
        if (renderer != null)
        {
            mat = renderer.material;
        }

        // 애니메이션 데이터 초기화
        if (animationData != null)
        {
            animationData.Initialize();
        }
        else
        {
            Debug.LogWarning($"[Enemy] {name}에 EnemyAnimationData가 설정되지 않음");
        }

        // 데이터 검증
        if (enemyData == null)
        {
            Debug.LogWarning($"[Enemy] {name}에 EnemySO 데이터가 설정되지 않음");
        }
    }

    /// <summary>
    /// StateMachine 초기화
    /// </summary>
    private void InitializeStateMachine()
    {
        stateMachine = new EnemyStateMachine(this);
    }

    /// <summary>
    /// UI 초기화
    /// </summary>
    private void InitializeUI()
    {
        // HP UI 생성 (기존 로직 유지)
        if (hpUIPrefab != null && hpAnchor != null)
        {
            hpUIInstance = Instantiate(hpUIPrefab);
            hpSlider = hpUIInstance.GetComponentInChildren<Slider>();

            if (hpSlider != null && health != null)
            {
                hpSlider.maxValue = health.maxHealth;
                hpSlider.value = health.curHealth;
            }
        }
    }

    /// <summary>
    /// EnemyManager에 등록
    /// </summary>
    private void RegisterToManager()
    {
        EnemyManager.Register(this);
    }

    /// <summary>
    /// 등급별 시각 효과 적용
    /// </summary>
    private void ApplyRankVisual()
    {
        if (visualModifier != null)
        {
            visualModifier.ApplyRankVisual(enemyRank);
        }
    }

    #endregion

    #region FSM Public Interface

    /// <summary>
    /// 외부에서 강제로 상태 변경 (기존 호환성)
    /// </summary>
    public void ForceSetState(EnemyState newState)
    {
        switch (newState)
        {
            case EnemyState.Idle:
                stateMachine.ChangeState(stateMachine.IdleState);
                break;
            case EnemyState.Trace:
                stateMachine.ChangeState(stateMachine.ChasingState);
                break;
            case EnemyState.Attack:
                stateMachine.ChangeState(stateMachine.AttackState);
                break;
                // Hit, Die 등은 별도 처리
        }
    }

    #endregion

    #region Health & Damage System

    /// <summary>
    /// 데미지 받기 (기존 호환성 유지)
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (isDead) return;
        if (Time.time - lastHitTime < hitCooldown) return;

        if (other.tag == "Melee")
        {
            Weapon weapon = other.GetComponent<Weapon>();

            // 크리티컬 여부 먼저 판정
            bool isCritical = false;
            int damage = 10;

            if (weapon != null)
            {
                // PlayerStats에서 크리티컬 판정
                if (PlayerStats.Instance != null)
                {
                    isCritical = PlayerStats.Instance.RollCritical();
                }

                // 최종 데미지 계산 (무기 데미지 + 공격력, 크리티컬 시 1.5배)
                damage = weapon.GetDamage(false);
                if (isCritical)
                {
                    damage = Mathf.RoundToInt(damage * 1.5f);
                }
            }

            Vector3 reactVec = transform.position - other.transform.position;
            reactVec.y = 0f;

            TakeDamage(damage, reactVec, isCritical);
            lastHitTime = Time.time;
        }
    }

    /// <summary>
    /// 데미지 처리 - EnemyHitReactionSystem과 연동
    /// 히트스탑/카메라 흔들림 피드백을 포함한다.
    /// </summary>
    private void TakeDamage(int damage, Vector3 reactVec, bool isCritical = false)
    {
        if (health != null)
        {
            // 체력 감소
            health.TakeDamage(damage);

            // 데미지 텍스트 표시
            ShowDamageText(damage, isCritical);

            if (health.isDead)
            {
                // 처치 시 강한 피드백
                if (HitStopManager.Instance != null)
                    HitStopManager.Instance.PlayHitFeedback(HitStopManager.FeedbackIntensity.Impact);

                HandleDeath(reactVec);
            }
            else
            {
                // 생존 시 일반/크리티컬에 따라 피드백 강도 분기
                if (HitStopManager.Instance != null)
                {
                    var intensity = isCritical
                        ? HitStopManager.FeedbackIntensity.Heavy
                        : HitStopManager.FeedbackIntensity.Light;
                    HitStopManager.Instance.PlayHitFeedback(intensity);
                }

                var hitReactionSystem = GetComponent<EnemyHitReactionSystem>();
                if (hitReactionSystem != null)
                {
                    hitReactionSystem.AddDamage(damage);
                }
                else
                {
                    StartCoroutine(OnDamage(reactVec, damage));
                }
            }
        }
    }

    private void ShowDamageText(int damage, bool isCritical)
    {
        if (DamageTextManager.Instance != null && hpAnchor != null)
        {
            DamageTextManager.Instance.Show(hpAnchor.position, damage, isCritical);
        }
        else if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.Show(transform.position, damage, isCritical);
        }
    }

    /// <summary>
    /// 기본 피격 반응 처리 (HitReactionSystem이 없을 때 폴백)
    /// </summary>
    private IEnumerator OnDamage(Vector3 reactVec, int damage)
    {
        isHit = true;

        // 넉백 효과
        if (rigid != null)
        {
            rigid.AddForce(reactVec.normalized * 5, ForceMode.Impulse);
        }

        // 기본 피격 시간
        yield return new WaitForSeconds(0.5f);

        isHit = false;
    }

    /// <summary>
    /// 죽음 처리
    /// </summary>
    private void HandleDeath(Vector3 reactVec)
    {
        if (isDead) return;

        isDead = true;

        SpawnMagnetLoot();

        // Epic 등급일 경우 보상 오브젝트 스폰
        SpawnRankReward();

        // 충돌체 비활성화
        if (boxCollider != null) boxCollider.enabled = false;

        // 시각적 효과
        if (mat != null) mat.color = Color.gray;
        gameObject.layer = 10;

        // AI 비활성화
        if (nav != null) nav.enabled = false;

        // 죽음 애니메이션
        if (animator != null) animator.SetTrigger("doDie");

        // 물리 효과
        if (rigid != null)
        {
            rigid.linearVelocity = Vector3.zero;
            rigid.angularVelocity = Vector3.zero;
            //rigid.isKinematic = false;
            rigid.isKinematic = true;
            rigid.useGravity = true;
            rigid.AddForce(reactVec.normalized * 5, ForceMode.Impulse);
        }

        // 콜백 호출
        deathCallback?.Invoke();
        OnEnemyDeath?.Invoke();
        RewardManager.Instance?.OnEnemyKilled();

        // 정리 작업
        CleanupOnDeath();

        // 4초 후 제거
        Destroy(gameObject, 2f);
    }

    private void SpawnMagnetLoot()
    {
        if (MagnetLootSpawner.Instance == null)
        {
            Debug.LogWarning("[Enemy] MagnetLootSpawner.Instance가 없습니다!");
            return;
        }

        MagnetLootSpawner.Instance.SpawnFromMonster(
            transform.position,
            expReward,
            stardustDropChance,
            goldDropChance
        );
    }

    /// <summary>
    /// 등급별 추가 보상 스폰 (Epic 전용)
    /// </summary>
    private void SpawnRankReward()
    {
        // Epic 등급일 때만 보상 오브젝트 스폰
        if (enemyRank == EnemyRank.Epic)
        {
            if (RewardObjectSpawner.Instance != null)
            {
                RewardObjectSpawner.Instance.SpawnRewardObject(transform.position);
                Debug.Log($"[Enemy] {name} (Epic) 보상 오브젝트 스폰 요청");
            }
            else
            {
                Debug.LogWarning("[Enemy] RewardObjectSpawner.Instance가 없습니다!");
            }
        }
    }

    /// <summary>
    /// 적 레벨 설정 - 스폰 시 StageEnemySpawner에서 호출
    /// 레벨에 따라 HP, 공격력에 % 곱연산 적용
    /// 공식: HP = 원본 × (1 + (level-1) × 0.10), 공격력 = 원본 × (1 + (level-1) × 0.05)
    /// 예: Lv5 → HP +40%, 공격력 +20%
    /// </summary>
    /// <param name="newLevel">적용할 레벨 (1 이상)</param>
    public void SetLevel(int newLevel)
    {
        // 레벨 1 미만 방지
        level = Mathf.Max(1, newLevel);

        if (enemyData == null)
        {
            Debug.LogWarning($"[Enemy] {name} - EnemySO가 없어 레벨 적용 불가");
            return;
        }

        // 레벨당 곱셈 계수 계산 (Lv1 = 1.0, Lv2 = 1.10/1.05, ...)
        float hpMultiplier = 1f + (level - 1) * 0.10f;
        float atkMultiplier = 1f + (level - 1) * 0.05f;

        // HP 적용 (원본 값 기준 곱셈 → 누적 방지)
        int newMaxHP = Mathf.RoundToInt(originalMaxHealth * hpMultiplier);
        enemyData.maxHealth = newMaxHP;

        if (health != null)
        {
            health.SetMaxHealth(newMaxHP);
        }

        // 공격력 적용 (원본 값 기준 곱셈)
        enemyData.meleeDamage = Mathf.RoundToInt(originalMeleeDamage * atkMultiplier);
        enemyData.rangedDamage = Mathf.RoundToInt(originalRangedDamage * atkMultiplier);
    }

    #endregion

    #region Compatibility & Helper Methods

    /// <summary>
    /// 기존 코드와의 호환성을 위한 플래그 업데이트
    /// </summary>
    private void UpdateCompatibilityFlags()
    {
        // 필요에 따라 추가 플래그들 업데이트
    }

    /// <summary>
    /// 물리 속도 고정 (기존 로직 유지)
    /// </summary>
    private void FreezeVelocity()
    {
        // 추적 중일 때만 속도 고정 (FSM에서는 불필요할 수 있지만 호환성 유지)
        /*if (rigid != null && stateMachine.CurrentState is EnemyChasingState)
        {
            rigid.linearVelocity = Vector3.zero;
            rigid.angularVelocity = Vector3.zero;
        }*/

        if (rigid != null && !isDead && !rigid.isKinematic)
        {
            rigid.linearVelocity = Vector3.zero;
            rigid.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// HP UI 업데이트
    /// </summary>
    private void UpdateHPUI()
    {
        if (hpUIInstance != null && hpAnchor != null)
        {
            hpUIInstance.transform.position = hpAnchor.position;
            hpUIInstance.transform.rotation = Camera.main.transform.rotation;

            if (hpSlider != null && health != null)
            {
                hpSlider.value = health.curHealth;
            }
        }
    }

    /// <summary>
    /// 죽음 시 정리 작업
    /// </summary>
    private void CleanupOnDeath()
    {
        if (hpUIInstance != null)
        {
            Destroy(hpUIInstance);
        }

        EnemyManager.Unregister(this);
    }

    /// <summary>
    /// 죽음 콜백 설정 (MonsterSpawner에서 호출)
    /// </summary>
    public void SetDeathCallback(System.Action callback)
    {
        deathCallback = callback;
    }

    /// <summary>
    /// 스킬 데미지 받기
    /// </summary>
    public void ApplySkillDamage(int damage, HitEffectType effectType)
    {
        Vector3 reactVec = stateMachine.Target != null
            ? transform.position - stateMachine.Target.position
            : -transform.forward;
        reactVec.y = 0f;

        if (hitEffectManager != null)
        {
            hitEffectManager.PlayHitEffect(effectType, transform.position, -reactVec.normalized);
        }

        TakeDamage(damage, reactVec);
    }

    #endregion

    #region 기존 호환성을 위한 메서드들

    /// <summary>
    /// 일정 시간 동안 강제로 Idle 상태 유지 (기존 호환성)
    /// </summary>
    /// <param name="idleDuration">대기 시간</param>
    public void SetTemporaryIdle(float idleDuration)
    {
        StopAllCoroutines();
        StartCoroutine(IdleAfterDelay(idleDuration));
    }

    /// <summary>
    /// 지정된 시간 후 추적 상태로 복귀
    /// </summary>
    /// <param name="duration">대기 시간</param>
    private IEnumerator IdleAfterDelay(float duration)
    {
        // 강제로 Idle 상태로 변경
        stateMachine.ChangeState(stateMachine.IdleState);

        yield return new WaitForSeconds(duration);

        // 플레이어가 탐지 범위에 있으면 추적 재개
        if (stateMachine.IsPlayerInDetectRange())
        {
            stateMachine.ChangeState(stateMachine.ChasingState);
        }
    }

    /// <summary>
    /// 슬로우 모션 효과 (기존 호환성)
    /// </summary>
    /// <param name="isSlow">슬로우 모션 적용 여부</param>
    public void SetSlowMotion(bool isSlow)
    {
        if (isSlow)
        {
            if (animator != null) animator.speed = 0.2f;
            if (nav != null) nav.speed *= 0.2f;
        }
        else
        {
            if (animator != null) animator.speed = 1f;
            if (nav != null) nav.speed *= 5f;
        }
    }

    #region Debuff System

    /// <summary>
    /// 적에게 일정 시간 이동속도 감속 디버프 적용.
    /// 화살비 등 지속 장판 스킬에서 매 틱 호출 가능 — 이미 진행 중이면 타이머만 갱신.
    /// </summary>
    /// <param name="multiplier">속도 배율 (0.5 = 절반 속도, 1.0 = 정상)</param>
    /// <param name="duration">디버프 지속 시간 (초)</param>
    public void ApplySlowDebuff(float multiplier, float duration)
    {
        if (isDead || stateMachine == null) return;

        // 이미 진행 중인 디버프 코루틴 정리
        if (slowDebuffCoroutine != null)
        {
            StopCoroutine(slowDebuffCoroutine);
        }

        // 첫 적용일 때만 현재 modifier 백업 (재호출 시 백업 유지)
        if (!isSlowDebuffActive)
        {
            baseSpeedModifier = stateMachine.MovementSpeedModifier;
            isSlowDebuffActive = true;
        }

        // 감속 적용 + 타이머 갱신
        stateMachine.MovementSpeedModifier = multiplier;
        slowDebuffCoroutine = StartCoroutine(SlowDebuffRoutine(duration));
    }

    /// <summary>
    /// 지정 시간 후 감속 디버프 해제 — 백업한 modifier로 복구.
    /// </summary>
    private IEnumerator SlowDebuffRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (!isDead && stateMachine != null)
        {
            stateMachine.MovementSpeedModifier = baseSpeedModifier;
        }

        isSlowDebuffActive = false;
        slowDebuffCoroutine = null;
    }

    /// <summary>
    /// 적에게 일정 시간 스턴 디버프 적용.
    /// 스턴 중에는 FSM/이동/공격 모두 정지, 데미지는 받음.
    /// 폭렬 미사일의 "짧은 기절" 같은 효과 구현용.
    /// </summary>
    /// <param name="duration">스턴 지속 시간 (초)</param>
    public void ApplyStunDebuff(float duration)
    {
        if (isDead) return;

        // 이미 진행 중인 스턴 코루틴 정리 (재호출 시 타이머 갱신)
        if (stunCoroutine != null)
        {
            StopCoroutine(stunCoroutine);
        }

        stunCoroutine = StartCoroutine(StunRoutine(duration));
    }

    /// <summary>
    /// 스턴 적용 → 대기 → 해제.
    /// NavMesh 정지 + Animator 정지로 시각적으로도 멈춤.
    /// </summary>
    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;

        // NavMesh 정지 — FSM이 멈춰도 NavMesh 관성으로 미끄러지지 않게
        if (nav != null && nav.enabled && nav.isOnNavMesh)
        {
            nav.isStopped = true;
            nav.velocity = Vector3.zero;
        }

        // 애니메이션 정지 — 멈춰있는 듯한 시각 효과
        if (animator != null)
        {
            animator.speed = 0f;
        }

        yield return new WaitForSeconds(duration);

        isStunned = false;

        // NavMesh 재개 (죽었으면 건드리지 않음)
        if (!isDead && nav != null && nav.enabled && nav.isOnNavMesh)
        {
            nav.isStopped = false;
        }

        // 애니메이션 재개
        if (!isDead && animator != null)
        {
            animator.speed = 1f;
        }

        stunCoroutine = null;
    }

    #endregion

    #endregion

    void IDamageable.TakeDamage(int damage)
    {
        Vector3 reactVec = transform.forward * -1f;
        TakeDamage(damage, reactVec, false);
    }
    public bool IsDead => isDead;
    public Transform GetTransform() => transform;
}
