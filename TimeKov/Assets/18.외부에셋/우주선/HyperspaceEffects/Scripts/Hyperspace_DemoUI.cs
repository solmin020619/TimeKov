using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Hyperspace_DemoUI : MonoBehaviour
{

    public new Camera camera;
    // horizontal and vertical rotation speeds
    public float cameraSensitivity = 90;
    public Vector2 camFovRange = new Vector2(50f, 100f);
    private float fovTarget = 60f;
    private float rotationX = 0f;
    private float rotationY = 0f;

    private bool clickStartedOverUI = true;


    private HyperspaceEffectController currentPrefab;
    private int currentPrefabIndex;
    private Color currentPrefabInitialLight1Color = Color.black;
    private Color currentPrefabInitialLight2Color = Color.black;
    private Vector2 currentPrefabInitialFlashingPeriods = Vector2.one;

    [Header("Asset References")]
    public List<GameObject> hyperspaceEffectPrefabs;
    public List<ShellMeshClass> hyperspaceShellMeshes = new List<ShellMeshClass>();

    [System.Serializable]
    public class ShellMeshClass
    {
        public string name;
        public Mesh meshReference;
    }


    [Header("UI References")]
    public GameObject ControlsParent;
    public Button HideShowButton;
    public Text HideShowButtonText;
    [Space]

    public Button buttonPreviousPrefab;
    public Button buttonNextPrefab;

    public Button buttonTransitionIn;
    public Button buttonTransitionOut;

    public Dropdown dropdownMeshes;
    public Button buttonMeshesPrev;
    public Button buttonMeshesNext;
    private int currentMeshIndex;

    public Slider sliderTransition;
    public Slider sliderShellOpacity;
    public Slider sliderLightIntensity;
    public Slider sliderFlashingIntensity;
    public Slider sliderFlashingFrequency;
    public Slider sliderDisplacementIntensity;

    void Start()
    {
        fovTarget = camera.fieldOfView;

        //Initialize the UI by setting their values and interaction callbacks
        HideShowButton.onClick.AddListener(ToggleControlsUI);

        buttonPreviousPrefab.onClick.AddListener(ChangeToPreviousPrefab);
        buttonNextPrefab.onClick.AddListener(ChangeToNextPrefab);

        List<Dropdown.OptionData> optionsList = new List<Dropdown.OptionData>();

        foreach (var shell in hyperspaceShellMeshes)
        {
            Dropdown.OptionData newOption = new Dropdown.OptionData();
            newOption.text = shell.name;
            optionsList.Add(newOption);
        }

        dropdownMeshes.AddOptions(optionsList);
        dropdownMeshes.onValueChanged.AddListener(ChangeShellMesh);
        buttonMeshesPrev.onClick.AddListener(ChangeShellMeshPrev);
        buttonMeshesNext.onClick.AddListener(ChangeShellMeshNext);

        buttonTransitionIn.onClick.AddListener(PlayTransitionIn);
        buttonTransitionOut.onClick.AddListener(PlayTransitionOut);

        sliderTransition.onValueChanged.AddListener(ChangeTransitionEffect);
        sliderShellOpacity.onValueChanged.AddListener(ChangeShellOpacity);
        sliderLightIntensity.onValueChanged.AddListener(ChangeLightIntensity);
        sliderFlashingIntensity.onValueChanged.AddListener(ChangeFlashingIntensity);
        sliderFlashingFrequency.onValueChanged.AddListener(ChangeFlasingFrequency);
        sliderDisplacementIntensity.onValueChanged.AddListener(ChangeDisplacementIntensity);


        //Start by instantiating first prefab
        InstantiatePrefab(0);
        PlayTransitionIn();
    }


    void Update()
    {
        //Keyboard controls
        if (Input.GetKeyDown(KeyCode.Tab)) ToggleControlsUI();
        if (Input.GetKeyDown(KeyCode.LeftArrow)) ChangeToPreviousPrefab();
        if (Input.GetKeyDown(KeyCode.RightArrow)) ChangeToNextPrefab();
        if (Input.GetKeyDown(KeyCode.Space)) PlayTransitionIn();
        if (Input.GetKeyDown(KeyCode.Backspace)) PlayTransitionOut();
#if !UNITY_EDITOR
                if (Input.GetKeyDown(KeyCode.Escape))
                    Application.Quit();
#endif

        //CAMERA FOV
        if (EventSystem.current.IsPointerOverGameObject() == false)
        {
            fovTarget += -Input.mouseScrollDelta.y * 600f * Time.deltaTime;
            fovTarget = Mathf.Clamp(fovTarget, camFovRange.x, camFovRange.y);
            camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, fovTarget, 10f * Time.deltaTime);
        }


        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) clickStartedOverUI = EventSystem.current.IsPointerOverGameObject();

        if (clickStartedOverUI == false && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
            CameraMovement();


        if (currentPrefab.TransitionInProgress)
        {
            //when hyperspace transition animation is in progress we need to disable the UI sliders and also update them from the prefab each frame to reflect the animation
            UpdateUIFromPrefab();
            SetUIInteractibility(false);
        }
        else
        {
            SetUIInteractibility(true);
        }
    }

    private void CameraMovement()
    {
        //CAMERA ROTATION
        rotationX += Input.GetAxis("Mouse X") * cameraSensitivity * Time.deltaTime;
        rotationY += Input.GetAxis("Mouse Y") * cameraSensitivity * Time.deltaTime;
        rotationY = Mathf.Clamp(rotationY, -90, 90);

        camera.transform.localRotation = Quaternion.AngleAxis(rotationX, Vector3.up);
        camera.transform.localRotation *= Quaternion.AngleAxis(rotationY, Vector3.left);
    }

    private void ToggleControlsUI()
    {
        if (ControlsParent.activeSelf)
        {
            //disable controls UI
            ControlsParent.SetActive(false);
            HideShowButtonText.text = "▼ Show (TAB) ▼";
        }
        else
        {
            //enable controls UI
            ControlsParent.SetActive(true);
            HideShowButtonText.text = "▲ Hide (TAB) ▲";
        }
    }

    private void ChangeToPreviousPrefab() { ChangePrefab(false); }
    private void ChangeToNextPrefab() { ChangePrefab(true); }

    private void ChangePrefab(bool changeDir)
    {
        currentPrefabIndex += changeDir ? 1 : -1;
        if (currentPrefabIndex < 0) currentPrefabIndex = hyperspaceEffectPrefabs.Count - 1;
        if (currentPrefabIndex >= hyperspaceEffectPrefabs.Count) currentPrefabIndex = 0;

        InstantiatePrefab(currentPrefabIndex);
    }

    private void InstantiatePrefab(int prefabIndex)
    {
        currentPrefabIndex = prefabIndex;
        if (currentPrefab != null) Destroy(currentPrefab.gameObject);
        currentPrefab = Instantiate(hyperspaceEffectPrefabs[currentPrefabIndex], Vector3.zero, Quaternion.identity).GetComponent<HyperspaceEffectController>();

        //get default initial values
        currentPrefabInitialLight1Color = currentPrefab.hyperspaceShellRenderer.material.GetColor("_Light1_Color");
        currentPrefabInitialLight2Color = currentPrefab.hyperspaceShellRenderer.material.GetColor("_Light2_Color");
        currentPrefabInitialFlashingPeriods = currentPrefab.flashingPeriodRange;

        //update the UI to reflect new prefab parameters
        UpdateUIFromPrefab(true);

        //set the mesh dropdown value to the mesh from the prefab
        string prefabMeshName = currentPrefab.hyperspaceShellRenderer.GetComponent<MeshFilter>().sharedMesh.name;
        for (int i = 0; i < hyperspaceShellMeshes.Count; i++)
        {
            if (hyperspaceShellMeshes[i].meshReference.name == prefabMeshName)
            {
                dropdownMeshes.SetValueWithoutNotify(i);
            }
        }
    }

    private void UpdateUIFromPrefab(bool newPrefab = false)
    {
        sliderTransition.SetValueWithoutNotify(currentPrefab.hyperspaceShellRenderer.material.GetFloat("_Transition"));

        sliderShellOpacity.SetValueWithoutNotify(currentPrefab.hyperspaceShellRenderer.material.GetFloat("_ShellOpacity_Value"));

        if (newPrefab)
        {
            sliderLightIntensity.SetValueWithoutNotify(1f);

            sliderFlashingIntensity.maxValue = Mathf.Max(1f, currentPrefab.flashingIntensity * 2f);
            sliderFlashingIntensity.SetValueWithoutNotify(currentPrefab.flashingIntensity);
            sliderFlashingFrequency.SetValueWithoutNotify(1f);

        }
        sliderDisplacementIntensity.SetValueWithoutNotify(currentPrefab.hyperspaceShellRenderer.material.GetFloat("_Displacement_Intensity"));
    }

    private void SetUIInteractibility(bool interactable)
    {
        sliderTransition.interactable = interactable;
        sliderShellOpacity.interactable = interactable;
        sliderLightIntensity.interactable = interactable;
        sliderFlashingIntensity.interactable = interactable;
        sliderFlashingFrequency.interactable = interactable;
        sliderDisplacementIntensity.interactable = interactable;
    }

    private void ChangeShellMeshNext()
    {
        currentMeshIndex += 1;
        if (currentMeshIndex >= hyperspaceShellMeshes.Count) currentMeshIndex = 0;

        ChangeShellMesh(currentMeshIndex);
        dropdownMeshes.SetValueWithoutNotify(currentMeshIndex);
    }

    private void ChangeShellMeshPrev()
    {
        currentMeshIndex -= 1;
        if (currentMeshIndex < 0) currentMeshIndex = hyperspaceShellMeshes.Count - 1;

        ChangeShellMesh(currentMeshIndex);
        dropdownMeshes.SetValueWithoutNotify(currentMeshIndex);
    }

    private void ChangeShellMesh(int meshIndex)
    {
        currentMeshIndex = meshIndex;
        currentPrefab.hyperspaceShellRenderer.GetComponent<MeshFilter>().mesh = hyperspaceShellMeshes[meshIndex].meshReference;
    }

    private void PlayTransitionIn()
    {
        currentPrefab.StartTransitionIn();
    }

    private void PlayTransitionOut()
    {
        currentPrefab.StartTransitionOut();
    }

    private void ChangeTransitionEffect(float newValue)
    {
        currentPrefab.hyperspaceShellRenderer.material.SetFloat("_Transition", newValue);
    }

    private void ChangeShellOpacity(float newValue)
    {
        currentPrefab.hyperspaceShellRenderer.material.SetFloat("_ShellOpacity_Value", newValue);
    }

    private void ChangeLightIntensity(float newValue)
    {
        currentPrefab.hyperspaceShellRenderer.material.SetColor("_Light1_Color", currentPrefabInitialLight1Color * newValue);
        currentPrefab.hyperspaceShellRenderer.material.SetColor("_Light2_Color", currentPrefabInitialLight2Color * newValue);
    }

    private void ChangeFlashingIntensity(float newValue)
    {
        currentPrefab.flashingIntensity = newValue;
    }

    private void ChangeFlasingFrequency(float newValue)
    {
        currentPrefab.flashingPeriodRange = currentPrefabInitialFlashingPeriods / (newValue * newValue);
        currentPrefab.RestartFlashingCoroutine();
    }

    private void ChangeDisplacementIntensity(float newValue)
    {
        currentPrefab.hyperspaceShellRenderer.material.SetFloat("_Displacement_Intensity", newValue);
    }
}
