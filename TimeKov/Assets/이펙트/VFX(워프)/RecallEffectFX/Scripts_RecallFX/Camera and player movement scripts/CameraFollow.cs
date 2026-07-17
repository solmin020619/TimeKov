using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform targetObject;
    [SerializeField] private float smoothFactor = 0.5f;
    private Vector3 cameraOffset;

    private void Start()
    {
        cameraOffset = transform.position - targetObject.transform.position;
    }

    private void LateUpdate()
    {
        Vector3 newPosition = targetObject.transform.position + cameraOffset;
        transform.position = Vector3.Slerp(transform.position, newPosition, smoothFactor); 
    }
}
