// =====================================================================
// SingleInstanceAttribute.cs
// "이 컴포넌트는 씬에 하나만 있어야 한다"를 코드로 못 박는 표시.
//
// [왜 속성으로 선언하나 - 추측을 없애려고]
//   처음엔 리플렉션으로 'static Instance 필드가 있으면 싱글톤'이라고 추측했는데 양쪽으로 다 틀렸다.
//     - 거짓 양성: InventoryManager 는 Instance(가방)/StorageInstance(창고)/ChestInstance(상자)를
//                  역할별로 셋 두는 설계다. 정상 배치 3개를 중복이라고 신고했다(지웠으면 인벤토리 사망).
//     - 거짓 음성: `private static X _i;` + `public static X I => _i;` 는 같은 인스턴스인데
//                  필드+프로퍼티라 슬롯 2개로 세어져, 진짜 싱글톤이 검사에서 통째로 빠졌다.
//   ★중복 판정은 틀리면 멀쩡한 UI 를 지우게 만드는 작업이라 추측이 섞이면 안 된다.
//   그래서 판정을 코드 선언으로 옮겼다. 붙어 있으면 단일, 없으면 검사 대상 아님. 끝.
//
// [붙이는 기준]
//   Awake 에서 UIDuplicateGuard.Report(...) 로 중복을 파괴하는 클래스 = 붙인다.
//   역할별로 여러 개 두는 매니저(InventoryManager 류) = 붙이지 않는다.
// =====================================================================

using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SingleInstanceAttribute : Attribute
{
}
