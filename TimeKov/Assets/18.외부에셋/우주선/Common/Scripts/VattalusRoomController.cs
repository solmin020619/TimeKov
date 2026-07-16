using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


//This script keeps track of the different components that make up the room (mesh, lights, audio sources etc)
public class VattalusRoomController : MonoBehaviour
{
    [Header("Parameters")]
    private bool _isHidden = false; //if this room is hidden or not (hidden means meshes and lights are disabled, and audio sources are muted)
    private bool _isPlayerInRoom = false;
    public bool IsPlayerInRoom { get { PlayerPresenceCheck(); return _isPlayerInRoom; } }
    public bool visibleFromExterior = false; //useful when hiding non-visible room while viewing ship from outside
    public List<VattalusRoomController> otherRoomsPermanentlyVisible = new List<VattalusRoomController>(); //keeps track which rooms are visually connected to this one (to avoid hiding rooms that can be seen though a window for example)

    //reference to the player detector script and bounds collider
    public List<VattalusPlayerDetector> playerDetectorBounds = new List<VattalusPlayerDetector>(); //useful when determining whether player is inside the room

    //we de-activate these when hiding a room
    public GameObject meshParent;
    public GameObject lightsParent;

    //audio source effects/controls
    private List<VattalusAudioController> audioControllers = new List<VattalusAudioController>();
    private float audioLerpDuration = 1f;
    private float audioLerpStartTime = -1000f;
    private Vector2 audioLerpInterval = new Vector2(1f, 1f); //we will lerp the audio levels from .x to .y
    private float currentAudioFactor = 1f;

    //Events
    public UnityEvent OnPlayerEnteredRoom;
    public UnityEvent OnPlayerLeftRoom;

    void Start()
    {
        //Initialize variables
        audioControllers = new List<VattalusAudioController>();
        foreach (VattalusAudioController newAudioController in GetComponentsInChildren<VattalusAudioController>(true))
        {
            audioControllers.Add(newAudioController);
        }

        if (playerDetectorBounds != null)
        {
            foreach (VattalusPlayerDetector detector in playerDetectorBounds)
            {
                detector.OnPlayerEnter.AddListener(PlayerPresenceCheck);
                detector.OnPlayerExit.AddListener(PlayerPresenceCheck);
            }
        }

        //trigger this manually as an initialization
        PlayerPresenceCheck();
    }

    void FixedUpdate()
    {
        if (audioControllers != null && audioControllers.Count > 0 && Time.time <= audioLerpStartTime + audioLerpDuration)
            LerpAudioVolumes();
    }


    //we check each detector individually to check if player is within any of their bounds
    //if the player presence changed since the last check we also trigger some methods/events
    private void PlayerPresenceCheck()
    {
        bool newInsideCheck = false;
        if (playerDetectorBounds != null)
        {
            foreach (VattalusPlayerDetector detector in playerDetectorBounds)
            {
                if (detector.PlayerInsideCollider) newInsideCheck = true;
            }
        }

        bool playerPresenceChanged = _isPlayerInRoom != newInsideCheck;
        _isPlayerInRoom = newInsideCheck;

        if (playerPresenceChanged)
        {
            //new player presence changed, trigger PlayerEntersRoom() or PlayerExitsRoom() based on new values
            if (_isPlayerInRoom) PlayerEntersRoom(); else PlayerExitsRoom();
        }
    }

    //actions we want to do when the player enters the room
    private void PlayerEntersRoom()
    {
        if (!_isHidden) StartAudioVolumeLerp(1f, 1f);

        if (OnPlayerEnteredRoom != null) OnPlayerEnteredRoom.Invoke();
    }

    //actions we want to do when the player exits the room
    private void PlayerExitsRoom()
    {
        if (!_isHidden) StartAudioVolumeLerp(0.5f, 1.5f);

        if (OnPlayerLeftRoom != null) OnPlayerLeftRoom.Invoke();
    }

    public void HideRoom()
    {
        if (meshParent != null) meshParent.SetActive(false);
        if (lightsParent != null) lightsParent.SetActive(false);

        //mute audio sources
        MuteUnmuteAudioSources(true);

        _isHidden = true;
    }

    public void UnhideRoom()
    {
        if (meshParent != null) meshParent.SetActive(true);
        if (lightsParent != null) lightsParent.SetActive(true);

        //set audio sources volume based on player presence
        MuteUnmuteAudioSources(false);

        _isHidden = false;
    }


    #region AUDIO SOURCE VOLUME LERPING--------- 
    public void MuteUnmuteAudioSources(bool mute)
    {
        if (mute)
        {
            StartAudioVolumeLerp(0f, 1f);
        }
        else
        {
            //set audio sources volume based on player presence
            StartAudioVolumeLerp(_isPlayerInRoom ? 1f : 0.5f, 1f);
        }

    }

    private void StartAudioVolumeLerp(float toValue, float duration)
    {
        audioLerpDuration = duration;
        audioLerpStartTime = Time.time;
        audioLerpInterval = new Vector2(currentAudioFactor, toValue);
    }

    private void LerpAudioVolumes()
    {
        if (Time.time < audioLerpStartTime + audioLerpDuration)
        {
            float _lerpPos = 1f - (audioLerpStartTime + audioLerpDuration - Time.time) / audioLerpDuration;
            currentAudioFactor = Mathf.Lerp(audioLerpInterval.x, audioLerpInterval.y, _lerpPos);

            SetAudioVolumes(currentAudioFactor);
        }
    }

    private void SetAudioVolumes(float volumeFactor)
    {
        foreach (var audioController in audioControllers)
        {
            audioController.SetVolumeFactor(volumeFactor);
        }
    }
    #endregion
}
