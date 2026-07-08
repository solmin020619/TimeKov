// =====================================================================
// Schemas/ConsumableEffectSchema.cs
// 소모품 효과 상세 테이블 스키마
// ItemData 에서 TacticalConsumable 카테고리인 아이템의 효과를 정의
// itemId 가 ItemDataTable 과 1:1 대응한다
// =====================================================================

public class ConsumableEffectSchema : SheetSchema
{
    private const string URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vT7uMlIN0rbPXjGNR2u7jbh-e-KOWS9Np2NQU-d1caw9jSOG63-IQAl85stEhF5g-Pd-eFYtyfe8WxT/pub?output=csv";

    public ConsumableEffectSchema() : base("ConsumableEffect", URL)
    {
        // 아이템 ID (PK, FK → ItemData)
        // ItemDataTable 의 itemId 와 정확히 일치해야 한다
        AddRef("itemId", "ItemData", required: true, isKey: true);

        // 소모품 효과 분류 — Heal / SustainHeal / Buff / Stamina / Special
        AddEnum<ConsumableType>("consumableType", required: true);

        // 효과가 적용되는 대상 스탯
        // Time / ATK / MoveSpeed / Stamina / SkillGauge / TimeDecay / AllStats / DashStamina
        AddEnum<EffectTarget>("effectTarget", required: true);

        // 수치 적용 방식
        // Flat = 고정값, Percent = 현재값 기준 %, MaxPercent = 최대치 기준 %
        AddEnum<EffectValueType>("effectValueType", required: true);

        // 효과 수치 (effectValueType 에 따라 해석 방법이 다름)
        Add("effectValue", ColumnType.Float, required: true);

        // 효과 지속 시간(초) — 즉시 효과(Heal 등)는 0 으로 입력
        Add("duration", ColumnType.Float, required: true);
    }
}