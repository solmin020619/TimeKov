using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VattalusSpinScript : MonoBehaviour
{
    public Vector3 SpinSpeed = Vector3.zero;

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(SpinSpeed * Time.deltaTime);
    }
}
