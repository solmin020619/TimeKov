using UnityEngine;
using UnityEngine.Rendering;

public class QuarterViewCameraController : MonoBehaviour
{

    [Header("Target")]
    public Transform target;                                // 따라갈 대상(플레이어)

    [Header("View Settings")]
    public Vector3 offset = new Vector3(0f, 10f, -8f);      // 플레이어 기준 위치 오프셋
    public float followSpeed = 20f;                         // 카메라가 따라가는 속도
    public float lookHeightOffset = 1.5f;                   // 플레이어 위 어느 지점을 볼지

    private void LateUpdate()
    {
        if(target == null) return;

        // 목표 위치 = 플레이어 위치 + 오프셋
        transform.position = target.position + offset;

        // 2) 회전 : 플레이어를 바라보기만 한다
        Vector3 lookPoint = target.position + Vector3.up * lookHeightOffset;
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);    
    }   
}
