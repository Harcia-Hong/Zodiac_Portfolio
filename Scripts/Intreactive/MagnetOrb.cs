using UnityEngine;

public class MagnetOrb : MonoBehaviour
{
    [Header("설정")]
    public float flySpeed = 5f;       // 날아가는 기본 속도
    public float acceleration = 10f;  // 점점 빨라지는 가속도
    public string targetTag = "Player"; // 플레이어 태그 이름

    private Transform target;         // 플레이어의 위치
    private bool isTargetFound = false;

    void Start()
    {
        // 1. 씬에서 'Player' 태그를 가진 오브젝트를 찾음
        GameObject playerObj = GameObject.FindGameObjectWithTag(targetTag);

        if (playerObj != null)
        {
            target = playerObj.transform;
            isTargetFound = true;
        }
    }

    void Update()
    {
        if (!isTargetFound || target == null) return;

        // 2. 가속도 적용 (시간이 지날수록 엄청 빨라짐 -> 쾌감!)
        flySpeed += acceleration * Time.deltaTime;

        // 3. 플레이어 방향으로 이동
        transform.position = Vector3.MoveTowards(transform.position, target.position, flySpeed * Time.deltaTime);

        // 4. 플레이어와 거리가 거의 0이 되면 (닿으면)
        if (Vector3.Distance(transform.position, target.position) < 0.5f)
        {
            OnCollected();
        }
    }

    void OnCollected()
    {
        // 여기에 경험치나 자원 올라가는 코드 추가 (예: GameManager.Instance.AddExp(10);)
        Debug.Log("자원 획득!");

        // 나 자신 삭제
        Destroy(gameObject);
    }
}
