using System;
using System.Collections.Generic;
using UnityEngine;

namespace BipedGeneration2
{
    // SpawnerCopy: identical to examples/Spawner.cs except masses are recomputed
    // from references/mass-calculation.md (area × density; legs = 2, upper body = 1)
    // instead of being hand-tuned.
    //
    //   body  size 1.5 × 1.5 → area 2.25, density 1 → mass 2.25 (was 4)
    //   thigh size 0.3 × 1.0 → area 0.30, density 2 → mass 0.60 (was 0.8)
    //   shin  size 0.3 × 1.0 → area 0.30, density 2 → mass 0.60 (was 0.5)
    //   foot  size 1.0 × 0.2 → area 0.20, density 2 → mass 0.40 (was 0.6)
    //   totalMass = 2.25 + 2·(0.6 + 0.6 + 0.4) = 5.45
    //
    // Pair with skill-building/BrainCopy.cs, whose gains/torques are derived from
    // this totalMass and the resulting upper-body inertia.
    public class SpawnerCopy : MonoBehaviour
    {
        [Header("Body part sizes (world units)")]
        public Vector2 bodySize = new Vector2(1.5f, 1.5f);
        public Vector2 thighSize = new Vector2(0.3f, 1.0f);
        public Vector2 shinSize = new Vector2(0.3f, 1.0f);
        public Vector2 footSize = new Vector2(1.0f, 0.2f);

        [Header("Masses (area × density per mass-calculation.md)")]
        public float bodyMass = 2.25f;
        public float thighMass = 0.6f;
        public float shinMass = 0.6f;
        public float footMass = 0.4f;

        [Header("Rest pose")]
        [Range(0f, 70f)] public float restThighAngle = 45f;
        [Range(-30f, 60f)] public float restShinAngle = 0f;
        public float hipHalfWidth = 0.4f;

        [Header("Joint limits (degrees from the hyperextension limit)")]
        public float hipMaxFlexion = 90f;
        public float kneeMaxBend = 135f;
        public float kneeStraightMargin = 0f;

        [Header("Ground")]
        public bool createGround = true;
        private Vector2 groundSize;
        public float groundTopY = 0f;
        public bool autoPositionAtGround = true;

        [Header("Colors")]
        public Color bodyColor = new Color(0.85f, 0.55f, 0.2f);
        public Color leftLegColor = new Color(0.3f, 0.6f, 0.85f);
        public Color rightLegColor = new Color(0.85f, 0.3f, 0.4f);
        public Color groundColor = new Color(0.35f, 0.3f, 0.25f);

        [Header("Nametag")]
        public Vector2 nametagOffset = new Vector2(0f, 0.5f);
        public float nametagCharSize = 0.18f;
        public int nametagFontSize = 64;
        public Color nametagColor = Color.white;
        public int nametagSortingOrder = 100;

        [Header("Bipeds (drag a controller script per biped)")]
#if UNITY_EDITOR
        [SerializeField, Tooltip("Drag MonoBehaviour controller scripts (must implement IBipedController) — one entry per biped.")]
        private List<UnityEditor.MonoScript> bipedControllerScripts = new List<UnityEditor.MonoScript>();
#endif
        [SerializeField, HideInInspector]
        private List<string> bipedControllerTypeNames = new List<string>();

        [Tooltip("Horizontal spacing between adjacent bipeds, in world units. Clamped to a 5-unit minimum.")]
        public float bipedSpacing = 6f;

        PhysicsMaterial2D footMaterial;

#if UNITY_EDITOR
        void OnValidate()
        {
            bipedControllerTypeNames.Clear();
            foreach (var s in bipedControllerScripts)
            {
                if (s == null) { bipedControllerTypeNames.Add(string.Empty); continue; }
                var t = s.GetClass();
                bool valid = t != null
                    && typeof(MonoBehaviour).IsAssignableFrom(t)
                    && typeof(IBipedController).IsAssignableFrom(t);
                bipedControllerTypeNames.Add(valid ? t.AssemblyQualifiedName : string.Empty);
                if (!valid)
                {
                    Debug.LogWarning($"SpawnerCopy: '{s.name}' is not a MonoBehaviour implementing IBipedController and will be skipped.", this);
                }
            }
        }
#endif

        void Awake()
        {
            footMaterial = new PhysicsMaterial2D("BipedFoot") { friction = 1f, bounciness = 0f };

            float spawnY = transform.position.y;
            if (autoPositionAtGround)
            {
                float thighDy = thighSize.y * Mathf.Cos(restThighAngle * Mathf.Deg2Rad);
                float shinDy = shinSize.y * Mathf.Cos(restShinAngle * Mathf.Deg2Rad);
                spawnY = groundTopY + footSize.y + shinDy + thighDy + bodySize.y * 0.5f + 0.02f;
            }

            int n = Mathf.Max(1, bipedControllerTypeNames.Count);
            float spacing = Mathf.Max(5f, bipedSpacing);
            float startX = transform.position.x - (n - 1) * spacing * 0.5f;

            if (createGround) BuildGround(transform.position.x);

            for (int i = 0; i < n; i++)
            {
                string typeName = i < bipedControllerTypeNames.Count ? bipedControllerTypeNames[i] : null;
                Type controllerType = string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);
                Vector3 origin = new Vector3(startX + i * spacing, spawnY, transform.position.z);
                BuildOneBiped(origin, controllerType, i);
            }
        }

        void BuildGround(float centerX)
        {
            groundSize = new Vector2((1 + bipedControllerTypeNames.Count) * 10, 1f);

            var g = new GameObject("Ground");
            g.transform.position = new Vector3(centerX, groundTopY - groundSize.y * 0.5f, 0f);
            AddSprite(g, groundSize, groundColor);
            var col = g.AddComponent<BoxCollider2D>();
            col.size = groundSize;
            col.sharedMaterial = footMaterial;
        }

        void BuildOneBiped(Vector3 origin, Type controllerType, int index)
        {
            string typeLabel = controllerType != null ? controllerType.Name : "NoController";
            string suffix = "_" + index + "_" + typeLabel;

            var bodyGO = CreatePart("Body" + suffix, origin, 0f, bodySize, bodyMass, bodyColor);
            var bodyRb = bodyGO.GetComponent<Rigidbody2D>();
            bodyRb.angularDrag = 5f;

            var leftLeg = BuildLeg(bodyRb, origin, "Left" + suffix, -1, leftLegColor);
            var rightLeg = BuildLeg(bodyRb, origin, "Right" + suffix, +1, rightLegColor);

            IgnoreInternalCollisions(bodyRb, leftLeg, rightLeg);

            AttachController(bodyGO, controllerType, bodyRb, leftLeg, rightLeg);
            AttachNametag(bodyGO, typeLabel);
        }

        void AttachNametag(GameObject host, string label)
        {
            var tagGO = new GameObject("Nametag");
            tagGO.transform.SetParent(host.transform, false);
            tagGO.transform.localPosition = new Vector3(nametagOffset.x, bodySize.y * 0.5f + nametagOffset.y, 0f);

            var tm = tagGO.AddComponent<TextMesh>();
            tm.text = label;
            tm.fontSize = nametagFontSize;
            tm.characterSize = nametagCharSize;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = nametagColor;
            tm.fontStyle = FontStyle.Bold;

            var mr = tagGO.GetComponent<MeshRenderer>();
            mr.sortingOrder = nametagSortingOrder;

            tagGO.AddComponent<BipedNametag>();
        }

        void AttachController(GameObject host, Type controllerType, Rigidbody2D body, LegRig left, LegRig right)
        {
            if (controllerType == null) return;
            var added = host.AddComponent(controllerType) as IBipedController;
            if (added != null)
            {
                added.SetRig(body, left, right);
            }
            else
            {
                Debug.LogWarning($"SpawnerCopy: '{controllerType.Name}' does not implement IBipedController — skipping SetRig.", this);
            }
        }

        LegRig BuildLeg(Rigidbody2D bodyRb, Vector3 origin, string side, int sign, Color color)
        {
            var leg = new LegRig { layoutSign = sign, outwardSign = -sign };

            Vector2 hipPos = (Vector2)origin + new Vector2(sign * hipHalfWidth, -bodySize.y * 0.5f);

            float thighZ = sign * restThighAngle;
            Vector2 thighDir = AngleToDownVector(thighZ);
            Vector2 thighCenter = hipPos + thighDir * (thighSize.y * 0.5f);
            var thighGO = CreatePart(side + "Thigh", thighCenter, thighZ, thighSize, thighMass, color);
            leg.thigh = thighGO.GetComponent<Rigidbody2D>();

            Vector2 kneePos = hipPos + thighDir * thighSize.y;

            float shinZ = sign * restShinAngle;
            Vector2 shinDir = AngleToDownVector(shinZ);
            Vector2 shinCenter = kneePos + shinDir * (shinSize.y * 0.5f);
            var shinGO = CreatePart(side + "Shin", shinCenter, shinZ, shinSize, shinMass, color * 0.85f);
            leg.shin = shinGO.GetComponent<Rigidbody2D>();

            Vector2 anklePos = kneePos + shinDir * shinSize.y;
            Vector2 footCenter = anklePos + new Vector2(sign * footSize.x * 0.25f, -footSize.y * 0.5f);
            var footGO = CreatePart(side + "Foot", footCenter, 0f, footSize, footMass, color * 0.7f);
            leg.foot = footGO.GetComponent<Rigidbody2D>();
            footGO.GetComponent<BoxCollider2D>().sharedMaterial = footMaterial;

            leg.hip = AddHinge(thighGO, bodyRb, hipPos);
            leg.knee = AddHinge(shinGO, leg.thigh, kneePos);
            leg.ankle = AddHinge(footGO, leg.shin, anklePos);

            ApplyHipLimits(leg);
            ApplyKneeLimits(leg);
            ApplyAnkleLimits(leg);

            return leg;
        }

        void ApplyHipLimits(LegRig leg)
        {
            float hyperextJa = -leg.outwardSign * restThighAngle;
            float flexionJa = -leg.outwardSign * (restThighAngle - hipMaxFlexion);
            leg.hip.useLimits = true;
            leg.hip.limits = new JointAngleLimits2D
            {
                min = Mathf.Min(hyperextJa, flexionJa),
                max = Mathf.Max(hyperextJa, flexionJa)
            };
        }

        void ApplyKneeLimits(LegRig leg)
        {
            float straightJa = leg.outwardSign * (restThighAngle - restShinAngle - kneeStraightMargin);
            float bendJa = leg.outwardSign * (restThighAngle - restShinAngle - kneeMaxBend);
            leg.knee.useLimits = true;
            leg.knee.limits = new JointAngleLimits2D
            {
                min = Mathf.Min(straightJa, bendJa),
                max = Mathf.Max(straightJa, bendJa)
            };
        }

        void ApplyAnkleLimits(LegRig leg)
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

        static Vector2 AngleToDownVector(float zDegrees)
        {
            float rad = zDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
        }

        GameObject CreatePart(string name, Vector2 worldPos, float zRotation, Vector2 size, float mass, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, true);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, zRotation);

            AddSprite(go, size, color);

            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.mass = mass;
            rb.drag = 0.05f;
            rb.angularDrag = 1.5f;
            rb.position = worldPos;
            rb.rotation = zRotation;

            return go;
        }

        static void AddSprite(GameObject go, Vector2 size, Color color)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeRectSprite(size);
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

        void IgnoreInternalCollisions(Rigidbody2D body, LegRig left, LegRig right)
        {
            Rigidbody2D[] bodies =
            {
                body,
                left.thigh, left.shin, left.foot,
                right.thigh, right.shin, right.foot
            };
            for (int a = 0; a < bodies.Length; a++)
            {
                for (int b = a + 1; b < bodies.Length; b++)
                {
                    var ca = bodies[a].GetComponent<Collider2D>();
                    var cb = bodies[b].GetComponent<Collider2D>();
                    if (ca && cb) Physics2D.IgnoreCollision(ca, cb, true);
                }
            }
        }
    }
}
