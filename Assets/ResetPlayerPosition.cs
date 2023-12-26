using UnityEngine;

public class ResetPlayerPosition : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<Player>())
        {
            other.gameObject.transform.SetPositionAndRotation(new Vector3 (0,0,0), Quaternion.identity);
        }
    }
}
