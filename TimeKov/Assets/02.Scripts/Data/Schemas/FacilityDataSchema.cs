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

        // 설비 유형 — 코드에서 동작 방식을 결정하는 데 사용
        // BioExtractor / MineralCrusher / ChemRefinery / MatterFuser
        // TacticalMfg / BioInjector / CoreFusion / VoidAssembler
        AddEnum<FacilityType>("facilityType", required: true);

        // 그리드 가로 칸 수
        Add("gridW", ColumnType.Int, required: true);

        // 그리드 세로 칸 수
        Add("gridH", ColumnType.Int, required: true);

        // 재료 투입 슬롯 수 (UI 슬롯 개수 결정)
        Add("inputSlotCount", ColumnType.Int, required: true);

        // 결과물 출력 슬롯 수
        Add("outputSlotCount", ColumnType.Int, required: true);

        // 전력 필요 여부 (0 = 불필요, 1 = 필요)
        Add("requiresPower", ColumnType.Bool, required: true);

        // 회전 배치 가능 여부 (0 = 불가, 1 = 가능)
        Add("canRotate", ColumnType.Bool, required: true);

        // 최대 업그레이드 레벨
        // FacilityLevelDataTable 에 해당 레벨까지 행이 있어야 한다
        Add("maxLevel", ColumnType.Int, required: true);
    }
}