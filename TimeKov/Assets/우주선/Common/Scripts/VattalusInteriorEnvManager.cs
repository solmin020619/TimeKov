using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


//This script mainly manages interior environments, enabling/disabling renderes, lights, and audio sources based on player location
public class VattalusInteriorEnvManager : MonoBehaviour
{
    //a flag that reflects whether the player controller is located phyisically inside this interior environment
    private bool _isPlayerInside = false;
    public bool IsPlayerInside { get { return _isPlayerInside; } }

    //event for when the player enters/leaves the environment
    public UnityEvent OnPlayerEntersEnvironment;
    public UnityEvent OnPlayerExitsEnvironment;

    //list of all rooms & doors
    private List<VattalusRoomController> _roomsList = new List<VattalusRoomController>();
    private List<VattalusDoorController> _doorsList = new List<VattalusDoorController>();
    private List<VattalusRoomController> _roomsToLeaveVisible = new List<VattalusRoomController>();

    void Start()
    {
        //find all rooms and doors
        _roomsList = new List<VattalusRoomController>();
        foreach (var room in GetComponentsInChildren<VattalusRoomController>())
        {
            _roomsList.Add(room);

            //whenever the room detects the player enters/leaves we update the _isPlayerInside flag
            room.OnPlayerEnteredRoom.AddListener(PlayerInsideCheck);
            room.OnPlayerLeftRoom.AddListener(PlayerInsideCheck);
        }

        _doorsList = new List<VattalusDoorController>();
        foreach (var door in GetComponentsInChildren<VattalusDoorController>())
        {
            _doorsList.Add(door);
            //and also subscribe to its open/close events
            door.onDoorOpenStarted.AddListener(HideUnhideRooms);
            door.onDoorCloseEnded.AddListener(HideUnhideRooms);
        }

        //call the hide & unhide method to start off with an optimized scene
        StartCoroutine(DelayCoroutine(0.2f, delegate
        {
            PlayerInsideCheck();
            HideUnhideRooms();
        }));


        if (VattalusSceneController.Instance != null)
            VattalusSceneController.Instance.CameraChanged.AddListener(SceneCameraChanged);
    }

    //this method checks whether the player is inside the environment by checking each room individually
    private void PlayerInsideCheck()
    {
        bool playerInsideCheck = false;
        if (_roomsList != null)
        {
            foreach (var room in _roomsList)
            {
                if (room.IsPlayerInRoom) playerInsideCheck = true;
            }
        }

        //we compare the new player presence state to the previous one, if it changed we trigger some events
        if (playerInsideCheck != _isPlayerInside)
        {
            if (playerInsideCheck == true && OnPlayerEntersEnvironment != null) OnPlayerEntersEnvironment.Invoke();
            if (playerInsideCheck == false && OnPlayerExitsEnvironment != null) OnPlayerExitsEnvironment.Invoke();

            //when the player either enters or leaves the environment we should re-check which rooms need to be hidden
            HideUnhideRooms(playerInsideCheck);
        }

        _isPlayerInside = playerInsideCheck;
    }

    //this method figures out which rooms can be hidden in order to optimize performance. Certain audio sources in the hidden rooms are also muted to simulate sound insulation
    public void HideUnhideRooms(VattalusDoorController triggeredByDoor) { HideUnhideRooms(); }

    public void HideUnhideRooms() //when the method is called without any parameters, rooms will be hidden/shown based on player's physical location (_isPlayerInside)
    {
        HideUnhideRooms(_isPlayerInside);
    }

    public void HideUnhideRooms(bool playerInside = true) //when the method is called with the bool parameters given, rooms will be hidden/shown based on this parameter, regardless of actual player physical location (for example when rendering the env/ship from the outside even though the player didn't leave the environment)
    {
        //to remove some edge cases, we will still consider the player to be "outside" if any of the doors happen to close while the player is in orbit camera mode
        if (VattalusSceneController.Instance != null && VattalusSceneController.Instance.GetCamMode == VattalusSceneController.CameraModes.ShipOrbit) playerInside = false;

        //we will build a list of rooms to leave visible. All other will be hidden
        _roomsToLeaveVisible = new List<VattalusRoomController>();


        //we start by adding all rooms that intersect with player (could be multiple) OR are seen from the ouside, depending on situation
        foreach (var room in _roomsList)
        {
            if (playerInside)
            {
                if (room.IsPlayerInRoom)
                {
                    _roomsToLeaveVisible.Add(room);
                    AlsoAddPermanentlyVisibleRooms(room);
                }
            }
            else
            {
                if (room.visibleFromExterior)
                {
                    _roomsToLeaveVisible.Add(room);
                    AlsoAddPermanentlyVisibleRooms(room);
                }
            }
        }

        //check for some edge-cases...
        if (_roomsToLeaveVisible.Count == 0)
        {
            if (playerInside == false)
            {
                //in case the player is outside the ship, then we run the usual optimization when viewing the ship from outside
                HideUnhideRooms(false);
            }
            //in case player is somewhere inside the ship, but not found in any room, then we do nothing (to avoid hiding all the rooms and stranding the player)
            return;
        }

        //recursively check adiacent rooms until no new room is added
        CheckConnectedDoors();


        //At this point, we have determined which rooms should be hidden, and which should be shown. We just have to let each room know what to do.
        foreach (var room in _roomsList)
        {
            if (_roomsToLeaveVisible.Contains(room))
            {
                room.UnhideRoom();
            }
            else
            {
                room.HideRoom();
            }
        }

        //Finally, if the player is outside we should mute all room audio sources
        if (playerInside == false) MuteUnmuteRooms(true);
    }

    //called when player or camera is outside the ship in order to fade out all room-specific audio sources like beeps and vents
    public void MuteUnmuteRooms(bool mute)
    {
        foreach (var room in _roomsList)
        {
            room.MuteUnmuteAudioSources(mute);
        }
    }

    //add all other rooms that are visually connected to the given room
    private void AlsoAddPermanentlyVisibleRooms(VattalusRoomController room)
    {
        foreach (var connectedRoom in room.otherRoomsPermanentlyVisible)
        {
            if (!_roomsToLeaveVisible.Contains(connectedRoom)) _roomsToLeaveVisible.Add(connectedRoom);
        }
    }

    //recursive function to check all rooms, through all the door paths, until no new room is added to the "roomsToLeaveVisible" list
    private void CheckConnectedDoors()
    {
        bool newRoomAdded = false;

        //we check find all the doors that are connected to our list of rooms
        foreach (var door in _doorsList)
        {
            if (_roomsToLeaveVisible.Contains(door.connectedRoom1) || _roomsToLeaveVisible.Contains(door.connectedRoom2))
            {
                //found a door connected to our room
                if (door.isDoorOpen())
                {
                    if (!_roomsToLeaveVisible.Contains(door.connectedRoom1))
                    {
                        _roomsToLeaveVisible.Add(door.connectedRoom1);
                        AlsoAddPermanentlyVisibleRooms(door.connectedRoom1);
                        newRoomAdded = true;
                    }

                    if (!_roomsToLeaveVisible.Contains(door.connectedRoom2))
                    {
                        _roomsToLeaveVisible.Add(door.connectedRoom2);
                        AlsoAddPermanentlyVisibleRooms(door.connectedRoom2);
                        newRoomAdded = true;
                    }
                }
            }
        }

        if (newRoomAdded) CheckConnectedDoors();
    }

    //called whenever the camera perspective changes. This will for the environment to re-evaluate which rooms need to be shown/hidden
    private void SceneCameraChanged(VattalusSceneController.CameraModes camMode)
    {
        switch (camMode)
        {
            case VattalusSceneController.CameraModes.Player:
                {
                    HideUnhideRooms(true);
                    break;
                }

            case VattalusSceneController.CameraModes.ShipOrbit:
                {
                    HideUnhideRooms(false);
                    break;
                }
        }
    }

    IEnumerator DelayCoroutine(float delay, Action callback)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delay));

        if (callback != null) callback.Invoke();
    }
}
