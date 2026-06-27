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
}
