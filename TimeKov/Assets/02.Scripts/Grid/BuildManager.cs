using System.Collections.Generic; // HashSet 같은 컬렉션 자료구조를 사용하기 위해 필요
using UnityEngine; 

public class BuildManager : MonoBehaviour // 건축 모드, 프리뷰, 설치를 전체적으로 관리하는 클래스
{
    [System.Serializable] // 이 클래스가 인스펙터에 보이도록 직렬화 가능하게 만듦
    public class BuildItem // 건축 가능한 아이템 1개 정보를 담는 클래스
    {
        public string itemName; // 아이템 이름
        public GameObject prefab; // 실제 설치할 프리팹
    }

    [Header("References")] // 인스펙터에서 묶음 제목 표시
    public Camera mainCam; // 마우스 위치에서 Ray를 쏠 카메라
    public PlayerBuildZoneChecker zoneChecker; // 플레이어가 건축 가능 구역 안에 있는지 확인하는 컴포넌트
    public Transform buildParent; // 설치된 건물들을 정리해서 넣어둘 부모 오브젝트

    [Header("Build List (1~5 keys)")] // 인스펙터에서 건축 아이템 목록 구분용 제목
    public BuildItem[] buildItems; // 숫자키 1~5로 선택할 건축 아이템 배열

    [Header("Preview")] // 인스펙터에서 프리뷰 관련 항목 구분용 제목
    public GameObject previewMarker; // 설치 가능 위치를 미리 보여주는 프리뷰 오브젝트

    [Header("Raycast")] // 인스펙터에서 레이캐스트 관련 항목 구분용 제목
    public LayerMask groundMask; // 바닥 판정에 사용할 레이어 마스크
    public float rayDistance = 300f; // 레이캐스트 최대 거리

    [Header("Grid")] // 인스펙터에서 그리드 관련 항목 구분용 제목
    public float cellSize = 1f; // 한 칸의 크기, 1이면 1x1 그리드
    public float fixedY = 0f; // 건축이 가능한 고정 높이값
    public float yTolerance = 0.1f; // 높이 오차 허용 범위

    [Header("Build Check")] // 인스펙터에서 설치 가능 여부 체크 관련 항목 구분용 제목
    public LayerMask blockingMask; // 설치를 막는 오브젝트를 검사할 레이어 마스크
    public Vector3 checkHalfExtents = new Vector3(0.45f, 0.45f, 0.45f); // CheckBox의 반 크기, 거의 1칸 크기 검사

    public bool IsBuildMode { get; private set; } // 현재 건축 모드인지 외부에서 읽기만 가능하게 공개

    private int currentIndex = 0; // 현재 선택된 건축 아이템 인덱스
    private int currentRotationY = 0; // 현재 건축물의 Y축 회전값, 0/90/180/270 식으로 사용

    private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>(); // 이미 설치된 그리드 칸 정보를 저장하는 집합

    private void Start() // 게임 시작 시 한 번 호출
    {
        RefreshPreviewMarker();

        if (previewMarker != null) // 프리뷰 오브젝트가 연결되어 있으면
            previewMarker.SetActive(false); // 시작할 때는 프리뷰를 꺼둠
    }

    private void Update() // 매 프레임마다 호출
    {
        HandleModeInput(); // 건축 모드 켜기/끄기 입력 처리

        if (!IsBuildMode) // 현재 건축 모드가 아니면
            return; // 아래 건축 관련 로직은 실행하지 않고 종료

        HandleSelectInput(); // 숫자키로 건축 아이템 선택 입력 처리
        HandleRotateInput(); // R키로 회전 입력 처리
        HandleBuild(); // 실제 건축 가능 여부 계산 및 설치 처리
    }

    private void HandleModeInput() // 건축 모드 진입/해제 입력 처리 함수
    {
        if (Input.GetKeyDown(KeyCode.B)) // B키를 눌렀으면
        {
            IsBuildMode = !IsBuildMode; // 건축 모드를 토글함, 켜져 있으면 끄고 꺼져 있으면 켬

            if (!IsBuildMode) // 방금 건축 모드가 꺼졌다면
            {
                if (previewMarker != null) // 프리뷰 오브젝트가 있으면
                    previewMarker.SetActive(false); // 프리뷰를 비활성화
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) // Esc키 또는 우클릭을 했으면
        {
            IsBuildMode = false; // 강제로 건축 모드를 종료

            if (previewMarker != null) // 프리뷰 오브젝트가 있으면
                previewMarker.SetActive(false); // 프리뷰도 꺼줌
        }
    }

    private void HandleSelectInput() // 숫자키 입력으로 설치할 아이템 선택
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetCurrentItem(0); // 1번 키를 누르면 0번 아이템 선택
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetCurrentItem(1); // 2번 키를 누르면 1번 아이템 선택
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetCurrentItem(2); // 3번 키를 누르면 2번 아이템 선택
        if (Input.GetKeyDown(KeyCode.Alpha4)) SetCurrentItem(3); // 4번 키를 누르면 3번 아이템 선택
        if (Input.GetKeyDown(KeyCode.Alpha5)) SetCurrentItem(4); // 5번 키를 누르면 4번 아이템 선택
    }

    private void HandleRotateInput() // 회전 입력 처리 함수
    {
        if (Input.GetKeyDown(KeyCode.R)) // R키를 눌렀으면
        {
            currentRotationY += 90; // Y축 회전을 90도 증가시킴
            if (currentRotationY >= 360) // 회전값이 360도 이상이면
                currentRotationY = 0; // 다시 0도로 초기화
        }
    }

    private void HandleBuild() // 실제 건축 위치 계산, 설치 가능 여부 판정, 설치 실행을 담당
    {
        if (mainCam == null || buildItems == null || buildItems.Length == 0) // 카메라가 없거나 설치 목록이 비어 있으면
            return; // 더 진행할 수 없으므로 종료

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition); // 마우스 위치를 기준으로 카메라에서 Ray 생성

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, groundMask)) // 바닥 레이어에 레이가 맞지 않았다면
        {
            SetPreviewActive(false); // 프리뷰를 꺼줌
            return; // 더 진행하지 않고 종료
        }

        Vector3 snappedPos = SnapToGrid(hit.point); // 레이가 맞은 위치를 그리드에 맞게 보정
        Quaternion rotation = Quaternion.Euler(0f, currentRotationY, 0f); // 현재 회전값으로 회전 Quaternion 생성

        bool isInBuildZone = zoneChecker != null && zoneChecker.IsInBuildZone; // 플레이어가 건축 가능 구역 안에 있는지 확인
        bool isCorrectHeight = Mathf.Abs(hit.point.y - fixedY) <= yTolerance; // 맞은 지점의 y값이 고정 높이와 허용 오차 내인지 확인
        bool isOccupied = occupiedCells.Contains(WorldToCell(snappedPos)); // 이미 해당 칸에 설치된 적이 있는지 확인
        bool isBlocked = Physics.CheckBox(snappedPos, checkHalfExtents, rotation, blockingMask); // 해당 공간에 막는 오브젝트가 있는지 물리 검사

        bool canBuild = isInBuildZone && isCorrectHeight && !isOccupied && !isBlocked; // 모든 조건을 만족하면 설치 가능

        UpdatePreview(snappedPos, rotation, canBuild); // 프리뷰 위치, 회전, 색상 업데이트

        if (Input.GetMouseButtonDown(0) && canBuild) // 좌클릭했고 설치 가능 상태라면
        {
            PlaceCurrentItem(snappedPos, rotation); // 현재 선택된 아이템을 해당 위치에 설치
        }
    }

    private void SetCurrentItem(int index) // 현재 설치할 아이템을 변경하는 함수
    {
        if (buildItems == null || index < 0 || index >= buildItems.Length) // 배열이 없거나 인덱스가 범위를 벗어나면
            return; // 잘못된 접근이므로 종료

        if (buildItems[index] == null || buildItems[index].prefab == null) // 해당 칸의 아이템 정보나 프리팹이 비어 있으면
            return; // 선택하지 않고 종료

        currentIndex = index; // 현재 선택 아이템 인덱스를 변경
        RefreshPreviewMarker();
    }

    private void PlaceCurrentItem(Vector3 position, Quaternion rotation) // 실제 프리팹을 생성하는 함수
    {
        GameObject prefab = buildItems[currentIndex].prefab; // 현재 선택된 아이템의 프리팹을 가져옴
        if (prefab == null) // 프리팹이 없으면
            return; // 설치할 수 없으므로 종료

        GameObject obj = Instantiate(prefab, position, rotation, buildParent); // 프리팹을 생성하고 부모를 buildParent로 설정
        occupiedCells.Add(WorldToCell(position)); // 설치한 위치의 칸을 점유 상태로 기록

        if (blockingMask.value != 0) // blockingMask가 설정되어 있다면
        {
            // 설치된 건물이 blockingMask에 걸리도록
            // prefab의 레이어를 미리 BuildBlock 같은 걸로 맞춰두는 걸 추천
        }
    }

    private Vector3 SnapToGrid(Vector3 worldPos) // 월드 좌표를 그리드 중앙 좌표로 보정하는 함수
    {
        float x = Mathf.Floor(worldPos.x / cellSize) * cellSize + cellSize * 0.5f; // x좌표를 칸 단위로 내림 후 중앙으로 보정
        float z = Mathf.Floor(worldPos.z / cellSize) * cellSize + cellSize * 0.5f; // z좌표를 칸 단위로 내림 후 중앙으로 보정

        return new Vector3(x, fixedY, z); // 보정된 x,z와 고정 y값으로 최종 위치 반환
    }

    private Vector2Int WorldToCell(Vector3 worldPos) // 월드 좌표를 그리드 칸 좌표로 변환하는 함수
    {
        int x = Mathf.FloorToInt(worldPos.x / cellSize); // x좌표가 몇 번째 칸인지 계산
        int z = Mathf.FloorToInt(worldPos.z / cellSize); // z좌표가 몇 번째 칸인지 계산
        return new Vector2Int(x, z); // 계산된 칸 좌표를 반환
    }

    private void UpdatePreview(Vector3 position, Quaternion rotation, bool canBuild) // 프리뷰의 위치, 회전, 색상을 갱신하는 함수
    {
        if (previewMarker == null) // 프리뷰 오브젝트가 없으면
            return; // 처리할 수 없으므로 종료

        previewMarker.SetActive(true); // 프리뷰를 켬
        previewMarker.transform.position = position; // 프리뷰 위치를 설치 예정 위치로 이동
        previewMarker.transform.rotation = rotation; // 프리뷰 회전을 현재 회전값으로 설정

        Renderer[] renderers = previewMarker.GetComponentsInChildren<Renderer>(); // 프리뷰 자신과 자식들의 렌더러를 전부 가져옴
        for (int i = 0; i < renderers.Length; i++) // 모든 렌더러를 순회
        {
            if (renderers[i].material.HasProperty("_Color")) // 머티리얼에 Color 속성이 있으면
                renderers[i].material.color = canBuild ? Color.green : Color.red; // 설치 가능이면 초록, 불가능이면 빨강으로 표시
        }
    }
    private void RefreshPreviewMarker()
    {
        if (previewMarker != null)
            Destroy(previewMarker);

        if (buildItems == null || buildItems.Length == 0)
            return;

        if (buildItems[currentIndex] == null || buildItems[currentIndex].prefab == null)
            return;

        previewMarker = Instantiate(buildItems[currentIndex].prefab);

        previewMarker.name = buildItems[currentIndex].itemName + "_Preview";

        Collider[] colliders = previewMarker.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] rigidbodies = previewMarker.GetComponentsInChildren<Rigidbody>();
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
        }

        MonoBehaviour[] behaviours = previewMarker.GetComponentsInChildren<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != this)
                behaviours[i].enabled = false;
        }

        previewMarker.SetActive(false);
    }

    private void SetPreviewActive(bool value) // 프리뷰 활성화/비활성화만 따로 처리하는 함수
    {
        if (previewMarker != null) // 프리뷰 오브젝트가 있으면
            previewMarker.SetActive(value); // 전달받은 값대로 활성/비활성 설정
    }

    public string GetCurrentItemName() // 현재 선택된 아이템 이름을 반환하는 함수
    {
        if (buildItems == null || buildItems.Length == 0) // 아이템 배열이 없거나 비어 있으면
            return "None"; // 선택된 아이템이 없다고 반환

        if (buildItems[currentIndex] == null) // 현재 인덱스의 아이템 정보가 비어 있으면
            return "None"; // 선택된 아이템이 없다고 반환

        return buildItems[currentIndex].itemName; // 현재 선택된 아이템 이름 반환
    }
}