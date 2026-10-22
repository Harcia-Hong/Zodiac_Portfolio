using UnityEngine;

public class TestUI : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (KnowledgeArchiveUI.Instance != null)
                KnowledgeArchiveUI.Instance.Open();
        }
    }
}
