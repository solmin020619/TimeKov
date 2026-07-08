using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Script takes settings from a given camera and applies them to the one attached to this GameObject
public class VattalusCopyCamSettings : MonoBehaviour
{
    public Camera cameraToCopy;
    private Camera thisCamera;

    public bool copyFOV = true;
    public bool copyPosition = false;
    public bool copyRotation = false;

    // Start is called before the first frame update
    void Start()
    {
        thisCamera = GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        if (cameraToCopy != null && thisCamera != null)
        {
            if (copyFOV) thisCamera.fieldOfView = cameraToCopy.fieldOfView;
            if (copyPosition) transform.position = cameraToCopy.transform.position;
            if (copyRotation) transform.rotation = cameraToCopy.transform.rotation;
        }
    }
}
