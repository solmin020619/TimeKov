using System;
using System.Collections;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Events;


//The script is used to keep track of when it is opened/closed, and also relevant references such as connected rooms. The ShipController script then 
public class VattalusDoorController : MonoBehaviour
{
    private VattalusInteractable interactableComponent;
    private VattalusPlayerDetector playerDetector;

    //Rooms connected by this door
    public VattalusRoomController connectedRoom1;
    public VattalusRoomController connectedRoom2;

    //Door will automatically open/close whenever the player enters/exits the door's bounds collider
    public bool autoOpen = false;
    public bool autoClose = false;

    public float autoOpenDistance = 4f; //distance at which doors open when player approaches. Same distance for closing

    //Callback events for the different stages of door movement
    [CanBeNull]
    [Header("Event Callbacks")]
    public UnityEvent<VattalusDoorController> onDoorOpenStarted;
    public UnityEvent<VattalusDoorController> onDoorOpenEnded;
    public UnityEvent<VattalusDoorController> onDoorCloseStarted;
    public UnityEvent<VattalusDoorController> onDoorCloseEnded;

    //is the door (even partially) open?
    private bool _isOpen = false;

    void Awake()
    {
        //find the InteractableObject component and inject the OnDoorOpen and OnDoorClose methods into the callbacks
        interactableComponent = GetComponentInChildren<VattalusInteractable>();
        if (interactableComponent != null)
        {
            interactableComponent.onAnimationStartEvent.AddListener(OnAnimationStart);
            interactableComponent.onAnimationEndEvent.AddListener(OnAnimationEnd);
        }

        _isOpen = interactableComponent.isActivated;

        //if set to autoOpen, create a collider that detects player approach
        if (autoOpen || autoClose)
        {
            //Find the player detector script among the children
            playerDetector = GetComponentInChildren<VattalusPlayerDetector>();

            //if player detector script is missing, create one
            if (playerDetector == null)
            {
                GameObject boundsColliderGO = new GameObject("BoundsCollider");
                boundsColliderGO.transform.SetParent(gameObject.transform);
                boundsColliderGO.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                boundsColliderGO.layer = 2;

                //add the player detector script
                playerDetector = boundsColliderGO.AddComponent<VattalusPlayerDetector>();

                //create the box collider
                BoxCollider boundsBoxColl = boundsColliderGO.AddComponent<BoxCollider>();
                boundsBoxColl.isTrigger = true;
            }

            if (playerDetector != null)
            {
                //set the distance
                BoxCollider col = playerDetector.GetComponent<BoxCollider>();
                if (col != null)
                {
                    col.center = new Vector3(0f, 1.25f, 0f);
                    col.size = new Vector3(autoOpenDistance, 2.5f, autoOpenDistance);
                }


                //add the callbacks for when the player enters/exits the bounds collider
                playerDetector.OnPlayerEnter.AddListener(AutoOpenDoor);
                playerDetector.OnPlayerExit.AddListener(AutoCloseDoor);
            }
        }
    }

    //methods injected into the VattalusInteractable component animation callbacks
    //basically we want to know whenever a door is being opened/closed or when the open/close is complete
    private void OnAnimationStart()
    {
        if (interactableComponent.isActivated)
        {
            _isOpen = true;
            if (onDoorOpenStarted != null) onDoorOpenStarted.Invoke(this);
        }
        else
        {
            if (onDoorCloseStarted != null) onDoorCloseStarted.Invoke(this);
        }
    }

    private void OnAnimationEnd()
    {
        if (interactableComponent.isActivated)
        {
            if (onDoorOpenEnded != null) onDoorOpenEnded.Invoke(this);
        }
        else
        {
            _isOpen = false;
            if (onDoorCloseEnded != null) onDoorCloseEnded.Invoke(this);
        }
    }

    public bool isDoorOpen()
    {
        return _isOpen;
    }

    private void AutoOpenDoor() { if (autoOpen) OpenDoor(true); }
    public void OpenDoor(bool force = false)
    {
        if (interactableComponent != null && !interactableComponent.isActivated)
        {
            interactableComponent.Interact(false, force);
        }
    }
    public void OpenDoorWithDelay() { OpenDoorWithDelay(2f, false); }
    public void OpenDoorWithDelay(float delay = 1f, bool force = false)
    {
        StartCoroutine(OpenCloseDelay(true, delay, force));
    }

    private void AutoCloseDoor() { if (autoClose) CloseDoor(true); }
    public void CloseDoor(bool force = false)
    {
        if (interactableComponent != null && interactableComponent.isActivated)
        {
            interactableComponent.Interact(false, force);
        }
    }
    public void CloseDoorWithDelay() { CloseDoorWithDelay(2f, false); }
    public void CloseDoorWithDelay(float delay, bool force = false)
    {
        StartCoroutine(OpenCloseDelay(false, delay, force));
    }

    IEnumerator OpenCloseDelay(bool open, float delay, bool force)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delay));

        if (open) OpenDoor(force); else CloseDoor(force);
    }
}
