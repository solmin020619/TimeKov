using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VattalusFakeSkyboxMovement : MonoBehaviour
{
    //Since actual spaceship movement is not implemented in this demo, we move the entire environment instead. We basically read the movement inputs from the spaceship and mirror the movements on the environment instead.
    //It is important that all other cameras should have the ClearFlags set to "Don't clear" or "Depth only" and they should have a Depth value greater than the env camera.

    public Transform elementsParent;
    private Quaternion elementsInitRot;
    private Rigidbody rb;

    void Start()
    {
        //Check rigidbody, add one if not present. The rigidbody is required to simulate realistic rotations
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 100f;
            rb.angularDamping = 0.05f;
        }
        rb.useGravity = false;

        //also add a sphere collider if missing
        if (gameObject.GetComponent<SphereCollider>() == null)
        {
            SphereCollider col = gameObject.AddComponent<SphereCollider>();
            col.radius = 0.5f;
        }


        //save the initial rotation of the light source, so it doest get reset when the scene starts
        if (elementsParent != null)
            elementsInitRot = elementsParent.rotation;
    }


    void LateUpdate()
    {
        //rotate the light source
        if (elementsParent != null)
            elementsParent.rotation = Quaternion.Inverse(transform.rotation) * elementsInitRot;
    }

    public void MoveFakeSkybox(float pitchInput, float pitchThrust, float yawInput, float yawThrust, float rollInput, float rollThrust)
    {
        rb.AddRelativeTorque(pitchInput * -pitchThrust * Time.deltaTime, yawInput * yawThrust * Time.deltaTime, rollInput * -rollThrust * Time.deltaTime);
    }
}
