using IronKnuckle.Data;
using UnityEngine;

namespace IronKnuckle.Robot
{
    public class VisorFaceController : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int AtlasId = Shader.PropertyToID("_AtlasOffsetScale");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionStrength");

        private Renderer visorRenderer;
        private MaterialPropertyBlock mpb;
        private Color baseVisorColor = new Color(0.15f, 0.92f, 1.0f, 1f);
        private VisorExpression currentExpression = VisorExpression.Neutral;

        public void Initialize(Renderer targetRenderer, Color visorColor)
        {
            visorRenderer = targetRenderer;
            baseVisorColor = visorColor;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            ApplyExpression(VisorExpression.Neutral);
        }

        public void SetColor(Color visorColor)
        {
            baseVisorColor = visorColor;
            ApplyExpression(currentExpression);
        }

        public void ApplyExpression(VisorExpression expr)
        {
            currentExpression = expr;
            if (visorRenderer == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();

            // 2 columns x 3 rows atlas. Unity UV V=0 is bottom of image, PIL row 0 is top.
            // Row 0 (PIL top) => UV offsetY = 2/3, Row 1 => 1/3, Row 2 => 0.
            int col = 0;
            int pilRow = 0;
            Color tint = baseVisorColor;
            float emission = 1.9f;

            switch (expr)
            {
                case VisorExpression.Neutral:
                    col = 0; pilRow = 0;
                    break;
                case VisorExpression.Angry:
                    col = 1; pilRow = 0;
                    tint = Color.Lerp(baseVisorColor, new Color(1f, 0.35f, 0.15f, 1f), 0.55f);
                    emission = 2.4f;
                    break;
                case VisorExpression.Hit:
                    col = 0; pilRow = 1;
                    tint = new Color(1f, 0.75f, 0.2f, 1f);
                    emission = 2.2f;
                    break;
                case VisorExpression.Stunned:
                    col = 1; pilRow = 1;
                    tint = new Color(1f, 0.9f, 0.25f, 1f);
                    break;
                case VisorExpression.KO:
                    col = 0; pilRow = 2;
                    tint = new Color(1f, 0.2f, 0.2f, 1f);
                    emission = 1.3f;
                    break;
                case VisorExpression.Victory:
                    col = 1; pilRow = 2;
                    tint = new Color(0.3f, 1f, 0.55f, 1f);
                    emission = 2.4f;
                    break;
            }

            float scaleX = 0.5f;
            float scaleY = 1f / 3f;
            float offsetX = col * 0.5f;
            float offsetY = (2 - pilRow) / 3f;

            visorRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, tint);
            mpb.SetVector(AtlasId, new Vector4(scaleX, scaleY, offsetX, offsetY));
            mpb.SetFloat(EmissionId, emission);
            visorRenderer.SetPropertyBlock(mpb);
        }
    }
}
