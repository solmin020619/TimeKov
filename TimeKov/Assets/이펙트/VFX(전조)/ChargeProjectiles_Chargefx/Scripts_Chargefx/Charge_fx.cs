using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Charge_fx : MonoBehaviour
{
    public GameObject firepoint;
    public List<GameObject> chargefx = new List<GameObject>();
    public RotateGunOnMouse rotate;
    private GameObject chargeBefore;
    private int number = 0;
    GameObject charge;



    
    void Start()
    {
        chargeBefore = chargefx[0];
    }

    
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            charge = Instantiate(chargeBefore, firepoint.transform.position, Quaternion.identity);
            if (rotate != null)
            {
                charge.transform.localRotation = rotate.GetRotation();
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            Destroy(charge);
        }

        if (Input.GetKeyDown(KeyCode.D))
            Next();

        if (Input.GetKeyDown(KeyCode.A))
            Previous();
    }

    public void Next()
    {
        number++;

        if (number > chargefx.Count)
            number = 0;

        for (int i = 0; i < chargefx.Count; i++)
        {
            if (number == i) chargeBefore = chargefx[i];

        }
    }

    public void Previous()
    {
        number--;

        if (number < 0)
            number = chargefx.Count;

        for (int i = 0; i < chargefx.Count; i++)
        {
            if (number == i) chargeBefore = chargefx[i];

        }
    }


}
