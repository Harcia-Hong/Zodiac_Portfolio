using System.Collections.Generic;

[System.Serializable]
public class SkillInstance
{
    public SkillData originalData;   // 원본 스킬 데이터
    public int skillLevel;           // 스킬 자체의 강화 레벨

    // [핵심 변경] 단순 Action 리스트가 아니라, SocketItemInstance 리스트를 가집니다.
    // 그래야 아이템 별로 레벨을 따로 관리할 수 있습니다.
    public List<SocketItemInstance> equippedSockets;

    // 생성자
    public SkillInstance(SkillData data)
    {
        this.originalData = data;
        this.skillLevel = 1;
        // 소켓 구멍 3개 (지금은 리스트로 관리, 3개 제한은 로직에서 처리)
        this.equippedSockets = new List<SocketItemInstance>();
    }
}
