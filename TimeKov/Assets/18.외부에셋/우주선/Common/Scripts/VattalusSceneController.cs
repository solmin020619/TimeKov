using System;
using Unity.Collections;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


// This script acts as a central hub for all other important scripts in the demo scene and can be accessed easily from all other scripts
public class VattalusSceneController : VattalusUnitySingleton<VattalusSceneController>
{
    public bool lockCursorToWindow = true;

    public bool ExteriorOnlyDemoMode = false;

    //Events
    [HideInInspector]
    public UnityEvent<CameraModes> CameraChanged;

    [Header("Player / Camera / Spaceship")]
    //Player related variables
    public VattalusFirstPersonCamera firstPersonController;

    //Different camera behaviour types
    public enum CameraModes
    {
        Player,
        ShipOrbit
    }
    private CameraModes cameraMode;
    public CameraModes GetCamMode { get { return cameraMode; } }

    //orbit camera that rotates around the spaceship
    public VattalusOrbitCamera orbitCameraController;

    //Spaceship related variables
    public VattalusSpaceshipController spaceshipController;

    //INTERACTION VARIABLES
    [Header("Interaction Variables")]
    public KeyCode interactionKey = KeyCode.E; // what key is used to interact with objects
    public KeyCode standUpKey = KeyCode.X; //key used to stand up from seats
    public float interactionRange = 3f;
    [HideInInspector]
    public VattalusInteractable lookingAtInteractable = null;
    [HideInInspector]
    public VattalusInteractable currentlyOccupiedSeat = null; //reference to the seat the player is currently sitting in

    [Header("Inputs")]
    public KeyCode cameraKey = KeyCode.C;
    public KeyCode hideUIKey = KeyCode.Tab;

    [Header("UI References")]
    public GameObject reticle;
    public GameObject KeyPromptsParent;

    [Space]
    // references to the UI elements of key prompts
    public VattalusKeyPrompt interactPrompt;
    public VattalusKeyPrompt standUpPrompt;

    [Space]
    //ship controls prompts
    public VattalusKeyPrompt hyperspacePrompt;
    public VattalusKeyPrompt hologramPrompt;
    public VattalusKeyPrompt landingGearPrompt;
    public VattalusKeyPrompt rampPrompt;

    [Space]
    public VattalusKeyPrompt shipControlsPrompt;
    public Text pitchDownPrompt;
    public Text pitchUpPrompt;
    public Text yawLeftPrompt;
    public Text yawRightPrompt;
    public Text rollLeftPrompt;
    public Text rollRightPrompt;
    public Text acceleratePrompt;
    public Text deceleratePrompt;

    [Space]
    public VattalusKeyPrompt cameraPrompt;
    public VattalusKeyPrompt hideUIPrompt;

    public Text fpsCounter;

    void OnApplicationFocus(bool hasFocus)
    {
        // If you alt-tabbed, you come back, lock again
        if (hasFocus)
        {
            if (lockCursorToWindow) Cursor.lockState = CursorLockMode.Locked;   // Locks you to Game view
            Cursor.visible = false;
        }
    }

    void Start()
    {
        if (lockCursorToWindow) Cursor.lockState = CursorLockMode.Locked;   // Locks you to Game view
        Cursor.visible = false; // hides the cursor

        Application.targetFrameRate = 120;
        //FPS counter
        StartCoroutine(FpsCounterCoroutine());

        //Check important references and throw warnings for you in case you forgot something
        if (firstPersonController == null) Debug.Log("color=#FF0000>VattalusAssets: [SceneController] Missing reference to first person camera controller</color>");
        if (orbitCameraController == null) Debug.Log("color=#FF0000>VattalusAssets: [SceneController] Missing reference to orbit camera controller</color>");
        if (spaceshipController == null) Debug.Log("color=#FF0000>VattalusAssets: [SceneController] Missing reference to the ship controller</color>");

        if (interactPrompt == null) Debug.Log("<color=#FF0000>VattalusAssets: [SceneController] Missing reference to UI component: Interaction key prompt</color>");
        if (standUpPrompt == null) Debug.Log("<color=#FF0000>VattalusAssets: [SceneController] Missing reference to UI component: Stamp up key prompt</color>");
        if (hyperspacePrompt == null) Debug.Log("<color=#FF0000>VattalusAssets: [SceneController] Missing reference to UI component: Hyperspace key prompt</color>");
        if (hologramPrompt == null) Debug.Log("<color=#FF0000>VattalusAssets: [SceneController] Missing reference to UI component: Hologram key prompt</color>");
        if (landingGearPrompt == null) Debug.Log("<color=#FF0000>VattalusAssets: [SceneController] Missing reference to UI component: Landing gear key prompt</color>");
        if (rampPrompt == null) Debug.Log("<color=#FF0000>VattalusAssets: [SceneController] Missing reference to UI component: ramp key prompt</color>");
        if (cameraPrompt == null) Debug.Log("<color=#FF0000>VattalusAssets: [SceneController] Missing reference to UI component: camera key prompt</color>");

        if (shipControlsPrompt == null) Debug.Log("<color=#FF0000>VattalusAssets: Missing reference to UI component: ship controls prompts</color>");


        //Initialize the key prompt values on the UI
        if (spaceshipController != null)
        {
            if (rampPrompt != null)
            {
                rampPrompt.gameObject.SetActive(spaceshipController.ramp != null);
                rampPrompt.UpdateKeyPromptTexts("Ramp", spaceshipController.rampKey.ToString());
            }

            if (landingGearPrompt != null)
            {
                landingGearPrompt.gameObject.SetActive(spaceshipController.landingGearList != null && spaceshipController.landingGearList.Count > 0);
            }

            if (hyperspacePrompt != null)
            {
                hyperspacePrompt.gameObject.SetActive(spaceshipController.hyperspaceEffectController != null);
                hyperspacePrompt.UpdateKeyPromptTexts("Hyperspace", spaceshipController.hyperspaceKey.ToString());
            }

            if (hologramPrompt != null)
            {
                hologramPrompt.UpdateKeyPromptTexts("Hologram", spaceshipController.hologramKey.ToString());
                hologramPrompt.gameObject.SetActive(spaceshipController.hologram != null);
            }
            if (landingGearPrompt != null) landingGearPrompt.UpdateKeyPromptTexts("Landing Gear", spaceshipController.landingGearKey.ToString());

            if (pitchDownPrompt != null) pitchDownPrompt.text = spaceshipController.pitchDown.ToString();
            if (pitchUpPrompt != null) pitchUpPrompt.text = spaceshipController.pitchUp.ToString();
            if (yawLeftPrompt != null) yawLeftPrompt.text = spaceshipController.yawLeft.ToString();
            if (yawRightPrompt != null) yawRightPrompt.text = spaceshipController.yawRight.ToString();
            if (rollLeftPrompt != null) rollLeftPrompt.text = spaceshipController.rollLeftInputKey.ToString();
            if (rollRightPrompt != null) rollRightPrompt.text = spaceshipController.rollRightInputKey.ToString();
            if (acceleratePrompt != null) acceleratePrompt.text = spaceshipController.accelerateInputKey.ToString();
            if (deceleratePrompt != null) deceleratePrompt.text = spaceshipController.decelerateInputKey.ToString();


        }

        if (spaceshipController != null && orbitCameraController != null && orbitCameraController.targetTransform == null) orbitCameraController.targetTransform = spaceshipController.transform;
        if (cameraPrompt != null) cameraPrompt.UpdateKeyPromptTexts("Camera", cameraKey.ToString());
        if (hideUIPrompt != null) hideUIPrompt.UpdateKeyPromptTexts("Hide Controls", hideUIKey.ToString());

        if (ExteriorOnlyDemoMode)
        {
            SetCameraMode(CameraModes.ShipOrbit);
            if (spaceshipController != null) spaceshipController.enableMovement = true;
        }
        else
        {
            SetCameraMode(CameraModes.Player);
        }

        ShowUI(false);
        ShowUI(true);
    }

    void Update()
    {
        //Interaction with interactable ojects
        lookingAtInteractable = CheckIfLookingAtInteractable();

        //when looking at an interactable and pressing the interaction button
        if (lookingAtInteractable != null && lookingAtInteractable.CanInteract && Input.GetKeyDown(interactionKey))
        {
            if (lookingAtInteractable.isSeat)
            {
                //if the seat is unoccupied tell player to sit down (unless the player is already seated). If its occupied, tell player to stand up 
                if (currentlyOccupiedSeat == null && lookingAtInteractable.isActivated == false)
                {
                    SitPlayerDown(lookingAtInteractable);
                }
                else
                {
                    StandPlayerUp();
                }
            }
            else
            {
                InteractWith(lookingAtInteractable);
            }
        }

        //When pressing the 'stand up' key, check if we are currently sitting down, then tell player to stand up
        if (Input.GetKeyDown(standUpKey) && currentlyOccupiedSeat != null && currentlyOccupiedSeat.IsAnimating == false)
        {
            StandPlayerUp();
        }


        //When pressing the camera key, switch between camera modes
        if (Input.GetKeyDown(cameraKey) && spaceshipController != null && spaceshipController.PlayerInPilotSeat && orbitCameraController != null)
        {
            SetCameraMode(cameraMode == CameraModes.Player ? CameraModes.ShipOrbit : CameraModes.Player);
        }

        #region UI
        //When seated, always show a special UI hint to stand up
        standUpPrompt.gameObject.SetActive(currentlyOccupiedSeat != null);
        interactPrompt.gameObject.SetActive(lookingAtInteractable != null);


        //Update UI prompts when sitting in a seat
        if (currentlyOccupiedSeat != null)
        {
            string standUpPromptText = currentlyOccupiedSeat.isActivated ? currentlyOccupiedSeat.deactivateText : currentlyOccupiedSeat.activateText;
            if (string.IsNullOrEmpty(standUpPromptText)) standUpPromptText = "Stand Up";

            standUpPrompt.UpdateKeyPromptTexts(standUpPromptText, standUpKey.ToString(), currentlyOccupiedSeat.CanInteract);
        }

        //Update UI prompt when looking at an interactable
        if (lookingAtInteractable != null)
        {
            string interactionPromptText = lookingAtInteractable.isActivated ? lookingAtInteractable.deactivateText : lookingAtInteractable.activateText;
            if (string.IsNullOrEmpty(interactionPromptText)) interactionPromptText = "Interact";

            interactPrompt.UpdateKeyPromptTexts(interactionPromptText, interactionKey.ToString(), lookingAtInteractable.CanInteract);
        }

        if (spaceshipController != null)
        {
            //Show ship control specific UI prompts only when player is in the pilot seat (or when in exterior demo mode)

            if (cameraPrompt != null) cameraPrompt.gameObject.SetActive(spaceshipController.PlayerInPilotSeat && orbitCameraController != null);

            if (shipControlsPrompt != null) shipControlsPrompt.gameObject.SetActive(spaceshipController.PlayerInPilotSeat || ExteriorOnlyDemoMode);


            if (landingGearPrompt != null) landingGearPrompt.SetInteractable(!spaceshipController.IsLandingGearAnimating());
            if (rampPrompt != null) rampPrompt.SetInteractable(spaceshipController.ramp != null && spaceshipController.ramp.CanInteract);
            if (hyperspacePrompt != null) hyperspacePrompt.SetInteractable(!spaceshipController.HyperspaceSequenceInProgress);
        }

        //disable reticle when in orbit camera
        if (reticle != null) reticle.SetActive(cameraMode == CameraModes.Player);


        //Hide UI
        if (Input.GetKeyDown(hideUIKey) && KeyPromptsParent != null)
        {
            ShowUI(!KeyPromptsParent.activeSelf);
        }

        #endregion

#if !UNITY_EDITOR
                if (Input.GetKeyDown(KeyCode.Escape))
                    Application.Quit();
#endif
    }

    private void ShowUI(bool state)
    {
        if (KeyPromptsParent != null) KeyPromptsParent.SetActive(state);
    }

    //switches between the different camera modes (currently first person, and orbit cameras)
    public void SetCameraMode(CameraModes newMode)
    {
        bool camModeChanged = newMode != cameraMode;
        cameraMode = newMode;

        if (firstPersonController != null) firstPersonController.gameObject.SetActive(cameraMode == CameraModes.Player);
        if (orbitCameraController != null) orbitCameraController.gameObject.SetActive(cameraMode != CameraModes.Player);

        //notify the ship controller of the camera switch
        if (camModeChanged && CameraChanged != null) CameraChanged.Invoke(newMode);
    }


    #region Interaction

    private VattalusInteractable CheckIfLookingAtInteractable()
    {
        VattalusInteractable interactableObj = null;

        //Check if the cursor is looking at an interactable object
        RaycastHit hit;
        var cameraCenter = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width / 2f, Screen.height / 2f, Camera.main.nearClipPlane));
        if (Physics.Raycast(cameraCenter, Camera.main.transform.forward, out hit, interactionRange))
        {
            interactableObj = hit.collider.GetComponent<VattalusInteractable>();
        }

        return interactableObj;
    }

    private void InteractWith(VattalusInteractable interactable, bool forced = false)
    {
        if (interactable.CanInteract)
        {
            interactable.Interact(false, forced);
        }
    }

    //This method is called when the player sits down in a seat
    private void SitPlayerDown(VattalusInteractable interactableSeat)
    {
        InteractWith(interactableSeat);
        currentlyOccupiedSeat = interactableSeat;

        //Notify the player controller to sit down
        if (firstPersonController != null) firstPersonController.SitDown(interactableSeat);

        //check if player sat down in the pilot seat
        if (spaceshipController != null && interactableSeat == spaceshipController.pilotSeat)
        {
            spaceshipController.OccupyPilotSeat(true);
        }
    }

    //This method is called when the player stands up from seat
    private void StandPlayerUp()
    {
        // check if player just stood up from the pilot seat, and call the appropriate method
        if (spaceshipController != null && currentlyOccupiedSeat == spaceshipController.pilotSeat)
        {
            spaceshipController.OccupyPilotSeat(false);
        }

        //interact with currently occupied seat to "disable" it
        if (currentlyOccupiedSeat != null && currentlyOccupiedSeat.isActivated) InteractWith(currentlyOccupiedSeat, true);
        currentlyOccupiedSeat = null;

        SetCameraMode(CameraModes.Player);

        //Notify the player controller to stand up
        if (firstPersonController != null) firstPersonController.StandUp();
    }
    #endregion

    IEnumerator FpsCounterCoroutine()
    {
        fpsCounter.text = "FPS: ...";

        yield return new WaitForSeconds(1f);

        int sampleRate = 10;
        int currSample = 0;
        float avgFps = 0f;

        while (Application.isPlaying)
        {
            currSample++;
            avgFps += (1f / Time.unscaledDeltaTime);

            if (currSample >= sampleRate)
            {
                if (fpsCounter != null)
                    fpsCounter.text = "FPS: " + (int)avgFps / sampleRate;

                currSample = 0;
                avgFps = 0f;
            }

            yield return new WaitForSeconds(1f / (float)sampleRate);
        }
    }
}
