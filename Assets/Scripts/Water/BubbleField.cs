using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// Jacuzzi bubbles: small bright spheres that rise from jets on the floor of the tub, wobble on the way up and
    /// pop at the surface, where they stir the water (splashes on the ripple surface).
    /// </summary>
    public class BubbleField : MonoBehaviour
    {
        public Vector2 min, max;           // tub area on the floor plan (world)
        public float floorY, surfaceY;
        public int count = 90;
        public Material material;
        public PoolRipples surface;
        public int jets = 6;

        Transform[] b;
        Vector3[] pos;
        float[] speed, size, wob, delay;
        Vector2[] jet;

        void Start()
        {
            var rnd = new System.Random(5);
            jet = new Vector2[jets];
            for (int i = 0; i < jets; i++)
                jet[i] = new Vector2(Mathf.Lerp(min.x + 0.3f, max.x - 0.3f, (i + 0.5f) / jets), Mathf.Lerp(min.y + 0.3f, max.y - 0.3f, i % 2 == 0 ? 0.3f : 0.7f));
            b = new Transform[count];
            pos = new Vector3[count];
            speed = new float[count]; size = new float[count]; wob = new float[count]; delay = new float[count];
            for (int i = 0; i < count; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                g.name = "Bubble";
                g.transform.SetParent(transform, false);
                Destroy(g.GetComponent<Collider>());
                if (material) g.GetComponent<Renderer>().sharedMaterial = material;
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                b[i] = g.transform;
                Respawn(i, rnd, true);
            }
        }

        void Respawn(int i, System.Random rnd, bool scatter)
        {
            var j = jet[rnd.Next(jets)];
            pos[i] = new Vector3(j.x + ((float)rnd.NextDouble() - 0.5f) * 0.25f, floorY + 0.05f, j.y + ((float)rnd.NextDouble() - 0.5f) * 0.25f);
            if (scatter) pos[i].y = Mathf.Lerp(floorY, surfaceY, (float)rnd.NextDouble());
            speed[i] = 0.35f + (float)rnd.NextDouble() * 0.35f;
            size[i] = 0.025f + (float)rnd.NextDouble() * 0.045f;
            wob[i] = (float)rnd.NextDouble() * 10f;
            delay[i] = scatter ? 0f : (float)rnd.NextDouble() * 0.4f;
            b[i].localScale = Vector3.one * size[i];
        }

        readonly System.Random live = new System.Random(11);

        void Update()
        {
            for (int i = 0; i < count; i++)
            {
                if (delay[i] > 0f) { delay[i] -= Time.deltaTime; b[i].gameObject.SetActive(false); continue; }
                b[i].gameObject.SetActive(true);
                pos[i].y += speed[i] * Time.deltaTime * (1f + (pos[i].y - floorY) * 0.5f);
                float w = wob[i] + Time.time * 4f;
                var p = pos[i] + new Vector3(Mathf.Sin(w) * 0.02f, 0f, Mathf.Cos(w * 1.3f) * 0.02f);
                b[i].position = p;
                if (pos[i].y >= surfaceY - 0.02f)
                {
                    if (surface) surface.Splash(new Vector3(pos[i].x, surfaceY, pos[i].z), 0.25f, 0.1f);
                    Respawn(i, live, false);
                }
            }
        }
    }
}
