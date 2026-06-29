using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 패널/슬롯 위에 얹는 "흰 서리" 그라데이션 오버레이.
// 위(밝은 흰빛 0.10) -> 아래(투명)로 vertex 색을 보간해 유리 sheen 을 준다.
// 엔드필드 "맑은 유리" 느낌의 핵심. 순수 투명 블러만 쓰면 칙칙해 보임.
// 사용: Image 가 붙은 오버레이 오브젝트에 같이 부착. (PNG 불필요, vertex 색으로 처리)
[RequireComponent(typeof(Graphic))]
public class UIFrostGradient : BaseMeshEffect
{
    [Tooltip("위쪽 색 (밝은 흰빛). rgba(190,214,240,0.10) 기본")]
    public Color topColor = new Color(0.745f, 0.839f, 0.941f, 0.10f);
    [Tooltip("아래쪽 색 (투명)")]
    public Color bottomColor = new Color(0.745f, 0.839f, 0.941f, 0f);
    [Tooltip("위쪽 색이 상단에 몰리는 정도. 1=선형(기본, 인벤/도감 그대로). 클수록 상단 일부만 밝고 빠르게 아래색으로(엔필식 상단 집중).")]
    public float topBias = 1f;

    private static readonly List<UIVertex> _verts = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        vh.GetUIVertexStream(_verts);
        if (_verts.Count == 0) return;

        // 쿼드 경계
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < _verts.Count; i++)
        {
            var p = _verts[i].position;
            if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
        }
        float h = Mathf.Max(0.0001f, maxY - minY);

        bool curved = topBias > 0f && Mathf.Abs(topBias - 1f) > 0.001f;

        // 선형(기본): 기존 정점 색만 수정(인벤/도감 등 그대로). 4정점이면 GPU가 선형 보간.
        if (!curved)
        {
            for (int i = 0; i < _verts.Count; i++)
            {
                var v = _verts[i];
                float t = (v.position.y - minY) / h;   // 0 아래 ~ 1 위
                v.color = (Color)v.color * Color.Lerp(bottomColor, topColor, t);
                _verts[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(_verts);
            return;
        }

        // 곡선(상단 집중): 세로 N분할 재생성. (정점 4개뿐이면 pow 가 0/1 고정점이라 안 먹어 분할 필수.)
        var baseV = _verts[0];                 // UV/노멀/탄젠트 템플릿
        Color baseCol = (Color)baseV.color;    // Image.color (보통 흰색)
        vh.Clear();
        const int N = 48;
        for (int r = 0; r <= N; r++)
        {
            float ty = r / (float)N;                       // 0 아래 ~ 1 위
            float y  = Mathf.Lerp(minY, maxY, ty);
            float tb = Mathf.Pow(ty, topBias);             // 상단 집중
            Color col = baseCol * Color.Lerp(bottomColor, topColor, tb);
            var vl = baseV; vl.position = new Vector3(minX, y, baseV.position.z); vl.color = col;
            var vr = baseV; vr.position = new Vector3(maxX, y, baseV.position.z); vr.color = col;
            vh.AddVert(vl);
            vh.AddVert(vr);
        }
        for (int r = 0; r < N; r++)
        {
            int b = r * 2;                 // row r: b(좌), b+1(우) / row r+1: b+2(좌), b+3(우)
            vh.AddTriangle(b, b + 2, b + 3);
            vh.AddTriangle(b, b + 3, b + 1);
        }
    }

#if UNITY_EDITOR
    // 인스펙터에서 topBias/색 바꾸면 즉시 메쉬 갱신(라이브 튜닝).
    protected override void OnValidate()
    {
        base.OnValidate();
        if (graphic != null) graphic.SetVerticesDirty();
    }
#endif
}
