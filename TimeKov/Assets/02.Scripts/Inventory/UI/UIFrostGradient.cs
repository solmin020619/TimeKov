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

    private static readonly List<UIVertex> _verts = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        vh.GetUIVertexStream(_verts);

        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < _verts.Count; i++)
        {
            float y = _verts[i].position.y;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
        float h = Mathf.Max(0.0001f, maxY - minY);

        for (int i = 0; i < _verts.Count; i++)
        {
            var v = _verts[i];
            float t = (v.position.y - minY) / h;   // 0 아래 ~ 1 위
            // 기존 vertex 색(Image.color)에 그라데이션을 곱함. Image.color=흰색이면 예전과 동일,
            // 다른 색이면 런타임 틴트(등급 오로라 등)에 쓸 수 있음.
            Color grad = Color.Lerp(bottomColor, topColor, t);
            v.color = (Color)v.color * grad;
            _verts[i] = v;
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(_verts);
    }
}
