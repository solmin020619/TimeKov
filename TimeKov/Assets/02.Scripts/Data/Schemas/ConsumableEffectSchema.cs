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

        // 이 앰플로 올릴 수 있는 스탯 천장. 0 = 무제한.
        //
        // [왜 있나] 싼 초급 앰플만 대량 생산해서 스탯을 도배하면 중급/고급이 필요 없어진다.
        //   티어별로 "여기까지만" 을 두면 다음 티어로 갈아탈 이유가 생긴다.
        //   예) 초급 공격력 앰플 16 -> 공격력이 16 이상이면 더는 안 오른다(중급이 필요).
        // [판정] PermanentStat 계열에만 의미가 있다. 즉시 회복(Heal)은 스탯이 아니라 무관.
        // [거부 시] 아이템을 소모하지 않는다(ConsumableEffectApplier 참고).
        // 선택 컬럼이라 시트에 없어도 로드는 통과한다(그 경우 전부 0 = 무제한).
        Add("maxStatValue", ColumnType.Float, required: false);
    }
}