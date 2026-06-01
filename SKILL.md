---
name: balancing-biped
description: Use when the user asks to make a balancing biped ragdoll with realistic physics in Unity 2D. Creates the body and joints based on user description and/or image, and creates the brain that controls the balancing.
---

# Balancing Biped

## Deliverables
- A Spawner.cs monobehavior C# script **(Step 2)**
- A Brain.cs monobehavior C# script **(Step 3)**

## Step 0 - Read the Image (if provided)

If the user attached an image, examine it **before** asking any questions. Extract:
- Number of leg segments (2 = thigh + shin; 3 = thigh 1 + thigh 2 + calf; etc.)
- Whether feet are present
- Primitive shape used for each body part (capsule, square, circle, etc. — see `references/unity-2d-primitives.md`)
- Approximate proportions of each part
- Joint location on each parent part, expressed as a **clock position** (e.g. "hips on the torso circle at 3 o'clock and 9 o'clock", "knee at 6 o'clock on the thigh"). Avoid vague phrases like "either end" or "at the sides" — pin it to a clock direction so it survives into Step 2 as a concrete coordinate.
- Color of each body part
- Resting stance: how splayed the legs are, foot orientation

Use these values to pre-fill defaults for Step 1's choices, and to drive shape, proportion, and color decisions in Step 2.

## Step 1 - Plan

**Required: call `AskUserQuestion` before generating any code.** The "(Recommended)" markers below are option labels presented inside the AskUserQuestion UI — they are not silent defaults you may apply on the user's behalf. Skipping this step is incorrect, even if every row's recommended option seems reasonable.

For each row in the table below, include the question in the AskUserQuestion call unless the user's prompt explicitly named that exact choice (e.g., "use high-friction feet", "no floor") or it is shown in the image (e.g. image has feet). Do not skip a row because the answer feels obvious — only skip when the user has already stated it.

Bundle every remaining question into a **single** `AskUserQuestion` call. Put the recommended option first and append "(Recommended)" to its label. Do this once at the start of the flow — don't drip-feed questions later as new steps come up.

| Choice           | Options                                                                | Inferrable from image? |
| ---------------- | ---------------------------------------------------------------------- | ---------------------- |
| Body parts       | Confirm: "[summary from Step 0]" (Recommended) / Re-describe           | Required (see note below) |
| Feet             | Add Feet (Recommended) / No Feet                                       | Yes — are feet drawn?  |
| Build Floor      | Build floor (Recommended) / No Floor                                   | No                     |
| Feet Friction    | High (1.5) (Recommended) / Medium (1) / Low (0.5)                      | No                     |

**Body parts row:** include this row only when an image was attached. Populate the "Confirm" option label with your actual Step 0 extraction — e.g., `Confirm: "Circular torso, 2 legs × 2 capsule segments each, rectangular feet. Hip joints on the torso circle at 3 o'clock and 9 o'clock."`. List every part you intend to instantiate, and each joint's clock position on its parent. *Only* list parts visible in the image — do not pattern-complete features (e.g. don't add arms or a head just because the creature is humanoid). If the user selects "Re-describe" or provides a correction via the "Other" option, update your extraction and re-ask before proceeding to Step 2.

## Step 2 - Build the body

See `examples/Spawner.cs` for a reference implementation. The sub-steps below (Skeleton, Ligaments, Resting Pose) are presented in conceptual order, but in practice are interleaved per-segment — each leg segment is instantiated, joined to its parent, and set to its rest angle before moving to the next.

The example `BuildLeg` is hardcoded to thigh→shin→foot. If Step 0 inferred more than two leg segments, extend `BuildLeg` to chain the additional segments (each instantiated, hinged to its parent, and given intermediate rest angles per Step 2.3) before generating the rest of Step 2.

### 1. Skeleton

If an image was attached, match the primitive shape, proportions, and colors of each body part to the image. Otherwise fall back to the user's text description. Use only Unity 2D primitive shapes — list in `references/unity-2d-primitives.md`.

The example's `MakeRectSprite` / `BoxCollider2D` path only produces rectangles. For each part whose requested primitive is not a rectangle, swap both the sprite generation and the collider to match the requested shape (e.g. `CircleCollider2D` for circles, `PolygonCollider2D` for triangles/hexagons).

**Joint anchor placement.** The example's `hipPos = origin + (sign * hipHalfWidth, -bodySize.y * 0.5f)` is hardcoded for a *rectangular* torso — it places hips at the bottom corners. For any other shape, derive each joint's world position directly from the clock locations agreed in Step 1: locate where each clock position falls on the parent's actual boundary, then add that offset to the parent's world center.

Set friction of feet using a physics material 2D.

Calculate the mass of each body part based off their size, referencing the three step process in `references/mass-calculation.md` for each body part.

**After `AddComponent<Rigidbody2D>()` on each part, explicitly set `rb.position = worldPos` and `rb.rotation = zRotation` (matching the values you used on the transform).** These lines look redundant with `transform.position` / `transform.rotation` but are not — the Box2D body's internal angle is what `HingeJoint2D` reads when computing `referenceAngle` at joint creation, and without the explicit `rb.rotation` set the body can still be at `0` while the transform shows the rotated value. The joint then bakes the wrong reference angle, every joint-limit endpoint lands on the wrong physical position, and limbs swing through what should be the limits. The example in `Spawner.cs` includes these lines on every part — do the same.

### 2. Ligaments

Connect lower body parts using joints component, with rotation limits applied. Anchor each joint at the world position computed in Step 2.1 from the agreed clock locations. Upper body objects should remain rigid. If arms are included, keep them held out 45 degrees outward just like the legs resting angle.

Be careful to take into account the initial instantiation position when creating the angle limits — check whether the limits are expressed relative to the joint's starting rotation or to world rotation.

- **Hip:** range is 90 degrees, from parallel with the ground (outwards) to perpendicular to it (downwards).
- **Knee:** range is 135 degrees. From straight (shin aligned with the leg segment above) at one limit, to 45 degrees past perpendicular bend (135 degrees of bend from straight, foot folded toward the body) at the other limit, preventing hyperextension. Both knees point in opposite directions outwards from the body.
- **Ankle:** range is 135 degrees. From the foot pointing directly down (parallel to the leg segment above, toes down) at one limit, to 45 degrees up from the resting perpendicular position (toes raised above horizontal) at the other limit.

### 3. Resting Pose

Set angle resting position of each limb.

First segment of leg from hips should be 45 degrees outward.
Last leg segment should be pointing directly down (0 degrees outward).

If leg has more than 2 segments, each segment between the top and bottom should be at an in-between angle (e.g. 30 degrees outward).

Feet should be perpendicular to the last leg segment.


## Step 3 - Build the Brain

See `examples/Brain.cs` for a reference implementation.

The torque and gain formulas in 3.2 and 3.3 depend on the rig's actual masses and rotational inertias. Evaluate them numerically against the rig built in Step 2 and write the resulting numbers in as hardcoded constants in Brain.cs — do not emit the formulas themselves into the script.

### 1. Natural Stance Behavior

Each joint's `motorSpeed` is driven each FixedUpdate by a P-controller on angular error, clamped to `maxMotorSpeed` (default 300). Per-joint targets and gains (defaults shown):

- Hip — combines a body-uprightness term and a joint-angle rest term:
  `motorSpeed = bodyError × bodyUprightGain − (hipJointAngle − hipTargetOffset) × hipRestGain`

- Knee — driven to a world rotation of 0 (shin pointing straight down):
  `motorSpeed = shinWorldRotation × kneeGain`.

- Ankle — driven to a small outward rest angle in **world** coordinates (foot stays flat to the ground regardless of how the shin is tilted):
  `motorSpeed = (footWorldRotation − ankleTarget) × ankleGain`.

### 2. Motor Torque

Scale the base hip torque depending on mass while preserving the ratios (default hipTorque ≈ 20 * totalMass, where totalMass is the sum of all body-part masses).
If creature is very lanky and tall, can increase the constant.

Each joint's `maxMotorTorque` is a flat per-joint ceiling. The ratios between joints are:

- Hip torque: 1
- Knee torque: 0.5
- Ankle torque: 0.43

### 3. Motor Gain

`bodyUprightGain` scales with the upper body's rotational inertia about the pivot point where it meets the legs (the inverted pendulum's hinge). Computing inertia at the pivot rather than the COM makes the height penalty implicit — every part's contribution is `I_part + m_part · d_part²` where `d_part` is its distance from the pivot, so taller / wider-armed bodies naturally pick up larger inertia without needing a separate `h²` term.

- `bodyUprightGain = 2 * I_upperBody`
  - **I_upperBody**: combined moment of inertia of every part of the upper body — body/torso, head, arms, ears, anything else rigidly attached above the hips (including across `FixedJoint2D` welds) — computed about the pivot point where the upper body meets the legs (use the midpoint between the two hip pivots if they aren't co-located). See `references/inertia-calculation.md` for the per-shape formulas and the parallel-axis combine.
- `hipRestGain = bodyUprightGain`
- `kneeGain = 0.56 * bodyUprightGain`
- `ankleGain = 0.44 * bodyUprightGain`

### 4. Body Tilt Detection

To detect when the creature is tilting over and action must be taken, all of the following must hold:

- Body rotation is greater than the tilt deadzone (default to 5 degrees)
- Smoothed body angular velocity by lerp factor (default `angularVelocitySmoothing` = 0.1) is greater than the deadzone (default to 3 degrees/sec)
- Body angular velocity is NOT in the opposite direction as the tilt

When all three hold, the creature is falling over — activate tilt resistance.

### 5. Body Tilt Resistance

When the creature is falling over, change desired position of each joint and add separate strength multipliers to each leg. Reference `references/leg-tilt-behavior.md`.

The torque multipliers should scale linearly with absolute tilt, with the maximum multiplier being reached when the tilt surpasses deadzone angle (ex: deadzone 5 deg, full range 30 deg, so at 5 deg multiplier is zero, while at 35 the multiplier is at full force) (tiltFullScaleRange = 30).
