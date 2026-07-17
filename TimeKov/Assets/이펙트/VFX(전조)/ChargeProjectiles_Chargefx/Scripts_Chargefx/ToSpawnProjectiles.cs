using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToSpawnProjectiles : MonoBehaviour
{
    public GameObject firepoint;
    public List<GameObject> projectilesfx = new List<GameObject>();
    public RotateGunOnMouse rotate;
    private GameObject effectToSpawn;
    private int number = 0;
    void Start()
    {
        effectToSpawn = projectilesfx[0];
    }
    void Update()
    {
        if (Input.GetMouseButtonUp(0))
        {
            SpawnVFX();
        }
        if (Input.GetKeyDown(KeyCode.D))
            Next();

        if (Input.GetKeyDown(KeyCode.A))
            Previous();
    }

    void SpawnVFX()
    {
        GameObject fx;
        if (firepoint != null)
        {
            fx = Instantiate(effectToSpawn, firepoint.transform.position, Quaternion.identity);
            if (rotate != null)
            {
                fx.transform.localRotation = rotate.GetRotation();
            }
        }
        else
        {
            Debug.Log("No Fire Point");
        }
    }

    public void Next()
    {
        number++;
        if (number > projectilesfx.Count)
            number = 0;
        for (int i = 0; i < projectilesfx.Count; i++)
        {
            if (number == i) effectToSpawn = projectilesfx[i];
        }
    }

    public void Previous()
    {
        number--;
        if (number < 0)
            number = projectilesfx.Count;
        for (int i = 0; i < projectilesfx.Count; i++)
        {
            if (number == i) effectToSpawn = projectilesfx[i];
        }
    }
}
