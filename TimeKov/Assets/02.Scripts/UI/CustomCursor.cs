using UnityEngine;

public class CustomCursor : MonoBehaviour
{
    private void Update()
    {
        // UI 이미지가 실제 마우스 좌표를 실시간으로 따라가게 만듭니다.
        transform.position = Input.mousePosition;
    }
}