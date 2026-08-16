// =====================================================================
// SaveSlotMeta.cs
// 월드 선택 목록 화면에 보여줄 가벼운 메타데이터. meta.json에 저장.
// 전체 save.json(진행 데이터)을 읽지 않고도 목록을 빠르게 표시하기 위해 분리.
// =====================================================================

using System;

[Serializable]
public class SaveSlotMeta
{
    public string slotId;        // 폴더명과 동일 (예: "World_1")
    public string worldName;     // 플레이어가 지정한 월드 이름
    public string createdAtIso;  // 생성 시각 (ISO 8601)
    public string lastPlayedIso; // 마지막 플레이 시각 (ISO 8601)
    public int coreLevelSnapshot; // 목록에 "강화 Lv.N" 정도로 보여주기 위한 캐시(저장 시 갱신)

    // 아직 프롤로그를 안 본 월드인가. 월드 선택 화면이 이 값을 보고 Prologue / World 중
    // 어디로 보낼지 정한다. 프롤로그가 끝나는 순간 false 로 바뀐다.
    //
    // ★기본값이 false 여야 한다. 이 필드가 없던 시절에 만든 세이브는 JsonUtility 가
    //   false 로 읽는데, 그 월드들은 이미 프롤로그를 지난 뒤라 "필요 없음"이 정답이다.
    //   (반대로 이름을 prologueDone 으로 두면 옛 세이브가 전부 프롤로그를 다시 본다)
    public bool needsPrologue;
}
