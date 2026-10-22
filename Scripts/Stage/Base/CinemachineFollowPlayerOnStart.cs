using UnityEngine;
using Unity.Cinemachine;  // Cinemachine 3.x. 구버전이면 'using Cinemachine;'

/// <summary>
/// 씬 진입 시 DDOL된 Player를 자동으로 찾아 CinemachineCamera의 Follow/LookAt 타겟으로 설정
/// - 각 Stage 씬의 CinemachineCamera에 부착
/// - Start 시점에 Player 태그 GameObject를 찾아 자동 연결
/// - 인스펙터에서 Target을 직접 잡지 않아도 됨 (씬 전환 시 옛 Player 참조가 깨지는 문제 방지)
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class CinemachineFollowPlayerOnStart : MonoBehaviour
{
    private CinemachineCamera vcam;

    [SerializeField, Tooltip("Player 태그명 (기본 'Player')")]
    private string playerTag = "Player";

    private void Awake()
    {
        vcam = GetComponent<CinemachineCamera>();
    }

    /// <summary>
    /// 씬 진입 후 한 프레임 뒤에 Player를 찾아 카메라 타겟 설정
    /// (Player가 DDOL로 따라오는 경우 Awake 타이밍 차이가 있어 Start에서 처리)
    /// </summary>
    private void Start()
    {
        AttachToPlayer();
    }

    /// <summary>
    /// 씬 안에서 Player 태그 오브젝트를 찾아 Follow/LookAt 설정
    /// </summary>
    private void AttachToPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag(playerTag);
        if (playerObj == null)
        {
            Debug.LogWarning($"[CinemachineFollowPlayerOnStart] '{playerTag}' 태그 GameObject를 찾을 수 없습니다.");
            return;
        }

        if (vcam == null)
        {
            Debug.LogError("[CinemachineFollowPlayerOnStart] CinemachineCamera 컴포넌트가 없습니다.");
            return;
        }

        Transform target = playerObj.transform;
        vcam.Follow = target;
        vcam.LookAt = target;

        Debug.Log($"[CinemachineFollowPlayerOnStart] Player({playerObj.name})에 카메라 연결 완료");
    }
}
