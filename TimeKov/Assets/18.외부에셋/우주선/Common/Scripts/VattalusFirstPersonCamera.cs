using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Events;

public class VattalusFirstPersonCamera : MonoBehaviour
{
    //This script handles the player controls

    //CAMERA VARIABLES
    public Camera cameraComponent;

    [Tooltip("Use mouse scroll to change camera FOV. First value should be smaller than the second value")]
    public Vector2 camFovRange = new Vector2(15f, 60f);
    private float fovTarget = 60f;

    //Different player control types
    public enum ControlModeTypes
    {
        Walking,
        Seated,
        Flying
    }
    private ControlModeTypes controlMode;

    // horizontal and vertical rotation speeds
    public float cameraSensitivity = 90;
    private float rotationX = 0f;
    private float rotationY = 0f;

    //MOVEMENT VARIABLES
    CharacterController characterController;
    public float WalkSpeed = 1.5f;
    public float SprintSpeed = 3f;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public float Gravity = 9.8f;
    private float GravVelocity = 0;

    private Transform cameraAnchor; //where is the camera anchored (sometimes we wish to anchor the camera in a different place, for example when lying in bed or sitting in a chair)
    private Vector2 cameraAngleConstraints;
    private GameObject cameraParentGO = null; //an empty gameobject that serves as the target for the camera

    [Header("Camera Shake and Sway")]
    [Tooltip("The amount of camera shake applied to this camera. To disable shaking, set to 0")]
    public float shakeIntensity = 1f;
    [Tooltip("The amount of camera sway applied to this camera. To disable swaying, set to 0")]
    public float swayIntensity = 1f;

    void Start()
    {
        //initialize the camera mode to "Walking"
        SetPlayerControl(ControlModeTypes.Walking);

        //Automatically correct the movement speeds to the default values
        if (WalkSpeed <= 0f) WalkSpeed = 1f;
        if (WalkSpeed > SprintSpeed) SprintSpeed = WalkSpeed * 2f;

        characterController = GetComponent<CharacterController>();
        if (characterController == null) Debug.Log("color=#FF0000>VattalusAssets: [FirstPersonCamera] Missing CharacterController component. Add it this GameObject</color>");

        //Create a game object that serves as a parent for the camera component. We do this in order to free up the camera's localRotation vector so that angle calculations are simpler
        cameraParentGO = new GameObject("CameraParent");
        cameraParentGO.transform.SetParent(transform);
        cameraParentGO.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        cameraParentGO.transform.rotation = transform.rotation;


        //initialize FOV and automatically correct the values
        if (camFovRange == null) camFovRange = new Vector2(15f, 60f);
        else
        {
            //cap fov range to reasonable values
            if (camFovRange.x < 2f) camFovRange.x = 2f;
            if (camFovRange.y < 20f) camFovRange.y = 20f;
            if (camFovRange.y > 120f) camFovRange.y = 120f;
            //make sure smaller value comes first
            if (camFovRange.x > camFovRange.y)
            {
                float tempFOV = camFovRange.y;
                camFovRange.y = camFovRange.x;
                camFovRange.x = tempFOV;
            }
        }

        fovTarget = camFovRange.y;
        if (cameraComponent != null) cameraComponent.fieldOfView = camFovRange.y;

        if (cameraComponent == null) Debug.Log("color=#FF0000>VattalusAssets: Assign camera reference to player controller</color>");
        else
        {
            cameraComponent.transform.SetParent(cameraParentGO.transform);
        }
    }

    void Update()
    {
        /////////////////////////////////////////////////////////
        //CAMERA FOV
        fovTarget += -Input.mouseScrollDelta.y * Time.deltaTime * 300f;
        fovTarget = Mathf.Clamp(fovTarget, camFovRange.x, camFovRange.y);
        cameraComponent.fieldOfView = Mathf.Lerp(cameraComponent.fieldOfView, fovTarget, 10f * Time.deltaTime);

        /////////////////////////////////////////////////////////
        //CAMERA POSITION/ROTATION
        #region Camera Movement

        //read the mouse input
        rotationX += Input.GetAxis("Mouse X") * cameraSensitivity * Time.deltaTime;
        rotationY += Input.GetAxis("Mouse Y") * cameraSensitivity * Time.deltaTime;

        //restrict the camera angle
        rotationY = Mathf.Clamp(rotationY, -90, 90);
        if (controlMode == ControlModeTypes.Seated)
        {
            rotationX = Mathf.Clamp(rotationX, -cameraAngleConstraints.x, cameraAngleConstraints.x);
            rotationY = Mathf.Clamp(rotationY, -cameraAngleConstraints.y, cameraAngleConstraints.y);
        }

        //Now we need to move the camera position and rotation towards the anchor point (when walking it is locked to the player character, when seated it is locked to the seat's designated anchor point)


        //by default, align with the player character
        Vector3 posTarget = transform.position + new Vector3(0f, 0.7f, 0f);
        Quaternion rotTarget = transform.rotation;

        //if we have a separate anchor, move it to that instead
        if (cameraAnchor != null)
        {
            posTarget = cameraAnchor.position;
            rotTarget = cameraAnchor.rotation;
        }


        //smoothly move and rotate the camera parent towards the desired position/rotation
        cameraParentGO.transform.position = Vector3.Lerp(cameraParentGO.transform.position, posTarget, 4f * Time.deltaTime);
        cameraParentGO.transform.rotation = Quaternion.Slerp(cameraParentGO.transform.rotation, rotTarget, 4f * Time.deltaTime);


        //Now let's add the mouse inputs to the camera itself
        if (cameraComponent != null)
        {
            if (cameraAnchor == null)
            {
                //when the player is walking freely, we only need to move the camera on the Y axis (up/down) since X axis movements are applied to the whole character
                cameraComponent.transform.localRotation = Quaternion.AngleAxis(rotationY, Vector3.left);
            }
            else
            {
                //when the player is seated, we need to add both directions of the mouse movement to the camera, since the character itself is immobilized.
                cameraComponent.transform.localRotation = Quaternion.AngleAxis(rotationX, Vector3.up) * Quaternion.AngleAxis(rotationY, Vector3.left);
            }

            //Add the camera shake
            cameraComponent.transform.localPosition = VattalusCameraShake.shakePosOffset * shakeIntensity;
            cameraComponent.transform.localRotation *= Quaternion.Lerp(Quaternion.identity, VattalusCameraShake.shakeRotOffset, shakeIntensity);

            //Add the camera sway
            cameraComponent.transform.localRotation *= VattalusCameraShake.GetSwayRotation(swayIntensity);
        }
        #endregion

        /////////////////////////////////////////////////////////
        //PLAYER MOVEMENT
        #region Player Movement

        if (controlMode == ControlModeTypes.Walking || controlMode == ControlModeTypes.Flying)
        {
            //for walking / flying, apply the X axis (left/right) mouse movements to the entire character
            transform.localRotation = Quaternion.AngleAxis(rotationX, Vector3.up);

            // character movement
            float horizontal = Input.GetAxis("Horizontal") * (Input.GetKey(sprintKey) ? SprintSpeed : WalkSpeed);
            float vertical = Input.GetAxis("Vertical") * (Input.GetKey(sprintKey) ? SprintSpeed : WalkSpeed);
            characterController.Move((transform.right * horizontal + transform.forward * vertical) * Time.deltaTime);

            if (controlMode == ControlModeTypes.Walking)
            {
                // when walking, apply gravity
                if (characterController.isGrounded)
                {
                    GravVelocity = 0;
                }
                else
                {
                    GravVelocity -= Gravity * Time.deltaTime;
                    characterController.Move(new Vector3(0, GravVelocity, 0));
                }
            }

            if (controlMode == ControlModeTypes.Flying)
            {
                //use Q and E keys to climb/descent while flying
                if (Input.GetKey(KeyCode.Q)) { transform.position += transform.up * WalkSpeed * Time.deltaTime; }
                if (Input.GetKey(KeyCode.E)) { transform.position -= transform.up * WalkSpeed * Time.deltaTime; }
            }
        }
        #endregion
    }


    //METHODS TO SET CONTROL MODE
    public void SitDown(VattalusInteractable seatInteractable)
    {
        controlMode = ControlModeTypes.Seated;
        SetPlayerControl(ControlModeTypes.Seated, seatInteractable.seatCameraAnchor, seatInteractable.seatCamAngleConstraints);
    }

    public void StandUp()
    {
        SetPlayerControl(ControlModeTypes.Walking);
    }


    public void SetPlayerControl(ControlModeTypes newMode)
    {
        SetPlayerControl(newMode, null, new Vector2(180, 90));
        rotationX = transform.rotation.eulerAngles.y;
    }

    public void SetPlayerControl(ControlModeTypes newMode, [CanBeNull]Transform camAnchorRef, Vector2 angleConstraints)
    {
        controlMode = newMode;
        cameraAnchor = camAnchorRef;
        cameraAngleConstraints = angleConstraints;

        if (cameraAnchor != null)
        {
            rotationX = 0.0f;
            rotationY = 0.0f;
        }
    }
}
