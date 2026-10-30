using System;
using System.Collections.Generic;
using UnityEngine;

namespace AMT.AR.UI
{
    // Line icons rasterised at startup from simple signed-distance shapes (segments, arcs, circles,
    // polygons) on a 0..1 grid with Y up. Sprites are white; tint them with Image.color.
    public static class Icons
    {
        const int Size = 96;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        delegate float Shape(Vector2 p);

        public static Sprite Home => Get("home", () => Union(
            Poly(.18f, .5f, .5f, .8f, .82f, .5f, .5f, .8f).Stroke(.06f),
            Seg(.27f, .55f, .27f, .2f, .06f), Seg(.73f, .55f, .73f, .2f, .06f), Seg(.27f, .2f, .73f, .2f, .06f),
            Seg(.43f, .2f, .43f, .4f, .06f), Seg(.57f, .2f, .57f, .4f, .06f), Seg(.43f, .4f, .57f, .4f, .06f)));
        public static Sprite Back => Get("back", () => Union(Seg(.6f, .2f, .3f, .5f, .08f), Seg(.3f, .5f, .6f, .8f, .08f)));
        public static Sprite Forward => Get("forward", () => Union(Seg(.4f, .2f, .7f, .5f, .08f), Seg(.7f, .5f, .4f, .8f, .08f)));
        public static Sprite Close => Get("close", () => Union(Seg(.25f, .25f, .75f, .75f, .08f), Seg(.25f, .75f, .75f, .25f, .08f)));
        public static Sprite Play => Get("play", () => Poly(.3f, .18f, .82f, .5f, .3f, .82f).Fill());
        public static Sprite Pause => Get("pause", () => Union(Box(.27f, .2f, .42f, .8f), Box(.58f, .2f, .73f, .8f)));
        public static Sprite StepForward => Get("stepf", () => Union(Poly(.2f, .2f, .64f, .5f, .2f, .8f).Fill(), Box(.66f, .2f, .8f, .8f)));
        public static Sprite StepBack => Get("stepb", () => Union(Poly(.8f, .2f, .36f, .5f, .8f, .8f).Fill(), Box(.2f, .2f, .34f, .8f)));
        public static Sprite Restart => Get("restart", () => Union(
            Arc(.5f, .5f, .3f, .07f, 60f, 360f), Poly(.62f, .9f, .86f, .7f, .58f, .6f).Fill()));
        public static Sprite Cube => Get("cube", () => Union(
            Poly(.5f, .86f, .84f, .68f, .84f, .3f, .5f, .12f, .16f, .3f, .16f, .68f).Stroke(.055f),
            Seg(.16f, .68f, .5f, .5f, .055f), Seg(.84f, .68f, .5f, .5f, .055f), Seg(.5f, .5f, .5f, .12f, .055f)));
        public static Sprite AR => Get("ar", () => Union(
            Seg(.12f, .3f, .12f, .12f, .06f), Seg(.12f, .12f, .3f, .12f, .06f),
            Seg(.7f, .12f, .88f, .12f, .06f), Seg(.88f, .12f, .88f, .3f, .06f),
            Seg(.88f, .7f, .88f, .88f, .06f), Seg(.88f, .88f, .7f, .88f, .06f),
            Seg(.3f, .88f, .12f, .88f, .06f), Seg(.12f, .88f, .12f, .7f, .06f),
            Poly(.5f, .72f, .7f, .61f, .7f, .39f, .5f, .28f, .3f, .39f, .3f, .61f).Stroke(.05f),
            Seg(.3f, .61f, .5f, .5f, .05f), Seg(.7f, .61f, .5f, .5f, .05f), Seg(.5f, .5f, .5f, .28f, .05f)));
        public static Sprite Components => Get("components", () => Union(
            Box(.14f, .58f, .44f, .86f).Stroke(.055f), Box(.56f, .58f, .86f, .86f).Stroke(.055f),
            Box(.14f, .14f, .44f, .42f).Stroke(.055f), Circ(.71f, .28f, .15f).Stroke(.055f)));
        public static Sprite Tools => Get("tools", () => Union(
            Seg(.2f, .2f, .58f, .58f, .1f), Arc(.68f, .68f, .17f, .075f, 80f, 370f)));
        public static Sprite Motion => Get("motion", () => Union(
            Arc(.5f, .5f, .3f, .07f, 20f, 160f), Arc(.5f, .5f, .3f, .07f, 200f, 340f),
            Poly(.8f, .64f, .86f, .42f, .66f, .5f).Fill(), Poly(.2f, .36f, .14f, .58f, .34f, .5f).Fill(), Circ(.5f, .5f, .08f)));
        public static Sprite Explode => Get("explode", () => Union(
            Box(.38f, .38f, .62f, .62f).Stroke(.055f),
            Seg(.3f, .3f, .12f, .12f, .055f), Seg(.12f, .12f, .26f, .12f, .055f), Seg(.12f, .12f, .12f, .26f, .055f),
            Seg(.7f, .7f, .88f, .88f, .055f), Seg(.88f, .88f, .74f, .88f, .055f), Seg(.88f, .88f, .88f, .74f, .055f),
            Seg(.7f, .3f, .88f, .12f, .055f), Seg(.88f, .12f, .74f, .12f, .055f), Seg(.88f, .12f, .88f, .26f, .055f),
            Seg(.3f, .7f, .12f, .88f, .055f), Seg(.12f, .88f, .26f, .88f, .055f), Seg(.12f, .88f, .12f, .74f, .055f)));
        public static Sprite Assemble => Get("assemble", () => Union(
            Seg(.5f, .88f, .5f, .42f, .07f), Seg(.32f, .58f, .5f, .4f, .07f), Seg(.68f, .58f, .5f, .4f, .07f),
            Seg(.16f, .34f, .16f, .14f, .06f), Seg(.16f, .14f, .84f, .14f, .06f), Seg(.84f, .14f, .84f, .34f, .06f)));
        public static Sprite Disassemble => Get("disassemble", () => Union(
            Seg(.5f, .4f, .5f, .86f, .07f), Seg(.32f, .68f, .5f, .86f, .07f), Seg(.68f, .68f, .5f, .86f, .07f),
            Seg(.16f, .34f, .16f, .14f, .06f), Seg(.16f, .14f, .84f, .14f, .06f), Seg(.84f, .14f, .84f, .34f, .06f)));
        public static Sprite Eye => Get("eye", () => Union(
            Arc(.5f, .22f, .42f, .06f, 45f, 135f), Arc(.5f, .78f, .42f, .06f, 225f, 315f), Circ(.5f, .5f, .11f)));
        public static Sprite EyeOff => Get("eyeoff", () => Union(
            Arc(.5f, .22f, .42f, .06f, 45f, 135f), Arc(.5f, .78f, .42f, .06f, 225f, 315f), Circ(.5f, .5f, .11f),
            Seg(.18f, .18f, .82f, .82f, .06f)));
        public static Sprite Focus => Get("focus", () => Union(
            Circ(.5f, .5f, .26f).Stroke(.06f), Circ(.5f, .5f, .07f),
            Seg(.5f, .06f, .5f, .2f, .06f), Seg(.5f, .8f, .5f, .94f, .06f), Seg(.06f, .5f, .2f, .5f, .06f), Seg(.8f, .5f, .94f, .5f, .06f)));
        public static Sprite Isolate => Get("isolate", () => Union(
            Box(.12f, .12f, .88f, .88f).Stroke(.045f), Circ(.5f, .5f, .17f)));
        public static Sprite Info => Get("info", () => Union(
            Circ(.5f, .5f, .4f).Stroke(.06f), Seg(.5f, .27f, .5f, .52f, .075f), Circ(.5f, .67f, .055f)));
        public static Sprite Check => Get("check", () => Union(Seg(.2f, .5f, .42f, .28f, .085f), Seg(.42f, .28f, .82f, .7f, .085f)));
        public static Sprite Alert => Get("alert", () => Union(
            Poly(.5f, .88f, .9f, .16f, .1f, .16f).Stroke(.06f), Seg(.5f, .37f, .5f, .62f, .07f), Circ(.5f, .27f, .045f)));
        public static Sprite ResetView => Get("resetview", () => Union(
            Arc(.5f, .5f, .32f, .065f, 110f, 430f - 40f), Poly(.2f, .78f, .42f, .84f, .3f, .62f).Fill(), Circ(.5f, .5f, .08f)));
        public static Sprite Palette => Get("palette", () => Union(
            Circ(.5f, .5f, .38f).Stroke(.06f), Circ(.38f, .64f, .07f), Circ(.6f, .68f, .07f), Circ(.68f, .48f, .07f), Circ(.4f, .4f, .07f)));
        public static Sprite Layers => Get("layers", () => Union(
            Poly(.5f, .84f, .88f, .64f, .5f, .44f, .12f, .64f).Stroke(.055f),
            Seg(.12f, .46f, .5f, .26f, .055f), Seg(.5f, .26f, .88f, .46f, .055f)));
        public static Sprite List => Get("list", () => Union(
            Circ(.2f, .74f, .055f), Circ(.2f, .5f, .055f), Circ(.2f, .26f, .055f),
            Seg(.36f, .74f, .84f, .74f, .065f), Seg(.36f, .5f, .84f, .5f, .065f), Seg(.36f, .26f, .84f, .26f, .065f)));
        public static Sprite Speed => Get("speed", () => Union(
            Arc(.5f, .38f, .36f, .065f, 0f, 180f), Seg(.5f, .38f, .7f, .62f, .07f), Circ(.5f, .38f, .07f)));
        public static Sprite Shield => Get("shield", () => Union(
            Poly(.5f, .9f, .84f, .76f, .78f, .36f, .5f, .1f, .22f, .36f, .16f, .76f).Stroke(.06f),
            Seg(.36f, .5f, .47f, .38f, .065f), Seg(.47f, .38f, .66f, .62f, .065f)));
        public static Sprite Dot => Get("dot", () => Circ(.5f, .5f, .32f));

        // ---------- rasteriser ----------

        static Sprite Get(string key, Func<Shape> build)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            var shape = build();
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = "Icon " + key };
            var px = new Color32[Size * Size];
            float aa = 1.2f / Size;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var p = new Vector2((x + .5f) / Size, (y + .5f) / Size);
                float a = Mathf.Clamp01(.5f - shape(p) / aa);
                px[y * Size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply(true, true);
            s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 100);
            cache[key] = s;
            return s;
        }

        static Shape Union(params Shape[] shapes) => p =>
        {
            float d = float.MaxValue;
            foreach (var s in shapes) d = Mathf.Min(d, s(p));
            return d;
        };

        static Shape Seg(float ax, float ay, float bx, float by, float width)
        {
            var a = new Vector2(ax, ay); var b = new Vector2(bx, by);
            return p =>
            {
                var pa = p - a; var ba = b - a;
                float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
                return (pa - ba * h).magnitude - width * .5f;
            };
        }

        static Shape Circ(float cx, float cy, float r) => p => (p - new Vector2(cx, cy)).magnitude - r;

        static Shape Box(float x0, float y0, float x1, float y1) => p =>
        {
            var c = new Vector2((x0 + x1) * .5f, (y0 + y1) * .5f);
            var e = new Vector2((x1 - x0) * .5f, (y1 - y0) * .5f);
            var d = new Vector2(Mathf.Abs(p.x - c.x) - e.x, Mathf.Abs(p.y - c.y) - e.y);
            return new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0);
        };

        static Shape Arc(float cx, float cy, float r, float width, float fromDeg, float toDeg)
        {
            var c = new Vector2(cx, cy);
            return p =>
            {
                var v = p - c;
                float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                // Normalise into [fromDeg, fromDeg + 360).
                while (ang < fromDeg) ang += 360f;
                while (ang >= fromDeg + 360f) ang -= 360f;
                if (ang <= toDeg) return Mathf.Abs(v.magnitude - r) - width * .5f;
                var a = c + new Vector2(Mathf.Cos(fromDeg * Mathf.Deg2Rad), Mathf.Sin(fromDeg * Mathf.Deg2Rad)) * r;
                var b = c + new Vector2(Mathf.Cos(toDeg * Mathf.Deg2Rad), Mathf.Sin(toDeg * Mathf.Deg2Rad)) * r;
                return Mathf.Min((p - a).magnitude, (p - b).magnitude) - width * .5f;
            };
        }

        sealed class Polygon
        {
            readonly Vector2[] v;
            public Polygon(Vector2[] points) { v = points; }

            float EdgeDistance(Vector2 p)
            {
                float d = float.MaxValue;
                for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
                {
                    var e = v[i] - v[j]; var w = p - v[j];
                    var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                    d = Mathf.Min(d, b.magnitude);
                }
                return d;
            }

            bool Inside(Vector2 p)
            {
                bool inside = false;
                for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
                    if ((v[i].y > p.y) != (v[j].y > p.y) &&
                        p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x)
                        inside = !inside;
                return inside;
            }

            public Shape Fill() => p => Inside(p) ? -EdgeDistance(p) : EdgeDistance(p);
            public Shape Stroke(float width) => p => EdgeDistance(p) - width * .5f;
        }

        static Polygon Poly(params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return new Polygon(pts);
        }

        static Shape Stroke(this Shape s, float width) => p => Mathf.Abs(s(p)) - width * .5f;
    }
}
