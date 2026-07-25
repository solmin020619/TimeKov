// =====================================================================
// Schemas/FacilityDataSchema.cs
// 설비 기본 정보 테이블 스키마
// 기지에 설치 가능한 공장 설비 8종의 정적 데이터
// =====================================================================

public class FacilityDataSchema : SheetSchema
{
    private const string URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vRzn6rEn1qnF8yYZb8R_88SnFAVej3sFwDXwhYfY5663Hn5oTmriLz1nqr8dExb6cIuRiSJELBtaJpd/pub?output=csv";

    public FacilityDataSchema() : base("FacilityData", URL)
    {
        // 설비 고유 ID (PK, 1~8)
        Add("facilityId", ColumnType.Int, required: true, isKey: true);

        // 설비 이름 (UI 표시용)
        Add("facilityName", ColumnType.String, required: true);

        // 그리드 가로 칸 수
        Add("gridW", ColumnType.Int, required: true);

        // 그리드 세로 칸 수
        Add("gridH", ColumnType.Int, required: true);

        // 회전 배치 가능 여부 (0 = 불가, 1 = 가능)
        Add("canRotate", ColumnType.Bool, required: true);

        // 최대 업그레이드 레벨
        // FacilityLevelDataTable 에 해당 레벨까지 행이 있어야 한다
        Add("maxLevel", ColumnType.Int, required: true);

        // 설비 아이콘 키 — Resources/Facilities/{iconKey} 에서 스프라이트 로드.
        // 선택 컬럼: 비어 있으면 FacilityIconDatabase 의 인스펙터 수동매핑으로 폴백한다.
        Add("iconKey", ColumnType.String, required: false);

        // 빌드 퀵슬롯 위치(키 번호 1~9). 시트만 고치면 건축바 순서가 바뀐다(아이콘/도면과 같은 방식).
        // 선택 컬럼(문자열로 받아 코드에서 파싱): 비어 있으면 facilityId 순(facilityId N번 = 키 N번)으로 폴백.
        Add("buildSlot", ColumnType.String, required: false);
    }
}