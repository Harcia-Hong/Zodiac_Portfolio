using UnityEngine;

/// <summary>
/// 금서의 잔재 등급별 데이터
/// - 해당 등급에서 나올 수 있는 디버프 목록
/// - 즉시 보상 수치 (스탯 강화 기준)
/// </summary>
[System.Serializable]
public class CurseGradeData
{
    [Tooltip("등급 표시 이름 (찢어진 금서, 읽힌 금서 등)")]
    public string gradeName = "찢어진 금서";

    [Tooltip("등급 설명")]
    [TextArea(1, 3)]
    public string gradeDescription = "약한 저주를 받고 약간의 보상을 얻습니다.";

    [Tooltip("카드 테두리 색상")]
    public Color gradeColor = Color.white;

    [Tooltip("이 등급에서 랜덤으로 적용될 디버프 목록")]
    public DebuffEntrySO[] debuffPool;

    [Header("즉시 보상 — 스탯 강화 수치")]
    [Tooltip("이 등급 선택 시 제공되는 스탯 강화 풀 (StatBoostPoolSO)")]
    public StatBoostPoolSO rewardBoostPool;

    [Tooltip("보상 스탯 강화의 최소 등급 (이 등급 이상만 나옴)")]
    public StatBoostGrade minRewardGrade = StatBoostGrade.Common;
}

/// <summary>
/// 금서의 잔재 — 전체 데이터 ScriptableObject
/// - 등급별(I/II/III) 디버프 풀 + 보상 설정
/// - "덮어둔다"(무시) 옵션 포함
/// 
/// 흐름:
///   F키 → 카드 UI에 등급 3~4장 표시
///   → 선택하면 디버프 랜덤 1개 적용 + 보상(스탯 강화) 즉시 지급
///   → "덮어둔다" 선택하면 아무 일도 안 일어남
/// </summary>
[CreateAssetMenu(fileName = "CursedTomeData", menuName = "Game/Cursed Tome Data")]
public class CursedTomeDataSO : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("오브젝트 표시 이름")]
    public string tomeName = "금서의 잔재";

    [Tooltip("오브젝트 설명")]
    [TextArea(1, 3)]
    public string tomeDescription = "위험한 지식의 파편. 힘을 얻지만 대가가 따릅니다.";

    [Header("무시 옵션")]
    [Tooltip("덮어둔다 카드 이름")]
    public string noneName = "덮어둔다";

    [Tooltip("덮어둔다 설명")]
    public string noneDescription = "금서를 무시하고 지나갑니다.";

    [Tooltip("덮어둔다 카드 색상")]
    public Color noneColor = Color.gray;

    [Header("등급 데이터 (I / II / III 순서)")]
    [Tooltip("배열 크기 3 — [Grade_I, Grade_II, Grade_III]")]
    public CurseGradeData[] grades;

    /// <summary>
    /// 등급 인덱스로 데이터 반환 (0=I, 1=II, 2=III)
    /// </summary>
    public CurseGradeData GetGradeData(int gradeIndex)
    {
        if (grades == null || gradeIndex < 0 || gradeIndex >= grades.Length)
            return null;

        return grades[gradeIndex];
    }

    /// <summary>
    /// 해당 등급의 디버프 풀에서 랜덤 1개 반환
    /// </summary>
    public DebuffEntrySO GetRandomDebuff(int gradeIndex)
    {
        CurseGradeData data = GetGradeData(gradeIndex);
        if (data == null || data.debuffPool == null || data.debuffPool.Length == 0)
            return null;

        int randomIndex = Random.Range(0, data.debuffPool.Length);
        return data.debuffPool[randomIndex];
    }

    /// <summary>
    /// 총 등급 수 반환
    /// </summary>
    public int GradeCount => grades != null ? grades.Length : 0;
}
