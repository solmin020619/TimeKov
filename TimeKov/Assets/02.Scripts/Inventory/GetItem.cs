using UnityEngine;

public class GetItem : MonoBehaviour
{
    public GameObject invenmana;
    public int insertID;

    public void ItemClick()
    {
        invenmana.GetComponent<InventoryManager>().ChangeIndex(insertID);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
