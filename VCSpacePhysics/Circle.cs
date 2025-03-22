using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
public class Circle : Graphic
{
    [SerializeField]
    Texture m_Texture = s_WhiteTexture;
    public bool filled = true;
    public float lineWeight = 5;
    public int segments = 360;

    protected UIVertex[] SetVbo(Vector2[] vertices, Vector2[] uvs)
    {
        UIVertex[] vbo = new UIVertex[4];
        for (int i = 0; i < vertices.Length; i++)
        {
            var vert = UIVertex.simpleVert;
            vert.color = color;
            vert.position = vertices[i];
            vert.uv0 = uvs[i];
            vbo[i] = vert;
        }
        return vbo;
    }

    protected override void OnPopulateMesh(Mesh toFill)
    {
        float outerRadius = -rectTransform.pivot.x * rectTransform.rect.width;
        float innerRadius = -rectTransform.pivot.x * rectTransform.rect.width + this.lineWeight;

        toFill.Clear();
        var vbo = new VertexHelper(toFill);

        UIVertex vert = UIVertex.simpleVert;
        Vector2 prevOuter = Vector2.zero;
        Vector2 prevInner = Vector2.zero;
        Vector2 uv0 = new Vector2(0, 0);
        Vector2 uv1 = new Vector2(0, 1);
        Vector2 uv2 = new Vector2(1, 1);
        Vector2 uv3 = new Vector2(1, 0);
        Vector2 pos0;
        Vector2 pos1;
        Vector2 pos2;
        Vector2 pos3;

        for (int i = 0; i <= segments; i++)
        {
            float rad = Mathf.PI * 2 * i / segments;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);

            uv0 = new Vector2(0, 1);
            uv1 = new Vector2(1, 1);
            uv2 = new Vector2(1, 0);
            uv3 = new Vector2(0, 0);

            pos0 = prevOuter;
            pos1 = new Vector2(outerRadius * c, outerRadius * s);

            if (this.filled)
            {
                pos2 = Vector2.zero;
                pos3 = Vector2.zero;
            }
            else
            {
                pos2 = new Vector2(innerRadius * c, innerRadius * s);
                pos3 = prevInner;
            }

            prevOuter = pos1;
            prevInner = pos2;

            vbo.AddUIVertexQuad(SetVbo(new[] { pos0, pos1, pos2, pos3 }, new[] { uv0, uv1, uv2, uv3 }));

        }

        if (vbo.currentVertCount > 3)
        {
            vbo.FillMesh(toFill);
        }

    }
}