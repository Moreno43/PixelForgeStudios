using UnityEngine;
using UnityEngine.UI;

namespace PixelForge
{
    /// <summary>
    /// Un cuadrilátero inclinado con degradado opcional, usado para paneles, botones y barras.
    /// Fill lo recorta desde un lado, lo que convierte la misma forma en una barra de vida.
    /// Es un gráfico de UI propio: genera su malla (4 vértices, 2 triángulos) en OnPopulateMesh.
    /// </summary>
    [ExecuteAlways]
    public sealed class ForgeShape : MaskableGraphic
    {
        float slant, fill=1;
        bool fromRight, vertical;
        Color end=Color.clear;
        // Cada propiedad llama a SetVerticesDirty() para que Unity vuelva a generar la malla con el nuevo valor.
        /// <summary>Desplazamiento horizontal en píxeles entre el borde inferior y el superior; positivo inclina a la derecha.</summary>
        public float Slant { get => slant; set { slant=value; SetVerticesDirty(); } }
        /// <summary>Porción visible de 0 a 1 (solo regenera la malla si el valor cambia).</summary>
        public float Fill { get => fill; set { value=Mathf.Clamp01(value); if(!Mathf.Approximately(fill,value)){ fill=value; SetVerticesDirty(); } } }
        // true = el recorte deja visible la parte derecha.
        public bool FromRight { get => fromRight; set { fromRight=value; SetVerticesDirty(); } }
        /// <summary>Segundo color del degradado (derecha o arriba); transparente significa color plano.</summary>
        public Color End { get => end; set { end=value; SetVerticesDirty(); } }
        // true = degradado de abajo hacia arriba en vez de izquierda a derecha.
        public bool Vertical { get => vertical; set { vertical=value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r=GetPixelAdjustedRect();
            float width=r.width*fill;if(width<=.01f)return;
            // x0/x1: bordes izquierdo y derecho de la parte visible. s: media inclinación.
            float x0=fromRight?r.xMax-width:r.xMin, x1=x0+width, s=slant*.5f;
            var startColor=color;var endColor=end.a>0?end:color;
            // Los colores del degradado dependen de la posición en todo el rectángulo, así un relleno parcial conserva sus tonos.
            Color At(float x,float y)=>vertical?Color.Lerp(startColor,endColor,Mathf.InverseLerp(r.yMin,r.yMax,y)):Color.Lerp(startColor,endColor,Mathf.InverseLerp(r.xMin,r.xMax,x));
            // Vértices: abajo-izquierda, arriba-izquierda, arriba-derecha, abajo-derecha.
            AddVertex(vh,x0-s,r.yMin,At(x0,r.yMin));AddVertex(vh,x0+s,r.yMax,At(x0,r.yMax));
            AddVertex(vh,x1+s,r.yMax,At(x1,r.yMax));AddVertex(vh,x1-s,r.yMin,At(x1,r.yMin));
            vh.AddTriangle(0,1,2);vh.AddTriangle(2,3,0);
        }
        static void AddVertex(VertexHelper vh,float x,float y,Color color)
        { var vertex=UIVertex.simpleVert;vertex.position=new Vector3(x,y);vertex.color=color;vh.AddVert(vertex); }
    }
}
