using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;
using System.Collections;
using UnityEngine.SceneManagement;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    [Header("1. 체력 (HP)")]
    [SerializeField] private int maxHP = 100;
    [SerializeField] private int currentHP;

    [Header("2. 공격력 (Physical)")]
    [SerializeField] private int attackPower = 10;

    [Header("3. 주문력 (Magical)")]
    [SerializeField] private int spellPower = 0; // 마법 데미지 영향

    [Header("4. 공격 속도 (Attack Speed)")]
    [Tooltip("초당 공격 횟수 (예: 1.0 = 1초에 1번, 2.0 = 0.5초에 1번)")]
    [SerializeField] private float attackSpeed = 1.0f;

    [Header("5. 치명타 확률 (Critical)")]
    [Range(0, 100)]
    [SerializeField] private float criticalRate = 5.0f; // 기본 5%

    [Header("6. 쿨타임 감소 (CDR)")]
    [Tooltip("100일 때 스킬 회전율 100% 증가 (쿨타임 절반)")]
    [SerializeField] private float cooldownReduction = 0f;

    [Header("7. 이동 속도 (Move Speed)")]
    [SerializeField] private float moveSpeed = 5.0f;

    [Header("경험치와 레벨")]
    [SerializeField] private int level = 1;
    [SerializeField] private float maxEXP = 100f;
    [SerializeField] private float currentEXP = 0f;
    [SerializeField] private float expMultiplier = 1.2f;

    [Header("재화")]
    [SerializeField] private int coin = 0;
    [SerializeField] private int stardust = 0;

    [Header("Sound")]
    [Tooltip("플레이어가 피격될 때 재생할 효과음")]
    [SerializeField] private AudioClip hitSfx;

    [Header("HUD UI 오브젝트 이름 (각 Stage 씬마다 통일 필요)")]
    [SerializeField] private string hpBarName = "HpBarImage";
    [SerializeField] private string hpTextName = "HpText";
    [SerializeField] private string expBarName = "ExpBarImage";
    [SerializeField] private string levelTextName = "LevelText";
    [SerializeField] private string coinTextName = "CoinText";
    [SerializeField] private string stardustTextName = "StardustText";

    [Header("UI 연결")]
    [SerializeField] private Image hpBarImage;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Image expBarImage;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField] private TextMeshProUGUI stardustText;

    /// <summary>외부 공격력 배율 (축복 등 변형 조건용, 기본 1.0)</summary>
    private float attackMultiplier = 1.0f;

    // 스탯 변경 시 UI 업데이트를 위한 이벤트
    public event Action OnStatsChanged;

    // 프로퍼티 (외부에서 읽기 전용)
    public int MaxHP => maxHP;
    public int CurrentHP => currentHP;
    public int AttackPower => attackPower;
    public int SpellPower => spellPower;
    public float AttackSpeed => attackSpeed;
    public float CriticalRate => criticalRate;
    public float CooldownReduction => cooldownReduction;
    public float MoveSpeed => moveSpeed;

    public int Level => level;
    public float MaxEXP => maxEXP;
    public float CurrentEXP => currentEXP;
    public int Coin => coin;
    public int Stardust => stardust;
    public bool isDead = false;

    private Animator anim;

    /// <summary>
    /// 싱글톤 초기화 + DontDestroyOnLoad 처리
    /// - 첫 인스턴스: Instance 등록 + Player GameObject를 씬 전환 시에도 유지
    /// - 중복 인스턴스(각 Stage 씬에 또 있는 Player): 자기 자신 파괴
    ///   → 메인 메뉴의 Player가 살아남아서 데이터 유지됨
    /// - InitializeStats()는 첫 인스턴스에서만 호출되어 데이터가 리셋되지 않음
    /// </summary>
    public void Awake()
    {
        // 이미 인스턴스가 존재하면 자기 자신(중복 Player) 파괴
        // → 씬 전환 시 새 씬의 Player가 자동 제거되어 메인 메뉴의 Player가 유지됨
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 첫 인스턴스 등록
        Instance = this;

        // Player GameObject를 씬 전환 시에도 유지 (데이터 보존)
        DontDestroyOnLoad(gameObject);

        // 스탯 초기화 (첫 인스턴스에서만 호출됨 — 씬 전환 시 호출 X → 데이터 유지)
        InitializeStats();
    }

    /// <summary>
    /// 씬 로드 콜백 등록 (DDOL된 PlayerStats가 새 씬 진입 시 UI 재바인딩 필요)
    /// </summary>
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// 씬 로드 시 호출 — 1프레임 대기 후 새 씬의 UI GameObject들을 찾아 슬롯 재캐싱
    /// 옛 씬의 UI는 Destroy되어 슬롯이 Missing 상태이므로 매 씬마다 재연결 필요
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 모든 GameObject Start() 완료 대기 후 갱신 (SkillManager와 동일 패턴)
        StartCoroutine(RefindUIAfterFrame());
    }

    /// <summary>
    /// 1프레임 대기 후 UI 슬롯 재캐싱 + 전체 UI 갱신
    /// </summary>
    private IEnumerator RefindUIAfterFrame()
    {
        yield return null;

        RefindHUDReferences();
        UpdateAllUI();
    }

    /// <summary>
    /// 새 씬의 HUD UI GameObject들을 이름 기반으로 찾아 슬롯에 재연결
    /// 인스펙터의 *Name 필드들과 일치하는 이름의 GameObject를 검색
    /// </summary>
    private void RefindHUDReferences()
    {
        var hpBarObj = GameObject.Find(hpBarName);
        if (hpBarObj != null) hpBarImage = hpBarObj.GetComponent<Image>();

        var hpTextObj = GameObject.Find(hpTextName);
        if (hpTextObj != null) hpText = hpTextObj.GetComponent<TextMeshProUGUI>();

        var expBarObj = GameObject.Find(expBarName);
        if (expBarObj != null) expBarImage = expBarObj.GetComponent<Image>();

        var levelTextObj = GameObject.Find(levelTextName);
        if (levelTextObj != null) levelText = levelTextObj.GetComponent<TextMeshProUGUI>();

        var coinTextObj = GameObject.Find(coinTextName);
        if (coinTextObj != null) coinText = coinTextObj.GetComponent<TextMeshProUGUI>();

        var stardustTextObj = GameObject.Find(stardustTextName);
        if (stardustTextObj != null) stardustText = stardustTextObj.GetComponent<TextMeshProUGUI>();
    }

    private void InitializeStats()
    {
        currentHP = maxHP;
        currentEXP = maxEXP; // (테스트용으로 꽉 채워둠)
        level = 1;
        isDead = false;
        anim = GetComponentInChildren<Animator>();
        UpdateAllUI();
    }

    // 쿨타임 감소가 적용된 실제 시간 계산 함수
    public float GetReducedCooldown(float baseCooldown)
    {
        // 공식: 100 / (100 + CDR)
        // 예: CDR 0 -> 1배 (10초)
        // 예: CDR 100 -> 0.5배 (5초)
        float multiplier = 100f / (100f + cooldownReduction);
        return baseCooldown * multiplier;
    }

    // 스킬 타입에 맞는 공격력 반환
    public float GetStat(SkillStatType type)
    {
        switch (type)
        {
            case SkillStatType.Physical: return attackPower;
            case SkillStatType.Magical: return spellPower;
            case SkillStatType.Fixed: return 0f;
            default: return 0f;
        }
    }

    public void LevelUP()
    {
        level++;
        maxEXP *= expMultiplier;

        // 레벨업 보상
        int hpIncrease = 20;

        maxHP += hpIncrease;
        currentHP += hpIncrease;

        if (currentHP > maxHP) currentHP = maxHP;

        attackPower += 3;
        spellPower += 2;

        UpdateAllUI();
        OnStatsChanged?.Invoke(); ; // 스탯창 갱신 신호 보냄
    }

    // --- 스탯 조작 함수들 (아이템/버프용) ---
    public void AddStat(string statName, float amount)
    {
        switch (statName)
        {
            case "HP": maxHP += (int)amount; currentHP += (int)amount; break;
            case "ATK": attackPower += (int)amount; break;
            case "INT": spellPower += (int)amount; break;
            case "AS": attackSpeed += amount; break;
            case "CRI": criticalRate += amount; break;
            case "CDR": cooldownReduction += amount; break;
            case "SPD": moveSpeed += amount; break;
        }
        UpdateAllUI();
        OnStatsChanged?.Invoke();
    }

    public bool RollCritical()
    {
        float roll = UnityEngine.Random.Range(0f, 100f);
        return roll < criticalRate;
    }

    /// <summary>
    /// 데미지 받기 — HP 감소, UI 갱신, 사망 처리 또는 피격 사운드 재생
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        // 피격 추가 데미지 적용
        int bonusDamage = RewardManager.Instance?.GetBonusDamageOnHit() ?? 0;
        int totalDamage = damage + bonusDamage;

        currentHP -= totalDamage;
        currentHP = Mathf.Clamp(currentHP, 0, MaxHP);

        if (hpBarImage != null)
        {
            float fillRatio = (float)currentHP / maxHP;
            hpBarImage.DOFillAmount(fillRatio, 0.3f).SetEase(Ease.OutQuad);
        }
        UpdateHPUI();

        if (CurrentHP <= 0)
        {
            Die();
        }
        else
        {
            // 사망 아닐 때만 피격 사운드 재생
            if (hitSfx != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(hitSfx);
            }
        }
    }

    public void Heal(int amount)
    {
        if (isDead) return;
        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        UpdateHPUI();
        if (hpBarImage != null)
        {
            float fillRatio = (float)currentHP / maxHP;
            hpBarImage.DOFillAmount(fillRatio, 0.3f).SetEase(Ease.OutQuad);
        }
    }

    /// <summary>
    /// 사망 처리 — HP가 0 이하로 떨어졌을 때 TakeDamage()에서 호출
    /// 1. isDead 플래그 ON + 사망 애니메이션 트리거
    /// 2. 사망 애니메이션 재생 시간 대기 후 GameClearUI(Death 모드) 표시
    /// </summary>
    public void Die()
    {
        if (isDead) return;  // 중복 호출 방지

        isDead = true;
        if (anim != null) anim.SetTrigger("doDie");

        // 사망 애니메이션 대기 후 결과 UI 표시
        StartCoroutine(ShowDeathResultAfterDelay(2f));
    }

    /// <summary>
    /// 사망 애니메이션 재생 시간만큼 대기 후 GameClearUI(Death) 표시
    /// </summary>
    /// <param name="delay">대기 시간 (초). 기본 2초 — DeathUI의 showDelay 값 그대로 이식</param>
    private IEnumerator ShowDeathResultAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (GameClearUI.Instance != null)
        {
            GameClearUI.Instance.Show(GameClearUI.GameClearMode.Death);
        }
        else
        {
            Debug.LogError("[PlayerStats] GameClearUI.Instance가 없습니다. " +
                           "메인 메뉴 씬에 GameClearUI GameObject가 배치되어 있는지 확인하세요.");
        }
    }

    /// <summary>
    /// 런 종료 후 초기화 (사망 → 거점 복귀 시 RunManager에서 호출)
    /// - 별가루(영구 재화)만 보존, 나머지 전부 초기값으로 리셋
    /// - 씬 전환 전에 호출해야 함
    /// </summary>
    public void ResetForNewRun(int savedStardust)
    {
        // 체력
        maxHP = 100;
        currentHP = maxHP;

        // 전투 스탯
        attackPower = 10;
        spellPower = 0;
        attackSpeed = 1.0f;
        criticalRate = 5.0f;
        cooldownReduction = 0f;
        moveSpeed = 5.0f;
        attackMultiplier = 1.0f;

        // 레벨/경험치
        level = 1;
        maxEXP = 100f;
        currentEXP = 0f;

        // 재화 (별가루만 복원, 골드는 0)
        coin = 0;
        stardust = savedStardust;

        // 상태 초기화
        isDead = false;

        // UI 전체 갱신
        UpdateAllUI();
        OnStatsChanged?.Invoke();

        Debug.Log($"[PlayerStats] 런 리셋 완료 — 별가루 보존: {savedStardust}");
    }

    public void GainEXP(float amount)
    {
        if (isDead) return;
        currentEXP += amount;
        while (currentEXP >= maxEXP) { currentEXP -= maxEXP; LevelUP(); }
        UpdateEXPUI();
    }

    public void AddCoin(int amount) { coin += amount; UpdateCoinUI(); }
    public bool UseCoin(int amount) { if (coin < amount) return false; coin -= amount; UpdateCoinUI(); return true; }
    public void AddStardust(int amount) { stardust += amount; UpdateStardustUI(); }
    public bool UseStardust(int amount) { if (stardust < amount) return false; stardust -= amount; UpdateStardustUI(); return true; }

    /// <summary>
    /// 최대 HP 증감
    /// </summary>
    public void AddMaxHP(int amount)
    {
        maxHP += amount;
        currentHP += amount;

        if (currentHP > maxHP) currentHP = maxHP;
        if (currentHP < 0) currentHP = 0;
        if (maxHP < 1) maxHP = 1;

        UpdateHPUI();
        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// 공격력 증감
    /// </summary>
    public void AddAttackPower(int amount)
    {
        attackPower += amount;
        if (attackPower < 0) attackPower = 0;

        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// 주문력 증감
    /// </summary>
    public void AddSpellPower(int amount)
    {
        spellPower += amount;
        if (spellPower < 0) spellPower = 0;

        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// 치명타 확률 증감
    /// </summary>
    public void AddCriticalRate(float amount)
    {
        criticalRate += amount;
        criticalRate = Mathf.Clamp(criticalRate, 0f, 100f);

        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// 이동속도 증감
    /// </summary>
    public void AddMoveSpeed(float amount)
    {
        moveSpeed += amount;
        if (moveSpeed < 0.1f) moveSpeed = 0.1f;

        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// 쿨타임 감소 증감
    /// </summary>
    public void AddCooldownReduction(float amount)
    {
        cooldownReduction += amount;
        if (cooldownReduction < 0f) cooldownReduction = 0f;

        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// 공격 속도 증감 (디버프/버프용)
    /// </summary>
    public void AddAttackSpeed(float amount)
    {
        attackSpeed += amount;
        if (attackSpeed < 0.1f) attackSpeed = 0.1f;

        OnStatsChanged?.Invoke();
    }

    private void UpdateHPUI() { if (hpText != null) hpText.text = $"{currentHP:N0} / {maxHP:N0}"; }
    private void UpdateEXPUI() { if (expBarImage != null) { float ratio = currentEXP / maxEXP; expBarImage.DOFillAmount(ratio, 0.5f).SetEase(Ease.OutQuart); } if (levelText != null) levelText.text = level.ToString(); }
    private void UpdateCoinUI() { if (coinText != null) coinText.text = coin.ToString("N0"); }
    private void UpdateStardustUI() { if (stardustText != null) stardustText.text = stardust.ToString("N0"); }
    private void UpdateAllUI() { UpdateHPUI(); UpdateEXPUI(); UpdateCoinUI(); UpdateStardustUI(); }
}
