using UnityEngine;
using UnityEngine.UI;

namespace PixelForge
{
    /// <summary>Rayas diagonales que se desplazan lentamente sobre un fondo. Usa tiempo real, así las pausas no las detienen.</summary>
    [ExecuteAlways]
    public sealed class ForgeStripes : MaskableGraphic
    {
        // Spacing: distancia entre rayas. Width: grosor. Lean: inclinación. Speed: píxeles por segundo.
        public float Spacing=140, Width=34, Lean=.55f, Speed=24;
        float offset;

        void Update()
        {
            // El desplazamiento da la vuelta cada "Spacing" píxeles, así el movimiento es infinito y sin saltos.
            offset=(offset+Time.unscaledDeltaTime*Speed)%Spacing;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r=GetPixelAdjustedRect();float rise=r.height*Lean;
            // Empieza lo bastante a la izquierda para que las rayas inclinadas cubran también la esquina inferior izquierda.
            for(float x=r.xMin-rise-Spacing+offset;x<r.xMax;x+=Spacing)
            {
                // Cada raya es un paralelogramo de 4 vértices y 2 triángulos.
                int i=vh.currentVertCount;var vertex=UIVertex.simpleVert;vertex.color=color;
                vertex.position=new Vector3(x,r.yMin);vh.AddVert(vertex);
                vertex.position=new Vector3(x+rise,r.yMax);vh.AddVert(vertex);
                vertex.position=new Vector3(x+rise+Width,r.yMax);vh.AddVert(vertex);
                vertex.position=new Vector3(x+Width,r.yMin);vh.AddVert(vertex);
                vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i+2,i+3,i);
            }
        }
    }
}
