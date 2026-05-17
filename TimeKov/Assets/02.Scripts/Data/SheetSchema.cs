// =====================================================================
// SheetSchema.cs
// 컬럼 타입 정의, 컬럼 메타데이터, 스키마 베이스 클래스 통합 파일
// 각 테이블 스키마는 SheetSchema 를 상속해서 정의한다
// CodeGenerator 가 이 정보를 읽어 .g.cs 파일을 자동 생성한다
// =====================================================================

using System;
using System.Collections.Generic;

// 컬럼이 담는 데이터 종류
public enum ColumnType
{
    String, // 문자열
    Int,    // 정수
    Float,  // 실수
    Bool,   // 참/거짓 (0 or 1)
    Ref,    // 다른 테이블 참조 (FK) — RefTable 에 대상 테이블명 저장
    Enum    // C# enum — EnumType 에 실제 타입 저장
}

// 컬럼 하나의 메타데이터
// CodeGenerator 가 이 정보를 읽어 필드 타입과 파싱 코드를 결정한다
public class ColumnSchema
{
    // 컬럼 이름 (CSV 헤더와 정확히 일치해야 한다)
    public string Name { get; }

    // 컬럼 타입
    public ColumnType Type { get; }

    // true 면 빈 값이 있을 때 Validator 가 에러를 낸다
    public bool Required { get; }

    // true 면 이 컬럼이 Dictionary 의 키로 사용된다
    // 2개 이상이면 복합키로, "_" 로 이어붙인 문자열이 키가 된다
    public bool IsKey { get; }

    // true 면 "-" 값을 기본값(0 or "")으로 처리한다
    // 선택적 데이터를 나타낼 때 사용한다
    public bool AllowDash { get; }

    // Type == Ref 일 때 참조하는 테이블 이름
    public string RefTable { get; }

    // Type == Enum 일 때 실제 C# enum 타입
    public Type EnumType { get; }

    // 일반 컬럼 (String / Int / Float / Bool)
    public ColumnSchema(string name, ColumnType type, bool required, bool isKey, bool allowDash)
    {
        Name = name;
        Type = type;
        Required = required;
        IsKey = isKey;
        AllowDash = allowDash;
    }

    // FK 컬럼 (Ref)
    public ColumnSchema(string name, string refTable, bool required, bool isKey, bool allowDash)
    {
        Name = name;
        Type = ColumnType.Ref;
        RefTable = refTable;
        Required = required;
        IsKey = isKey;
        AllowDash = allowDash;
    }

    // Enum 컬럼
    public ColumnSchema(string name, Type enumType, bool required, bool isKey, bool allowDash)
    {
        Name = name;
        Type = ColumnType.Enum;
        EnumType = enumType;
        Required = required;
        IsKey = isKey;
        AllowDash = allowDash;
    }
}

// 시트 스키마 베이스 클래스
// 각 테이블은 이 클래스를 상속해서 생성자에서 컬럼을 등록한다
public abstract class SheetSchema
{
    // 테이블 이름 (생성될 클래스명의 접두어가 된다)
    // 예: "ItemData" -> ItemDataSheetId, ItemDataSheetData, ItemDataParser
    public string TableName { get; }

    // 구글 시트 웹 게시 URL (CSV 형식)
    public string GoogleSheetUrl { get; }

    // 등록된 컬럼 목록 (순서가 CSV 컬럼 순서와 일치해야 한다)
    public List<ColumnSchema> Columns { get; } = new List<ColumnSchema>();

    protected SheetSchema(string tableName, string googleSheetUrl)
    {
        TableName = tableName;
        GoogleSheetUrl = googleSheetUrl;
    }

    // 일반 컬럼 등록
    protected void Add(string name, ColumnType type,
        bool required = false, bool isKey = false, bool allowDash = false)
    {
        Columns.Add(new ColumnSchema(name, type, required, isKey, allowDash));
    }

    // FK 컬럼 등록
    protected void AddRef(string name, string refTable,
        bool required = false, bool isKey = false, bool allowDash = false)
    {
        Columns.Add(new ColumnSchema(name, refTable, required, isKey, allowDash));
    }

    // Enum 컬럼 등록 (제네릭으로 타입 안전 보장)
    protected void AddEnum<T>(string name,
        bool required = false, bool isKey = false, bool allowDash = false) where T : Enum
    {
        Columns.Add(new ColumnSchema(name, typeof(T), required, isKey, allowDash));
    }

    // 키 컬럼만 반환
    public List<ColumnSchema> GetKeyColumns()
    {
        return Columns.FindAll(c => c.IsKey);
    }

    // 복합키 여부
    public bool IsCompositeKey => GetKeyColumns().Count > 1;
}