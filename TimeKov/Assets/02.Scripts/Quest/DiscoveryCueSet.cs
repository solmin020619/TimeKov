using System;
using System.Collections.Generic;
using UnityEngine;

// 상황별 발견 팝업 데이터셋.
//   특정 이벤트(설비 해금 / 귀환석 획득 / 전송기 상호작용 / 아이템 획득)가 '처음' 발생할 때
//   해당 큐의 영상+설명 팝업(TutorialVideoUI)을 1회 띄운다. 퀘스트 순서와 무관(이벤트 구동).
//   - 시청 기록은 기존 도감 시스템(CodexDiscovery, 첫 페이지 title 기준)을 재사용 -> 세이브 별도 필드 불필요.
//   - 도감 튜토 탭에서 재시청 가능(빌더가 큐 페이지를 CodexTutorialConfig 로 복사).
//   Resources/DiscoveryCues/DiscoveryCueSet 에 두면 DiscoveryCueManager 가 자동 로드.
[CreateAssetMenu(fileName = "DiscoveryCueSet", menuName = "TIMEKOV/Discovery Cue Set")]
public class DiscoveryCueSet : ScriptableObject
{
    public List<DiscoveryCue> cues = new List<DiscoveryCue>();
}

[Serializable]
public class DiscoveryCue
{
    [Tooltip("디스패처가 이벤트를 이 키로 바꿔 매칭.\n" +
             "  facility:<id>   설비 해금 (예 facility:6)\n" +
             "  returnstone     귀환석 첫 획득\n" +
             "  interact:<id>   상호작용 (예 interact:transmit)\n" +
             "  item:<id>       아이템 첫 획득 (예 item:1102)")]
    public string cueKey;

    [Tooltip("기지(안전지대) 안에서 나는 이벤트면 true = 즉시 팝업.\n" +
             "필드(안전지대 밖)면 false = 거점 복귀 시점까지 미뤘다 팝업(전투/이동 중 방해 방지).")]
    public bool safe = true;

    [Tooltip("팝업 페이지들(영상 + 제목 + 본문). title 은 전역 유일해야 함(시청 기록 키).")]
    public VideoTutorialPage[] pages;

    public bool HasContent => pages != null && pages.Length > 0;
    public string FirstTitle => (pages != null && pages.Length > 0 && pages[0] != null) ? pages[0].title : null;
}
