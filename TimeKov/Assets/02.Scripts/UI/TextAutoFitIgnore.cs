using UnityEngine;

// 이 오브젝트의 TMP 텍스트는 TextAutoFit 이 건드리지 않는다.
//   일부러 상자 밖으로 넘치게 만든 연출(아이콘 위 겹침 숫자, 흐르는 자막 등)에 붙인다.
//   붙이기만 하면 되고 설정은 없다.
[DisallowMultipleComponent]
public class TextAutoFitIgnore : MonoBehaviour { }
