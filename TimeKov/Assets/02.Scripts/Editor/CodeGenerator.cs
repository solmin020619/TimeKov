// =====================================================================
// Editor/CodeGenerator.cs
// 스키마를 읽어 .g.cs 파일 4종을 자동 생성하는 에디터 도구
// TableBuildMenu 에서 호출하거나 직접 CodeGenerator.GenerateAll() 호출
// 생성 대상: {Name}SheetId / {Name}SheetData / {Name}Parser / GameDataHolder
// =====================================================================

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CodeGenerator
{
    // 생성 파일 출력 경로 (Assets 기준)
    private const string OUTPUT_PATH = "Assets/02.Scripts/Data/Generated";

    // 전체 생성 실행
    public static void GenerateAll()
    {
        var schemas = AllTableSchemas.GetAll();
        Directory.CreateDirectory(OUTPUT_PATH);

        foreach (var schema in schemas)
        {
            GenerateSheetId(schema);
            GenerateSheetData(schema);
            GenerateParser(schema);
        }

        GenerateGameDataHolder(schemas);
        AssetDatabase.Refresh();
        Debug.Log($"[CodeGenerator] 생성 완료 — {schemas.Count}개 테이블");
    }

    // {Name}SheetId.g.cs — readonly struct, IEquatable, implicit/explicit string 변환
    private static void GenerateSheetId(SheetSchema schema)
    {
        var name = schema.TableName;
        var keys = schema.GetKeyColumns();
        var sb = new StringBuilder();

        sb.AppendLine("// 자동 생성 파일 — 직접 수정 금지 (메뉴 '시트 > 코드 다시 만들기' 로 재생성)");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"public readonly struct {name}SheetId : IEquatable<{name}SheetId>");
        sb.AppendLine("{");
        sb.AppendLine("    // 딕셔너리 키로 사용되는 문자열 값");
        sb.AppendLine("    private readonly string _value;");
        sb.AppendLine();

        if (schema.IsCompositeKey)
        {
            // 복합키 — 각 키 컬럼을 파라미터로 받아 "_" 로 이어붙임
            var paramList = new List<string>();
            var argList = new List<string>();
            foreach (var col in keys)
            {
                string paramName = char.ToLower(col.Name[0]) + col.Name.Substring(1);
                paramList.Add($"{GetCSharpTypeName(col)} {paramName}");
                argList.Add(paramName);
            }
            sb.AppendLine($"    public {name}SheetId({string.Join(", ", paramList)})");
            sb.AppendLine("    {");
            sb.AppendLine($"        _value = string.Join(\"_\", new object[] {{ {string.Join(", ", argList)} }});");
            sb.AppendLine("    }");
            sb.AppendLine();
            // 수정: explicit cast 전용 private string 생성자 추가
            sb.AppendLine($"    // 이미 조합된 문자열로 직접 생성 (explicit cast 전용)");
            sb.AppendLine($"    private {name}SheetId(string rawValue, bool direct) {{ _value = rawValue; }}");
        }
        else
        {
            // 단일키
            sb.AppendLine($"    public {name}SheetId(string value)");
            sb.AppendLine("    {");
            sb.AppendLine("        _value = value;");
            sb.AppendLine("    }");
        }

        sb.AppendLine();
        sb.AppendLine("    // string 으로 implicit 변환 (읽기 편의)");
        sb.AppendLine($"    public static implicit operator string({name}SheetId id) => id._value;");
        sb.AppendLine();
        sb.AppendLine("    // string 에서 explicit 변환 (실수 방지)");

        if (schema.IsCompositeKey)
            // 수정: private (string, bool) 생성자로 explicit cast
            sb.AppendLine($"    public static explicit operator {name}SheetId(string value) => new {name}SheetId(value, true);");
        else
            sb.AppendLine($"    public static explicit operator {name}SheetId(string value) => new {name}SheetId(value);");

        sb.AppendLine($"    public bool Equals({name}SheetId other) => _value == other._value;");
        sb.AppendLine("    public override bool Equals(object obj) =>");
        sb.AppendLine($"        obj is {name}SheetId other && Equals(other);");
        sb.AppendLine("    public override int GetHashCode() => _value?.GetHashCode() ?? 0;");
        sb.AppendLine("    public override string ToString() => _value;");
        sb.AppendLine("}");

        WriteFile($"{name}SheetId.g.cs", sb.ToString());
    }

    // {Name}SheetData.g.cs — [Serializable] 데이터 클래스
    private static void GenerateSheetData(SheetSchema schema)
    {
        var name = schema.TableName;
        var sb = new StringBuilder();

        sb.AppendLine("// 자동 생성 파일 — 직접 수정 금지 (메뉴 '시트 > 코드 다시 만들기' 로 재생성)");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine("[Serializable]");
        sb.AppendLine($"public class {name}SheetData");
        sb.AppendLine("{");
        sb.AppendLine($"    // 이 데이터의 키 (Dictionary 조회용)");
        sb.AppendLine($"    public {name}SheetId SheetId;");
        sb.AppendLine();

        foreach (var col in schema.Columns)
        {
            if (col.IsKey) continue;

            if (col.Type == ColumnType.Ref)
                sb.AppendLine($"    // FK → {col.RefTable}");
            else if (col.Type == ColumnType.Enum)
                sb.AppendLine($"    // Enum: {col.EnumType.Name}");

            sb.AppendLine($"    public {GetCSharpTypeName(col)} {col.Name};");
        }

        sb.AppendLine("}");
        WriteFile($"{name}SheetData.g.cs", sb.ToString());
    }

    // {Name}Parser.g.cs — 리플렉션 없이 컬럼 인덱스 직접 접근
    private static void GenerateParser(SheetSchema schema)
    {
        var name = schema.TableName;
        var keys = schema.GetKeyColumns();
        var sb = new StringBuilder();

        sb.AppendLine("// 자동 생성 파일 — 직접 수정 금지 (메뉴 '시트 > 코드 다시 만들기' 로 재생성)");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"public static class {name}Parser");
        sb.AppendLine("{");
        sb.AppendLine($"    public static Dictionary<string, {name}SheetData> Parse(CsvTable table)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var result = new Dictionary<string, {name}SheetData>();");
        sb.AppendLine();

        foreach (var col in schema.Columns)
            sb.AppendLine($"        int idx_{col.Name} = table.GetColumnIndex(\"{col.Name}\");");

        sb.AppendLine();
        sb.AppendLine("        foreach (var row in table.Rows)");
        sb.AppendLine("        {");
        sb.AppendLine($"            var data = new {name}SheetData();");
        sb.AppendLine();

        var keyReadParts = new List<string>();
        foreach (var col in keys)
        {
            sb.AppendLine($"            var key_{col.Name} = row.Get(idx_{col.Name});");
            keyReadParts.Add($"key_{col.Name}");
        }

        if (schema.IsCompositeKey)
        {
            // 수정: key_xxx 는 모두 string — string 연결 후 explicit cast
            string keyConcat = string.Join(" + \"_\" + ", keyReadParts);
            sb.AppendLine($"            // 복합키: string 연결 후 explicit cast");
            sb.AppendLine($"            data.SheetId = ({name}SheetId)({keyConcat});");
        }
        else
        {
            sb.AppendLine($"            data.SheetId = new {name}SheetId({keyReadParts[0]});");
        }

        sb.AppendLine();

        foreach (var col in schema.Columns)
        {
            if (col.IsKey) continue;
            string raw = $"row.Get(idx_{col.Name})";

            // 선택(required:false) 숫자 컬럼은 '없거나 빈 칸'을 0 으로 받는다.
            // 컬럼이 없으면 GetColumnIndex 가 -1 이고 row.Get(-1) 이 빈 문자열을 주는데,
            // int/float.Parse 는 빈 문자열에서 예외를 던져 로드 전체가 실패한다.
            // 코드를 먼저 넣고 시트는 나중에 고치는 순서를 쓰려면 이게 필요하다.
            bool lenientNumber = !col.Required && !col.IsKey;

            string parsed = col.Type switch
            {
                ColumnType.String => col.AllowDash
                    ? $"(row.Get(idx_{col.Name}) == \"-\" ? \"\" : row.Get(idx_{col.Name}))"
                    : raw,
                ColumnType.Int => lenientNumber
                    ? $"(int.TryParse({raw}, out var v_{col.Name}) ? v_{col.Name} : 0)"
                    : col.AllowDash
                        ? $"(row.Get(idx_{col.Name}) == \"-\" ? 0 : int.Parse(row.Get(idx_{col.Name})))"
                        : $"int.Parse({raw})",
                ColumnType.Float => lenientNumber
                    ? $"(float.TryParse({raw}, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v_{col.Name}) ? v_{col.Name} : 0f)"
                    : col.AllowDash
                        ? $"(row.Get(idx_{col.Name}) == \"-\" ? 0f : float.Parse(row.Get(idx_{col.Name}), System.Globalization.CultureInfo.InvariantCulture))"
                        : $"float.Parse({raw}, System.Globalization.CultureInfo.InvariantCulture)",
                ColumnType.Bool => $"({raw} == \"1\")",
                ColumnType.Ref => col.AllowDash
                    ? $"(row.Get(idx_{col.Name}) == \"-\" ? default : ({col.RefTable}SheetId)row.Get(idx_{col.Name}))"
                    : $"({col.RefTable}SheetId)({raw})",
                ColumnType.Enum => col.AllowDash
                    ? $"(row.Get(idx_{col.Name}) == \"-\" ? default : ({col.EnumType.Name})Enum.Parse(typeof({col.EnumType.Name}), row.Get(idx_{col.Name})))"
                    : $"({col.EnumType.Name})Enum.Parse(typeof({col.EnumType.Name}), {raw})",
                _ => raw
            };
            sb.AppendLine($"            data.{col.Name} = {parsed};");
        }

        sb.AppendLine();
        sb.AppendLine("            result[data.SheetId] = data;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        WriteFile($"{name}Parser.g.cs", sb.ToString());
    }

    // GameDataHolder.g.cs — DataHolder 프로퍼티 + partial LoadAll + partial ClearAll
    private static void GenerateGameDataHolder(List<SheetSchema> schemas)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// 자동 생성 파일 — 직접 수정 금지 (메뉴 '시트 > 코드 다시 만들기' 로 재생성)");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("public partial class GameDataHolder");
        sb.AppendLine("{");

        foreach (var schema in schemas)
            sb.AppendLine($"    public DataHolder<{schema.TableName}SheetData> {schema.TableName} {{ get; }} = new DataHolder<{schema.TableName}SheetData>();");

        sb.AppendLine();

        sb.AppendLine("    partial void LoadAll(Dictionary<string, CsvTable> tables)");
        sb.AppendLine("    {");
        foreach (var schema in schemas)
        {
            var n = schema.TableName;
            var v = char.ToLower(n[0]) + n.Substring(1);
            sb.AppendLine($"        if (tables.TryGetValue(\"{n}\", out var {v}Table))");
            sb.AppendLine($"            foreach (var pair in {n}Parser.Parse({v}Table))");
            sb.AppendLine($"                {n}.Add(pair.Key, pair.Value);");
        }
        sb.AppendLine("    }");

        sb.AppendLine();

        sb.AppendLine("    partial void ClearAll()");
        sb.AppendLine("    {");
        foreach (var schema in schemas)
            sb.AppendLine($"        {schema.TableName}.Clear();");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        WriteFile("GameDataHolder.g.cs", sb.ToString());
    }

    // 파일 쓰기 (내용이 같으면 스킵해서 불필요한 reimport 방지)
    private static void WriteFile(string fileName, string content)
    {
        string path = Path.Combine(OUTPUT_PATH, fileName);
        if (File.Exists(path) && File.ReadAllText(path) == content)
            return;
        // BOM 없는 UTF-8 로 쓴다. Encoding.UTF8 은 파일 앞에 BOM 을 붙이는데,
        // 이 프로젝트의 .cs 는 전부 BOM 없이 통일돼 있어서(08-09 인코딩 정리 + .editorconfig)
        // 그것만으로 26개 생성 파일이 전부 '변경됨'으로 잡혀 커밋해야 할 것처럼 보인다.
        File.WriteAllText(path, content, new UTF8Encoding(false));
        Debug.Log($"[CodeGenerator] 생성: {path}");
    }

    // 컬럼 타입을 C# 타입 이름으로 변환
    private static string GetCSharpTypeName(ColumnSchema col)
    {
        return col.Type switch
        {
            ColumnType.String => "string",
            ColumnType.Int => "int",
            ColumnType.Float => "float",
            ColumnType.Bool => "bool",
            ColumnType.Ref => $"{col.RefTable}SheetId",
            ColumnType.Enum => col.EnumType.Name,
            _ => "string"
        };
    }
}

#endif