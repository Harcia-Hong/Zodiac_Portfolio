using UnityEngine;
using DG.Tweening;

/// <summary>
/// 파괴 가능한 오브젝트
/// 공격받으면 내구도 감소, 파괴 시 루트 스폰
/// </summary>
public class BreakableObject : MonoBehaviour, IDamageable
{
    #region Enums
    /// <summary>
    /// 오브젝트 타입 정의
    /// </summary>
    public enum ObjectType
    {
        Stardust,   // 별빛 오브젝트 (2대)
        Gold        // 골드 오브젝트 (1대)
    }
    #endregion

    #region Settings
    [Header("오브젝트 설정")]
    [SerializeField] private ObjectType objectType = ObjectType.Gold;

    [Header("내구도")]
    [SerializeField] private int maxHitCount = 1;
    private int currentHitCount;

    [Header("드롭 설정")]
    [SerializeField] private int dropAmount = 5;

    [Header("피격 연출")]
    [SerializeField] private float hitShakeDuration = 0.2f;
    [SerializeField] private float hitShakeStrength = 0.3f;

    [Header("파괴 연출")]
    [SerializeField] private float destroyDuration = 0.5f;
    [SerializeField] private GameObject destroyVFX;

    [Header("비주얼")]
    [SerializeField] private Renderer objectRenderer;
    #endregion

    #region State
    private bool isDestroyed = false;
    private Vector3 originalPosition;
    private Vector3 originalScale;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        InitializeObject();
    }

    private void OnValidate()
    {
        // 에디터에서 타입 변경 시 내구도 자동 설정
        SetDefaultHitCount();
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 오브젝트 초기화
    /// </summary>
    private void InitializeObject()
    {
        SetDefaultHitCount();
        currentHitCount = maxHitCount;

        originalPosition = transform.position;
        originalScale = transform.localScale;

        if (objectRenderer == null)
        {
            objectRenderer = GetComponentInChildren<Renderer>();
        }
    }

    /// <summary>
    /// 타입에 따른 기본 내구도 설정
    /// </summary>
    private void SetDefaultHitCount()
    {
        maxHitCount = objectType switch
        {
            ObjectType.Stardust => 2,   // 별빛: 2대
            ObjectType.Gold => 1,       // 골드: 1대
            _ => 1
        };
    }
    #endregion

    /// <summary>
    /// 피격 처리 (외부에서 호출)
    /// </summary>
    public void TakeHit()
    {
        if (isDestroyed) return;

        currentHitCount--;

        // 피격 연출
        PlayHitEffect();

        // 파괴 체크
        if (currentHitCount <= 0)
        {
            BreakObject();
        }
    }

    #region IDamageable Implementation
    /// <summary>
    /// 데미지 받기 (데미지 값 무시, 1히트로 처리)
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (isDestroyed) return;
        TakeHit();
    }

    /// <summary>
    /// 스킬 데미지 받기
    /// </summary>
    public void ApplySkillDamage(int damage, HitEffectType effectType)
    {
        if (isDestroyed) return;
        TakeHit();
    }

    /// <summary>
    /// 파괴 상태 확인
    /// </summary>
    public bool IsDead => isDestroyed;

    /// <summary>
    /// Transform 반환
    /// </summary>
    public Transform GetTransform() => transform;
    #endregion

    #region Hit Effect
    /// <summary>
    /// 피격 시 흔들림 연출
    /// </summary>
    private void PlayHitEffect()
    {
        // 기존 트윈 취소
        transform.DOKill();

        // 흔들림 효과
        transform.DOShakePosition(hitShakeDuration, hitShakeStrength, 10, 90f, false, true)
            .OnComplete(() =>
            {
                // 원래 위치로 복귀
                transform.position = originalPosition;
            });

        // 색상 깜빡임 (선택)
        if (objectRenderer != null)
        {
            objectRenderer.material.DOColor(Color.white, 0.05f)
                .SetLoops(2, LoopType.Yoyo);
        }
    }
    #endregion

    #region Break Object
    /// <summary>
    /// 오브젝트 파괴
    /// </summary>
    private void BreakObject()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        // 트윈 정리
        transform.DOKill();

        // 루트 스폰
        SpawnLoot();

        // 파괴 이펙트
        if (destroyVFX != null)
        {
            GameObject vfx = Instantiate(destroyVFX, transform.position, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        // 서서히 사라지는 연출
        PlayDestroyAnimation();
    }

    /// <summary>
    /// 타입에 따른 루트 스폰
    /// </summary>
    private void SpawnLoot()
    {
        if (MagnetLootSpawner.Instance == null)
        {
            Debug.LogError("[BreakableObject] MagnetLootSpawner.Instance가 없습니다!");
            return;
        }

        switch (objectType)
        {
            case ObjectType.Stardust:
                MagnetLootSpawner.Instance.SpawnStardust(transform.position, dropAmount);
                break;

            case ObjectType.Gold:
                MagnetLootSpawner.Instance.SpawnGold(transform.position, dropAmount);
                break;
        }
    }

    /// <summary>
    /// 파괴 시 사라지는 애니메이션
    /// </summary>
    private void PlayDestroyAnimation()
    {
        Sequence destroySequence = DOTween.Sequence();

        // 크기 줄어들면서
        destroySequence.Append(transform.DOScale(Vector3.zero, destroyDuration)
            .SetEase(Ease.InBack));

        // 투명해지기 (머티리얼이 투명 지원 시)
        if (objectRenderer != null)
        {
            destroySequence.Join(objectRenderer.material.DOFade(0f, destroyDuration));
        }

        // 완료 후 삭제
        destroySequence.OnComplete(() =>
        {
            Destroy(gameObject);
        });
    }
    #endregion

    #region Cleanup
    private void OnDestroy()
    {
        transform.DOKill();
    }
    #endregion

    #region Editor Helpers
    /// <summary>
    /// 기즈모로 오브젝트 타입 표시
    /// </summary>
    private void OnDrawGizmos()
    {
        Color gizmoColor = objectType switch
        {
            ObjectType.Stardust => new Color(0.4f, 0.8f, 1f, 0.5f),  // 푸른색
            ObjectType.Gold => new Color(1f, 0.85f, 0.2f, 0.5f),     // 금색
            _ => Color.white
        };

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(transform.position, Vector3.one);
    }
    #endregion
}
