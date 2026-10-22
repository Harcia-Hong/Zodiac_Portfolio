using UnityEngine;
using System.Collections;

/// <summary>
/// 직선 경로 순차 스폰 액션 — 마우스 방향으로 prefab N개를 일정 간격으로 순차 생성.
/// 
/// 동작:
///   1) 마우스 방향 계산 + 캐릭터 회전
///   2) 시전 사운드 재생
///   3) castDelay 대기 (애니메이션 발동 타이밍 맞춤)
///   4) N개 prefab 순차 스폰 (spawnInterval 간격, 각 스폰마다 사운드)
///   5) 각 스폰 위치 = 캐릭터 + direction × (startOffset + i × spawnSpacing)
/// 
/// 활용:
///   - 얼음 기둥 직선 (IcePillar prefab)
///   - 가시 솟구침, 화염 폭발 직선 등에 재활용 가능
/// 
/// 강화 확장 자리 (불씨로 비틀 가능):
///   - 개수/간격/속도 (인스펙터)
///   - 분기 발사 (좌우/Y자) — 향후 옵션 추가
/// 
/// 의존성:
///   - 현재 IcePillar에 데미지 전달. 다른 컴포넌트 추가 시 GetComponent 분기 추가 필요.
/// </summary>
[CreateAssetMenu(menuName = "Skill/Action/Line Spawn")]
public class LineSpawnAction : SkillAction
{
    [Header("프리팹")]
    [Tooltip("스폰할 프리팹. IcePillar 같은 자체 동작 컴포넌트 포함 권장.")]
    public GameObject prefab;

    [Header("배치")]
    [Tooltip("순차 스폰 개수.")]
    public int spawnCount = 5;

    [Tooltip("프리팹 간 거리 (m). 1.5~2.0 권장.")]
    public float spawnSpacing = 1.5f;

    [Tooltip("캐릭터에서 첫 프리팹까지 거리 (m). 자기 위치에 안 솟구치도록.")]
    public float startOffset = 1.5f;

    [Tooltip("순차 스폰 간격 (초). 시각적 '투투투툭' 표현. 0.08~0.15 권장.")]
    public float spawnInterval = 0.1f;

    [Header("애니메이션")]
    [Tooltip("시전 시 호출할 Animator 트리거 이름. 비우면 모션 없음.")]
    public string animTrigger = "doIceLine";

    [Header("시전 딜레이")]
    [Tooltip("실제 스폰 시작까지 대기 (초). 애니메이션 발동 프레임에 맞추는 용도. 0이면 즉시 스폰.")]
    public float castDelay = 0.85f;

    [Header("사운드")]
    [Tooltip("시전 시작 시 1회 재생.")]
    public AudioClip castSfx;

    [Tooltip("각 스폰마다 재생 (투투투툭 사운드 효과).")]
    public AudioClip spawnSfx;

    /// <summary>
    /// 메인 실행 코루틴.
    /// 시전 사운드 → 딜레이 → N개 순차 스폰.
    /// 이동 잠금은 SkillData.blockMovement = true로 처리 (이 액션은 신경 X).
    /// </summary>
    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        PlayerController pc = user.GetComponent<PlayerController>();
        if (pc == null)
        {
            Debug.LogError("[LineSpawnAction] PlayerController 없음");
            yield break;
        }

        // 마우스 방향 계산 (XZ 평면, Y는 캐릭터 높이 유지)
        Vector3 mouseWorldPos = pc.GetMouseWorldPosition();
        Vector3 spawnOrigin = user.transform.position;
        Vector3 toMouse = new Vector3(mouseWorldPos.x, spawnOrigin.y, mouseWorldPos.z) - spawnOrigin;
        Vector3 direction = toMouse.sqrMagnitude > 0.001f ? toMouse.normalized : user.transform.forward;

        // 캐릭터 회전
        user.transform.rotation = Quaternion.LookRotation(direction);

        // 애니메이션 트리거
        if (!string.IsNullOrEmpty(animTrigger))
        {
            Animator anim = user.GetComponentInChildren<Animator>();
            if (anim != null) anim.SetTrigger(animTrigger);
        }

        // 시전 사운드
        PlaySfx(castSfx);

        // 시전 딜레이 (애니메이션 발동 프레임까지)
        if (castDelay > 0f) yield return new WaitForSeconds(castDelay);

        // 데미지 결정 — damageOverride > 0이면 그 값 사용, 아니면 prefab 자체 기본값(0 전달)
        int damagePerSpawn = (damageOverride > 0) ? Mathf.RoundToInt(damageOverride) : 0;

        // 순차 스폰
        for (int i = 0; i < spawnCount; i++)
        {
            float distance = startOffset + i * spawnSpacing;
            Vector3 spawnPos = spawnOrigin + direction * distance;

            SpawnPrefab(spawnPos, direction, damagePerSpawn);
            PlaySfx(spawnSfx);

            // 마지막이 아니면 간격 대기
            if (i < spawnCount - 1)
                yield return new WaitForSeconds(spawnInterval);
        }
    }

    /// <summary>프리팹 1개 생성 + IcePillar에 데미지 전달.</summary>
    private void SpawnPrefab(Vector3 pos, Vector3 dir, int damage)
    {
        if (prefab == null) return;

        Quaternion rot = (dir != Vector3.zero) ? Quaternion.LookRotation(dir) : Quaternion.identity;
        GameObject instance = Instantiate(prefab, pos, rot);

        // IcePillar 같은 자체 데미지 시스템에 데미지 전달
        // damage > 0일 때만 (0이면 prefab의 인스펙터 기본값 사용됨)
        if (damage > 0)
        {
            IcePillar pillar = instance.GetComponent<IcePillar>();
            if (pillar != null) pillar.Initialize(damage);
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(clip);
    }
}
