using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;


public class InventoryManager : MonoBehaviour
{
    [SerializeField] private GameObject slot;
    [SerializeField] private Transform contentTransform;

    [SerializeField] private int slotCount = 50;

    List<GameObject> createSlot = new List<GameObject>();
  
    void Start()
    {
        Createslot();
    }

    
    void Update()
    {
        
    }
    public void Createslot()
    {
        
        for(int i = 0; i < slotCount; i++)
        {
            GameObject newslot = Instantiate(slot, contentTransform);

            createSlot.Add(newslot);

            newslot.name = $"slot_{i}";
        }

    }
}
