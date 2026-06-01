using System.Collections.Generic;
using UnityEngine;

namespace BipedGen2
{
    public class LegRig2
    {
        public Rigidbody2D thigh;
        public Rigidbody2D shin;
        public Rigidbody2D foot;
        public HingeJoint2D hip;
        public HingeJoint2D knee;
        public HingeJoint2D ankle;
        public int layoutSign;   // -1 = left, +1 = right
        public int outwardSign;  // = -layoutSign, used in joint-angle formulas
    }

    public class bipedconstructor2 : MonoBehaviour
    {
        [Header("Body part sizes (world units)")]
        public float bodyRadius = 0.75f;
        public Vector2 earSize = new Vector2(0.22f, 0.32f);     // base, height
        public Vector2 upperArmSize = new Vector2(0.18f, 0.65f);
        public Vector2 forearmSize = new Vector2(0.18f, 0.55f);
        public Vector2 thighSize = new Vector2(0.22f, 0.75f);
        public Vector2 shinSize = new Vector2(0.22f, 0.75f);
        public Vector2 footSize = new Vector2(0.55f, 0.15f);

        [Header("Layout")]
        public float hipHalfWidth = 0.40f;
        public float shoulderHalfWidth = 0.70f;
        public float shoulderY = 0.20f;
        public float earHalfWidth = 0.40f;
        public float earY = 0.85f;
        public float earOutwardTilt = 15f;       // deg, each ear tilted outward
        public float armOutwardAngle = 45f;      // upper arm + forearm angled outward
        public float restThighAngle = 45f;
        public float restShinAngle = 0f;

        [Header("Density (mass = area * density)")]
        public float density = 2f;

        [Header("Colors")]
        public Color bodyColor = new Color(0.88f, 0.12f, 0.12f);
        public Color earColor = new Color(0.88f, 0.12f, 0.12f);
        public Color limbColor = new Color(0.97f, 0.55f, 0.12f);
        public Color groundColor = new Color(0.35f, 0.30f, 0.25f);

        [Header("Ground")]
        public bool createGround = true;
        public Vector2 groundSize = new Vector2(20f, 1f);
        public float groundTopY = 0f;
        public float feetFriction = 1f;
        public bool autoPositionAtGround = true;

        PhysicsMaterial2D footMaterial;

        void Awake()
        {
            footMaterial = new PhysicsMaterial2D("BipedFoot2") { friction = feetFriction, bounciness = 0f };

            if (createGround) BuildGround();

            float spawnY = transform.position.y;
            if (autoPositionAtGround)
            {
                float hipYRel = -bodyRadius * 0.8f;
                float thighDy = thighSize.y * Mathf.Cos(restThighAngle * Mathf.Deg2Rad);
                float shinDy = shinSize.y * Mathf.Cos(restShinAngle * Mathf.Deg2Rad);
                float ankleYRel = hipYRel - thighDy - shinDy;
                float lowestYRel = ankleYRel - footSize.y;
                spawnY = groundTopY - lowestYRel + 0.05f;
            }

            Vector3 origin = new Vector3(transform.position.x, spawnY, 0f);
            BuildBiped(origin);
        }

        void BuildGround()
        {
            var g = new GameObject("Ground");
            g.transform.position = new Vector3(transform.position.x, groundTopY - groundSize.y * 0.5f, 0f);
            AddRectSprite(g, groundSize, groundColor);
            var col = g.AddComponent<BoxCollider2D>();
            col.size = groundSize;
            col.sharedMaterial = footMaterial;
        }

        void BuildBiped(Vector3 origin)
        {
            // Body (circle - the big red head/body)
            float bodyMass = Mathf.PI * bodyRadius * bodyRadius * density;
            var bodyGO = CreateCirclePart("Body", origin, bodyRadius, bodyMass, bodyColor);
            var bodyRb = bodyGO.GetComponent<Rigidbody2D>();
            bodyRb.angularDrag = 5f;

            // Ears (red triangles on top, rigidly welded to body)
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 earPos = (Vector2)origin + new Vector2(s * earHalfWidth, earY);
                float earArea = 0.5f * earSize.x * earSize.y;
                float earMass = earArea * density;
                float earRot = s * earOutwardTilt;
                var earGO = CreateTrianglePart((s < 0 ? "LeftEar" : "RightEar"), earPos, earRot, earSize, earMass, earColor);
                WeldTo(earGO, bodyRb, earPos);
            }

            // Arms (orange, two segments each, rigidly welded together and to body)
            for (int s = -1; s <= 1; s += 2)
            {
                BuildRigidArm(bodyRb, origin, s);
            }

            // Legs (hinged at hip, knee, ankle)
            var left = BuildLeg(bodyRb, origin, -1);
            var right = BuildLeg(bodyRb, origin, +1);

            IgnoreInternalCollisions();

            // Brain
            var brain = bodyGO.AddComponent<bipedbrain2>();
            brain.SetRig(bodyRb, left, right);
        }

        void BuildRigidArm(Rigidbody2D bodyRb, Vector3 origin, int sign)
        {
            Vector2 shoulderPos = (Vector2)origin + new Vector2(sign * shoulderHalfWidth, shoulderY);

            float armZ = sign * armOutwardAngle;
            Vector2 armDir = new Vector2(Mathf.Sin(armZ * Mathf.Deg2Rad), -Mathf.Cos(armZ * Mathf.Deg2Rad));

            Vector2 upperArmCenter = shoulderPos + armDir * (upperArmSize.y * 0.5f);
            float upperArmMass = upperArmSize.x * upperArmSize.y * density;
            var upperArmGO = CreateRectPart((sign < 0 ? "LeftUpperArm" : "RightUpperArm"),
                upperArmCenter, armZ, upperArmSize, upperArmMass, limbColor);
            WeldTo(upperArmGO, bodyRb, shoulderPos);

            Vector2 elbowPos = shoulderPos + armDir * upperArmSize.y;
            Vector2 forearmCenter = elbowPos + armDir * (forearmSize.y * 0.5f);
            float forearmMass = forearmSize.x * forearmSize.y * density;
            var forearmGO = CreateRectPart((sign < 0 ? "LeftForearm" : "RightForearm"),
                forearmCenter, armZ, forearmSize, forearmMass, limbColor);
            WeldTo(forearmGO, upperArmGO.GetComponent<Rigidbody2D>(), elbowPos);
        }

        LegRig2 BuildLeg(Rigidbody2D bodyRb, Vector3 origin, int sign)
        {
            var leg = new LegRig2 { layoutSign = sign, outwardSign = -sign };

            Vector2 hipPos = (Vector2)origin + new Vector2(sign * hipHalfWidth, -bodyRadius * 0.8f);

            float thighZ = sign * restThighAngle;
            Vector2 thighDir = new Vector2(Mathf.Sin(thighZ * Mathf.Deg2Rad), -Mathf.Cos(thighZ * Mathf.Deg2Rad));
            Vector2 thighCenter = hipPos + thighDir * (thighSize.y * 0.5f);
            float thighMass = thighSize.x * thighSize.y * density;
            var thighGO = CreateRectPart((sign < 0 ? "LeftThigh" : "RightThigh"),
                thighCenter, thighZ, thighSize, thighMass, limbColor);

            Vector2 kneePos = hipPos + thighDir * thighSize.y;

            float shinZ = sign * restShinAngle;
            Vector2 shinDir = new Vector2(Mathf.Sin(shinZ * Mathf.Deg2Rad), -Mathf.Cos(shinZ * Mathf.Deg2Rad));
            Vector2 shinCenter = kneePos + shinDir * (shinSize.y * 0.5f);
            float shinMass = shinSize.x * shinSize.y * density;
            var shinGO = CreateRectPart((sign < 0 ? "LeftShin" : "RightShin"),
                shinCenter, shinZ, shinSize, shinMass, limbColor * 0.9f);

            Vector2 anklePos = kneePos + shinDir * shinSize.y;
            Vector2 footCenter = anklePos + new Vector2(sign * footSize.x * 0.25f, -footSize.y * 0.5f);
            float footMass = footSize.x * footSize.y * density;
            var footGO = CreateRectPart((sign < 0 ? "LeftFoot" : "RightFoot"),
                footCenter, 0f, footSize, footMass, limbColor * 0.8f);
            footGO.GetComponent<BoxCollider2D>().sharedMaterial = footMaterial;

            leg.thigh = thighGO.GetComponent<Rigidbody2D>();
            leg.shin = shinGO.GetComponent<Rigidbody2D>();
            leg.foot = footGO.GetComponent<Rigidbody2D>();

            leg.hip = AddHinge(thighGO, bodyRb, hipPos);
            leg.knee = AddHinge(shinGO, leg.thigh, kneePos);
            leg.ankle = AddHinge(footGO, leg.shin, anklePos);

            ApplyHipLimits(leg);
            ApplyKneeLimits(leg);
            ApplyAnkleLimits(leg);

            return leg;
        }

        void ApplyHipLimits(LegRig2 leg)
        {
            float hyperextJa = -leg.outwardSign * restThighAngle;
            float flexionJa = -leg.outwardSign * (restThighAngle - 90f);
            leg.hip.useLimits = true;
            leg.hip.limits = new JointAngleLimits2D
            {
                min = Mathf.Min(hyperextJa, flexionJa),
                max = Mathf.Max(hyperextJa, flexionJa)
            };
        }

        void ApplyKneeLimits(LegRig2 leg)
        {
            float straightJa = leg.outwardSign * (restThighAngle - restShinAngle);
            float bendJa = leg.outwardSign * (restThighAngle - restShinAngle - 135f);
            leg.knee.useLimits = true;
            leg.knee.limits = new JointAngleLimits2D
            {
                min = Mathf.Min(straightJa, bendJa),
                max = Mathf.Max(straightJa, bendJa)
            };
        }

        void ApplyAnkleLimits(LegRig2 leg)
        {
            float toeDownJa = leg.outwardSign * 90f;
            float toesUpJa = -leg.outwardSign * 45f;
            leg.ankle.useLimits = true;
            leg.ankle.limits = new JointAngleLimits2D
            {
                min = Mathf.Min(toeDownJa, toesUpJa),
                max = Mathf.Max(toeDownJa, toesUpJa)
            };
        }

        HingeJoint2D AddHinge(GameObject child, Rigidbody2D connected, Vector2 worldAnchor)
        {
            var j = child.AddComponent<HingeJoint2D>();
            j.connectedBody = connected;
            j.autoConfigureConnectedAnchor = false;
            j.anchor = child.transform.InverseTransformPoint(worldAnchor);
            j.connectedAnchor = connected.transform.InverseTransformPoint(worldAnchor);
            var motor = new JointMotor2D { motorSpeed = 0f, maxMotorTorque = 30f };
            j.motor = motor;
            j.useMotor = true;
            return j;
        }

        void WeldTo(GameObject child, Rigidbody2D connected, Vector2 worldAnchor)
        {
            var j = child.AddComponent<FixedJoint2D>();
            j.connectedBody = connected;
            j.autoConfigureConnectedAnchor = false;
            j.anchor = child.transform.InverseTransformPoint(worldAnchor);
            j.connectedAnchor = connected.transform.InverseTransformPoint(worldAnchor);
            j.dampingRatio = 1f;
            j.frequency = 0f; // perfectly rigid
        }

        GameObject CreateRectPart(string name, Vector2 worldPos, float zRotation, Vector2 size, float mass, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, true);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, zRotation);
            AddRectSprite(go, size, color);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.mass = mass;
            rb.drag = 0.05f;
            rb.angularDrag = 1.5f;
            return go;
        }

        GameObject CreateCirclePart(string name, Vector2 worldPos, float radius, float mass, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, true);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.identity;
            AddCircleSprite(go, radius, color);
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = radius;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.mass = mass;
            rb.drag = 0.05f;
            rb.angularDrag = 1.5f;
            return go;
        }

        GameObject CreateTrianglePart(string name, Vector2 worldPos, float zRotation, Vector2 baseHeight, float mass, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, true);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, zRotation);
            float b = baseHeight.x;
            float h = baseHeight.y;
            AddTriangleSprite(go, b, h, color);
            var col = go.AddComponent<PolygonCollider2D>();
            col.points = new Vector2[]
            {
                new Vector2(-b * 0.5f, -h * 0.5f),
                new Vector2( b * 0.5f, -h * 0.5f),
                new Vector2(       0f,  h * 0.5f),
            };
            var rb = go.AddComponent<Rigidbody2D>();
            rb.mass = mass;
            rb.drag = 0.05f;
            rb.angularDrag = 1.5f;
            return go;
        }

        static void AddRectSprite(GameObject go, Vector2 size, Color color)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeRectSprite(size);
            sr.color = color;
        }

        static void AddCircleSprite(GameObject go, float radius, Color color)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeCircleSprite(radius);
            sr.color = color;
        }

        static void AddTriangleSprite(GameObject go, float b, float h, Color color)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeTriangleSprite(b, h);
            sr.color = color;
        }

        static Sprite MakeRectSprite(Vector2 size)
        {
            int w = Mathf.Max(2, Mathf.RoundToInt(size.x * 100f));
            int h = Mathf.Max(2, Mathf.RoundToInt(size.y * 100f));
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            var white = new Color32(255, 255, 255, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = white;
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite MakeCircleSprite(float radius)
        {
            int d = Mathf.Max(4, Mathf.RoundToInt(radius * 2f * 100f));
            var tex = new Texture2D(d, d, TextureFormat.RGBA32, false);
            var pixels = new Color32[d * d];
            var white = new Color32(255, 255, 255, 255);
            var clear = new Color32(0, 0, 0, 0);
            float cx = (d - 1) * 0.5f;
            float cy = (d - 1) * 0.5f;
            float r = d * 0.5f - 0.5f;
            float r2 = r * r;
            for (int y = 0; y < d; y++)
            {
                for (int x = 0; x < d; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    pixels[y * d + x] = (dx * dx + dy * dy <= r2) ? white : clear;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite MakeTriangleSprite(float b, float h)
        {
            int w = Mathf.Max(4, Mathf.RoundToInt(b * 100f));
            int ht = Mathf.Max(4, Mathf.RoundToInt(h * 100f));
            var tex = new Texture2D(w, ht, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * ht];
            var white = new Color32(255, 255, 255, 255);
            var clear = new Color32(0, 0, 0, 0);
            float cx = (w - 1) * 0.5f;
            for (int y = 0; y < ht; y++)
            {
                float t = (ht - 1 == 0) ? 0f : (float)y / (ht - 1);
                float halfWidth = (w * 0.5f) * (1f - t);
                for (int x = 0; x < w; x++)
                {
                    pixels[y * w + x] = (Mathf.Abs(x - cx) <= halfWidth) ? white : clear;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, ht), new Vector2(0.5f, 0.5f), 100f);
        }

        void IgnoreInternalCollisions()
        {
            var cols = new List<Collider2D>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var t = transform.GetChild(i);
                if (t.GetComponent<Rigidbody2D>() == null) continue;
                var col = t.GetComponent<Collider2D>();
                if (col != null) cols.Add(col);
            }
            for (int a = 0; a < cols.Count; a++)
                for (int b = a + 1; b < cols.Count; b++)
                    Physics2D.IgnoreCollision(cols[a], cols[b], true);
        }
    }
}
