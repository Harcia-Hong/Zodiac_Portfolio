using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SocketItemInstance
{
    public SocketItemData data; // 원본 데이터 (어떤 정수인지)
    public int level;           // 이 정수의 현재 강화 레벨

    public SocketItemInstance(SocketItemData data)
    {
        this.data = data;
        this.level = 1; // 획득 시 1레벨부터 시작 (혹은 드랍 시 결정)
    }
}
