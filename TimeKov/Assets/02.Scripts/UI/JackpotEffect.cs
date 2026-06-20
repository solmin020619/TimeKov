using UnityEngine;

// 공장 잭팟(마스터 레시피가 10% 확률로 2배 제작) 연출.
// RecipeProgress.OnJackpot 을 구독해, 발동한 설비 위치에 "잭팟! x2" 플로팅 텍스트를 띄운다.
// 정적 부트스트랩 - 씬 세팅/프리팹 불필요. 도감서 잭팟을 활성화한 레시피만 OnJackpot 이 발생한다.
public static class JackpotEffect
{
    // 발동 설비 머리 위로 살짝 띄울 높이(월드 단위).
    const float HeadOffset = 1.4f;
    static readonly Color Gold = new Color(1f, 0.82f, 0.2f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        // 도메인 리로드 비활성 환경서 중복 구독 방지(한 번만 연결).
        RecipeProgress.OnJackpot -= OnJackpot;
        RecipeProgress.OnJackpot += OnJackpot;
    }

    static void OnJackpot(int facilityId, int itemId, Vector3 worldPos)
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 sp = cam.WorldToScreenPoint(worldPos + Vector3.up * HeadOffset);
        if (sp.z < 0f) return;   // 카메라 뒤(설비가 화면 뒤쪽)면 스킵
        FloatingTextManager.Show("<size=130%>잭팟!</size> x2", Gold, sp);
    }
}
