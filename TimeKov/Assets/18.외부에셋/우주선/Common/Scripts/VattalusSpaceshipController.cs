using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Events;


//This script handles the functions specific to controlling the spaceship (inputs, movement, landing gear animations, etc)
public class VattalusSpaceshipController : MonoBehaviour
{
    [Tooltip("Should the landing gear be deployed at the start of the scene?")]
    public bool DeployLandingGearByDefault = false;
    [Tooltip("Should the ramp be deployed at the start of the scene?")]
    public bool DeployRampByDefault = false;

    //CONTROLS

    //List of input keys
    [Header("Spaceship Controls")]
    public KeyCode accelerateInputKey = KeyCode.LeftShift;
    public KeyCode decelerateInputKey = KeyCode.LeftControl;
    public KeyCode strafeLeftInputKey = KeyCode.A;
    public KeyCode strafeRightInputKey = KeyCode.D;
    public KeyCode moveUpInputKey = KeyCode.Space;
    public KeyCode moveDownInputKey = KeyCode.LeftAlt;
    public KeyCode rollLeftInputKey = KeyCode.Q;
    public KeyCode rollRightInputKey = KeyCode.E;
    public KeyCode pitchUp = KeyCode.S;
    public KeyCode pitchDown = KeyCode.W;
    public KeyCode yawLeft = KeyCode.LeftArrow;
    public KeyCode yawRight = KeyCode.RightArrow;

    [Space]
    public KeyCode landingGearKey = KeyCode.G;
    public KeyCode hologramKey = KeyCode.H;
    public KeyCode rampKey = KeyCode.R;
    public KeyCode hyperspaceKey = KeyCode.J;


    [Header("Movement Variables")]
    public bool enableMovement = false;
    public float maxSpeed = 200f;
    public float maxRotation = 3f;

    public float fwdThrust = 10f;
    public float backThrust = 6f;

    public float verticalThrust = 6f;
    public float lateralThrust = 6f;
    public float rollThrust = 4f;
    public float pitchThrust = 5f;
    public float yawThrust = 5f;

    [Space]

    //player input values
    public float accelerationInput = 0f;
    [HideInInspector] public float strafeInput = 0f;
    [HideInInspector] public float upDownInput = 0f;
    [HideInInspector] public float rollInput = 0f;
    [HideInInspector] public float pitchInput = 0f;
    [HideInInspector] public float yawInput = 0f;

    [Space]

    //important references
    [Header("Important Components (Thruster references populated automatically)")]
    public List<VattalusThrusterController> listOfThrusters = new List<VattalusThrusterController>();
    public List<VattalusInteractable> landingGearList = new List<VattalusInteractable>();
    public VattalusInteractable pilotSeat;
    public VattalusInteractable ramp;
    public VattalusInteractable hologram;
    [HideInInspector]
    private bool _playerInPilotSeat = false;
    public bool PlayerInPilotSeat { get { return _playerInPilotSeat; } }

    public Transform Joystick;
    public Transform ThrottleControl;

    private Vector3 joystickAngleTarget = new Vector3();
    private float throttleAngleTarget = 0f;

    [Header("Sound Effects")]
    public VattalusEngineSoundController IdlingSound;
    public VattalusEngineSoundController ThrustSound;
    public VattalusEngineSoundController RevThrustSound;
    public VattalusEngineSoundController ManeuverSound;
    public VattalusEngineSoundController HyperspaceEngineSound;


    //parameters that control the amount of shaking produced during maneuvering (this will influence camera shake, and other potential behaviours)
    [Header("Shaking")]
    public float accelerationShakeAmplitude = 0.2f;
    public float reverseShakeAmplitude = 0.12f;
    public float maneuveringShakeAmplitude = 0.06f;
    public float hyperspaceSequenceShakeAmplitude = 1f;
    public float duringHyperspaceShakeAmplitude = 0.2f;
    [Space]
    public float accelerationShakeFrequency = 40f;
    public float reversetionShakeFrequency = 60f;
    public float maneuveringShakeFrequency = 80f;
    [Space]
    public float shakeSmoothingSpeed = 2f; //how smoothly should we change the shake parameters
    private Vector2 shakeValues = new Vector2(0f, 1f); //[Amplitude, Frequency] final result of calculating the shake factors

    public VattalusFakeSkyboxMovement fakeSkyboxController;

    [Header("Hyperspace Effects")]
    public bool inHyperspaceByDefault = false;
    private bool inHyperspace = false;
    public bool InHyperspace { get { return inHyperspace; } }

    public float hyperspaceSequenceDuration = 5f;
    public AnimationCurve hyperspaceScreenShakeCurve = new AnimationCurve();
    public HyperspaceEffectController hyperspaceEffectController;
    public AudioSource hyperspaceEnterSound;
    public AudioSource hyperspaceExitSound;

    private float _sequenceStartTime = -9999f;
    public float HyperspaceSequenceProgress { get { return Mathf.Clamp01((Time.time - _sequenceStartTime) / (inHyperspace ? hyperspaceSequenceDuration : hyperspaceSequenceDuration * 0.66f)); } } //sequence is 33% faster when exiting hyperspace
    private bool _hyperspaceSequenceInProgress = false;
    public bool HyperspaceSequenceInProgress { get { return _hyperspaceSequenceInProgress; } }

    [Space]
    //EVENTS
    public UnityEvent OnHyperspaceInStart;
    public UnityEvent OnHyperspaceInEnd;
    public UnityEvent OnHyperspaceOutStart;
    public UnityEvent OnHyperspaceOutEnd;

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (DeployLandingGearByDefault) DeployLandingGear(true);

        if (DeployRampByDefault && ramp != null) ramp.InteractForceToState(true, true);
        shakeValues = new Vector2(0f, 1f);

        //Initialize References/Variables
        listOfThrusters = new List<VattalusThrusterController>();
        foreach (var ThrusterController in GetComponentsInChildren<VattalusThrusterController>())
        {
            listOfThrusters.Add(ThrusterController);
        }

        //reference checks
        if (listOfThrusters == null || listOfThrusters.Count == 0) Debug.Log("<color=#FF0000>VattalusAssets: [ShipController] List of thrusters null or empty.</color>");
        if (landingGearList == null || landingGearList.Count == 0) Debug.Log("<color=#FF0000>VattalusAssets: [ShipController] Landing gear references null or empty.</color>");
        if (pilotSeat == null) Debug.Log("<color=#FF0000>VattalusAssets: [ShipController] Missing pilot seat reference.</color>");

        //initialize the hyperspace effect
        if (hyperspaceSequenceDuration <= 0f) hyperspaceSequenceDuration = 1f; //make sure hyperspace sequence duration is at least 1 second
        _hyperspaceSequenceInProgress = false;
        _sequenceStartTime = -9999f;

        //make sure the hyperspace effect GO is enabled, and set initial state
        if (hyperspaceEffectController != null) hyperspaceEffectController.gameObject.SetActive(true);
        if (inHyperspaceByDefault)
        {
            inHyperspace = true;
            if (hyperspaceEffectController != null) hyperspaceEffectController.StartTransitionIn(true);
        }
        else
        {
            inHyperspace = false;
            if (hyperspaceEffectController != null) hyperspaceEffectController.StartTransitionOut(true);
        }
    }

    void Update()
    {
        //only enable spaceship controls when sitting in the pilot seat (or when in exterior demo mode) AND ship is not in hyperspace
        enableMovement = (PlayerInPilotSeat || VattalusSceneController.Instance.ExteriorOnlyDemoMode) && !inHyperspace && !HyperspaceSequenceInProgress;

        //handle interaction inputs
        if (Input.GetKeyDown(landingGearKey)) DeployLandingGear();
        if (Input.GetKeyDown(rampKey) && ramp != null) ramp.Interact();
        if (Input.GetKeyDown(hologramKey) && hologram != null) hologram.Interact();
        if (Input.GetKeyDown(hyperspaceKey) && hyperspaceEffectController != null && !HyperspaceSequenceInProgress)
        {
            if (inHyperspace)
            {
                StartHyperspaceSequence(false);
            }
            else
            {
                StartHyperspaceSequence(true);
            }
        }
    }

    private void FixedUpdate()
    {
        //We set the command inputs based on keypresses (or other input methods). They will be processed later
        accelerationInput = 0f;
        strafeInput = 0f;
        upDownInput = 0f;
        rollInput = 0f;
        pitchInput = 0f;
        yawInput = 0f;

        //Get inputs
        if (enableMovement)
        {
            if (Input.GetKey(accelerateInputKey)) accelerationInput = 1f; if (Input.GetKey(decelerateInputKey)) accelerationInput -= 1f;
            if (Input.GetKey(strafeRightInputKey)) strafeInput = 1f; if (Input.GetKey(strafeLeftInputKey)) strafeInput -= 1f;
            if (Input.GetKey(moveUpInputKey)) upDownInput = 1f; if (Input.GetKey(moveDownInputKey)) upDownInput -= 1f;
            if (Input.GetKey(rollRightInputKey)) rollInput = 1f; if (Input.GetKey(rollLeftInputKey)) rollInput -= 1f;
            if (Input.GetKey(pitchUp)) pitchInput = 1f; if (Input.GetKey(pitchDown)) pitchInput -= 1f;
            if (Input.GetKey(yawRight)) yawInput = 1f; if (Input.GetKey(yawLeft)) yawInput -= 1f;


            //Add movement force
            if (rb != null)
            {
                ////MOVEMENT   (Actual ship movement is not implemented, as it would be outside the scope of this demo, in order to achieve the desired effect, I instead chose to rotate the whole environment around the ship to fake movement)
                //              the code below is a very basic implementation of physics movement using forces applied to the rigidbody
                if (enableMovement)
                {
                    //Apply move speed
                    //if (accelerationInput > 0f) rb.AddForce(transform.forward * accelerationInput * fwdThrust * Time.deltaTime);
                    //if (accelerationInput < 0f) rb.AddForce(transform.forward * accelerationInput * backThrust * Time.deltaTime);
                    //if (upDownInput != 0f) rb.AddForce(transform.up * upDownInput * verticalThrust * Time.deltaTime);
                    //if (strafeInput != 0f) rb.AddForce(transform.right * strafeInput * lateralThrust * Time.deltaTime);

                    ////ROTATION
                    //rb.AddRelativeTorque(
                    //    pitchInput * -pitchThrust * Time.deltaTime,
                    //    yawInput * yawThrust * Time.deltaTime,
                    //    rollInput * -rollThrust * Time.deltaTime
                    //);
                }

            }
        }

        //Apply thruster effects
        if (listOfThrusters != null)
        {
            foreach (var thruster in listOfThrusters)
            {
                if (thruster == null) continue;

                //by default, all thrusters will decay
                thruster.SetThrust(0f);

                //Depending on which input is given to the spaceship, enable thrust effects on thrusters whose directions correspond with given input
                if (accelerationInput > 0 && thruster.usedForAcceleration) thruster.SetThrust(accelerationInput);
                if (accelerationInput < 0 && thruster.usedForDeceleration) thruster.SetThrust(-accelerationInput);

                if (strafeInput > 0 && thruster.usedForStrafeRight) thruster.SetThrust(strafeInput);
                if (strafeInput < 0 && thruster.usedForStrafeLeft) thruster.SetThrust(-strafeInput);

                if (upDownInput > 0 && thruster.usedForMoveUp) thruster.SetThrust(upDownInput);
                if (upDownInput < 0 && thruster.usedForMoveDown) thruster.SetThrust(-upDownInput);

                if (rollInput > 0 && thruster.usedForRollRight) thruster.SetThrust(rollInput);
                if (rollInput < 0 && thruster.usedForRollLeft) thruster.SetThrust(-rollInput);

                if (pitchInput > 0 && thruster.usedForPitchUp) thruster.SetThrust(pitchInput);
                if (pitchInput < 0 && thruster.usedForPitchDown) thruster.SetThrust(-pitchInput);

                if (yawInput > 0 && thruster.usedForYawRight) thruster.SetThrust(yawInput);
                if (yawInput < 0 && thruster.usedForYawLeft) thruster.SetThrust(-yawInput);
            }
        }

        //when in hyperspace override acceleration thruster values
        if (inHyperspace)
        {
            foreach (var thruster in listOfThrusters)
            {
                if (thruster == null) continue;
                if (thruster.usedForAcceleration) thruster.SetThrust(HyperspaceSequenceInProgress ? HyperspaceSequenceProgress * 3f : 1f);
            }
        }


        //Move Joystick / Throttle control
        if (Joystick != null)
        {
            joystickAngleTarget = new Vector3(
                Mathf.Lerp(25f, -25f, (pitchInput + 1f) / 2f),
                Mathf.Lerp(-20f, 20f, (rollInput + 1f) / 2f),
                Mathf.Lerp(-19f, 19f, (yawInput + 1f) / 2f));

            Joystick.localRotation = Quaternion.Lerp(Joystick.transform.localRotation, Quaternion.Euler(joystickAngleTarget), 5f * Time.deltaTime);
        }

        if (ThrottleControl != null)
        {
            throttleAngleTarget = Mathf.Lerp(throttleAngleTarget, Mathf.Lerp(-33f, 33f, (accelerationInput + 1f) / 2f), 5f * Time.deltaTime);
            ThrottleControl.localRotation = Quaternion.Euler(throttleAngleTarget, 0f, 0f);
        }

        //Handle sound effects
        if (IdlingSound != null) IdlingSound.SetInput(enableMovement ? 1f - Mathf.Abs(accelerationInput) : 0f); //idling sound will play when player in pilot seat but not accelerating
        if (ThrustSound != null) ThrustSound.SetInput(Mathf.Clamp01(Mathf.Max(accelerationInput)));
        if (RevThrustSound != null) RevThrustSound.SetInput(Mathf.Clamp01(-accelerationInput));
        float maneuverintensity = Mathf.Clamp01(Mathf.Abs(strafeInput) + Mathf.Abs(upDownInput) + Mathf.Abs(rollInput) + Mathf.Abs(pitchInput) + Mathf.Abs(yawInput));
        if (ManeuverSound != null) ManeuverSound.SetInput(maneuverintensity);
        if (HyperspaceEngineSound != null) HyperspaceEngineSound.SetInput(inHyperspace ? 1f : 0f);

        //Handle shaking
        float shakeAmplitude = 0f;
        shakeAmplitude += Mathf.Lerp(0f, accelerationShakeAmplitude, Mathf.Clamp01(accelerationInput));
        shakeAmplitude += Mathf.Lerp(0f, reverseShakeAmplitude, Mathf.Clamp01(-accelerationInput));
        shakeAmplitude += Mathf.Lerp(0f, maneuveringShakeAmplitude, Mathf.Clamp01(maneuverintensity));
        //hyperpsace effect shaking
        if (HyperspaceSequenceInProgress) { shakeAmplitude += hyperspaceScreenShakeCurve.Evaluate(inHyperspace ? HyperspaceSequenceProgress : 1f - HyperspaceSequenceProgress) * hyperspaceSequenceShakeAmplitude; }
        else { if (inHyperspace) shakeAmplitude += duringHyperspaceShakeAmplitude; }


        float shakeFreq = 1f;
        shakeFreq += Mathf.Lerp(0f, accelerationShakeFrequency, Mathf.Clamp01(accelerationInput));
        shakeFreq += Mathf.Lerp(0f, reversetionShakeFrequency, Mathf.Clamp01(-accelerationInput));
        shakeFreq += Mathf.Lerp(0f, maneuveringShakeFrequency, Mathf.Clamp01(maneuverintensity - Mathf.Abs(accelerationInput) * 0.65f));
        //hyperpsace effect shaking
        if (HyperspaceSequenceInProgress) { shakeFreq += hyperspaceScreenShakeCurve.Evaluate(inHyperspace ? HyperspaceSequenceProgress : 1f - HyperspaceSequenceProgress) * 50f; }
        else { if (inHyperspace) shakeFreq += 10f; }

        shakeValues = Vector2.Lerp(shakeValues, new Vector2(shakeAmplitude, shakeFreq), shakeSmoothingSpeed * Time.deltaTime);

        VattalusCameraShake.Shake(shakeValues.x, shakeValues.y);

        //update the fake skybox
        if (fakeSkyboxController != null) fakeSkyboxController.MoveFakeSkybox(pitchInput, pitchThrust, yawInput, yawThrust, rollInput, rollThrust);

        //update hyperspace sequence
        if (HyperspaceSequenceInProgress) UpdateHyperspaceSequence();
    }

    //called from outside to let this script know when the player sits down and stands up from the pilot seat
    public void OccupyPilotSeat(bool isOccupied)
    {
        _playerInPilotSeat = isOccupied;

        if (isOccupied)
        {

        }
        else
        {

        }
    }

    private void DeployLandingGear(bool instant = false)
    {
        if (landingGearList != null)
        {
            foreach (var landingGear in landingGearList)
            {
                landingGear.Interact(instant);
            }
        }
    }

    public bool IsLandingGearDeployed()
    {
        if (landingGearList != null && landingGearList.Count > 0)
        {
            return landingGearList[0].isActivated;
        }

        return false;
    }

    public bool IsLandingGearAnimating()
    {
        if (landingGearList != null && landingGearList.Count > 0)
        {
            return landingGearList[0].IsAnimating;
        }

        return false;
    }

    private void StartHyperspaceSequence(bool transitionDirection)
    {
        inHyperspace = transitionDirection;
        _sequenceStartTime = Time.time;
        _hyperspaceSequenceInProgress = true;

        if (inHyperspace)
        {
            HyperspaceInStart();
        }
        else
        {
            HyperspaceOutStart();
        }
    }

    private void UpdateHyperspaceSequence()
    {
        if (HyperspaceSequenceProgress >= 1f)
        {
            // sequence finished
            if (inHyperspace)
            {
                HyperspaceInEnd();
            }
            else
            {
                HyperspaceOutEnd();
            }

            _hyperspaceSequenceInProgress = false;
        }
    }

    //called when sequence STARTS INTO hyperspace
    private void HyperspaceInStart()
    {
        if (hyperspaceEffectController != null)
        {
            hyperspaceEffectController.StartTransitionIn();
        }

        if (hyperspaceEnterSound != null)
        {
            hyperspaceEnterSound.Play();
        }

        if (OnHyperspaceInStart != null) OnHyperspaceInStart.Invoke();
    }

    //called when sequence ENDS INTO hyperspace
    private void HyperspaceInEnd()
    {
        if (OnHyperspaceInEnd != null) OnHyperspaceInEnd.Invoke();
    }

    //called when sequence STARTS OUT OF hyperspace
    private void HyperspaceOutStart()
    {
        if (hyperspaceEffectController != null) hyperspaceEffectController.StartTransitionOut();

        if (hyperspaceExitSound != null)
        {
            hyperspaceExitSound.Play();
        }

        if (OnHyperspaceOutStart != null) OnHyperspaceOutStart.Invoke();
    }

    //called when sequence ENDS OUT OF hyperspace
    private void HyperspaceOutEnd()
    {
        if (OnHyperspaceOutEnd != null) OnHyperspaceOutEnd.Invoke();
    }
}
