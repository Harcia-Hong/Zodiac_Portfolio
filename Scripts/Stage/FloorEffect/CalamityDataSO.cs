using UnityEngine;

/// <summary>
/// 재앙 데이터 정의 (ScriptableObject)
/// - 전투 층에 일정 확률로 부여되는 환경 위험 요소
/// - 인스펙터에서 재앙 종류별로 에셋을 생성하여 관리
/// - 예: 화염 회오리, 운석 낙하, 독안개, 번개 폭풍 등
/// </summary>
[CreateAssetMenu(fileName = "Calamity_", menuName = "Game/Calamity Data")]
public class CalamityDataSO : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("재앙 이름")]
    public string calamityName = "새 재앙";

    [Tooltip("재앙 설명 (UI 표시용)")]
    [TextArea(2, 4)]
    public string description = "재앙 설명을 입력하세요.";

    [Tooltip("UI 아이콘")]
    public Sprite icon;

    [Header("스폰 설정")]
    [Tooltip("맵에 배치할 위험 오브젝트 프리팹 (회오리, 운석 스포너 등)")]
    public GameObject hazardPrefab;

    [Tooltip("스폰할 개수")]
    [Range(1, 10)]
    public int spawnCount = 3;

    [Tooltip("전투 시작 후 재앙이 활성화되기까지 대기 시간 (초)")]
    public float activationDelay = 2f;

    [Header("보상 보너스 (재앙 감수 대가)")]
    [Tooltip("클리어 골드 배율 (1.0 = 기본, 1.5 = 50% 증가)")]
    public float goldMultiplier = 1.5f;

    [Tooltip("클리어 경험치 배율")]
    public float expMultiplier = 1.3f;

    [Tooltip("추가 보상 풀 (null이면 추가 보상 없음)")]
    public RewardPoolSO bonusRewardPool;

    [Header("난이도 제한")]
    [Tooltip("이 재앙이 등장할 수 있는 최소 챕터")]
    [Range(1, 10)]
    public int minChapter = 1;
}
