### Moment of Inertia About an Arbitrary Pivot

For the gain formula in SKILL.md Step 3.3, we need each upper-body part's polar moment of inertia about the **hip pivot** (not the part's own centroid), then summed. The parallel-axis theorem handles this exactly for any pivot in the plane:

  `I_pivot = I_centroid + m · d²`,  where  `d² = (x_centroid − x_pivot)² + (y_centroid − y_pivot)²`

So the per-part procedure is:
1. Look up `I_centroid` for the part's shape (table below).
2. Measure `d`, the distance from the part's centroid to the hip pivot.
3. Add `m · d²` to get that part's contribution to `I_upperBody`.

This is exact for 2D rotation about any point — no approximation, no assumption that the pivot is inside the shape or near its centroid.

### Centroidal Polar Moment by Shape

`I_centroid` is the polar moment about the part's own centroid — a rotation-invariant scalar (doesn't depend on how the part is oriented in the rig).

- **Circle** (radius r, mass m): `I_centroid = (1/2) · m · r²`
- **Rectangle / square** (width w, height h, mass m): `I_centroid = m · (w² + h²) / 12`
- **Isosceles triangle** (base b, height h, mass m): `I_centroid = m · (3b² + 4h²) / 72`
- **Capsule** (end-cap radius r, straight-section length l, mass m): see below.

The triangle formula gives the polar moment (rotation about the axis perpendicular to the triangle's plane), which is what 2D physics uses; it is rotation-invariant about the centroid, so the triangle's orientation in the rig does not matter for `I_centroid` — only for locating its centroid.

### Capsule Centroidal Moment

A capsule is a rectangle of size `2r × l` with semicircular caps of radius `r` on each end. Split the total mass between the rectangle and the two caps by area share:

  `m_rect = m · 2rl / (2rl + π·r²)`
  `m_caps = m − m_rect`     (= mass of both rounded caps combined)

Then the closed-form polar moment about the capsule's geometric center is:

  `I_centroid = m_rect · (4r² + l²) / 12  +  m_caps · [ r² · (9π² − 32) / (18π²)  +  (l/2 + 4r/(3π))² ]`

The first term is the rectangle's polar moment about its centroid. The second is the two semicircles combined: their own centroidal polar moment plus the parallel-axis shift from each cap's centroid (at `l/2 + 4r/(3π)` from the capsule center) to the capsule center.

Sanity checks:
- `l → 0`: capsule degenerates to a circle of radius `r`, and the formula gives `I_centroid = (1/2)·m·r²` ✓
- `r → 0`: capsule degenerates to a rod of length `l`, and the formula gives `m·l²/12` ✓

If you need a quick approximation instead of the closed form, treat the capsule as a rod of total length `(l + 2r)`: `I_centroid ≈ m · (l + 2r)² / 12`. This is within ~5% of the exact value for moderate `l/r` ratios and degrades to `m·r²/3` (vs. exact `m·r²/2`) at `l = 0`.

### Computing the Upper Body Inertia

1. Pick the hip pivot. For a symmetric biped with a left and right hip, use the midpoint between them: `(x_pivot, y_pivot)`.
2. For each part rigidly attached above the hips — torso, head, arms, ears, anything across a `FixedJoint2D` weld — compute its mass `m_i`, its centroidal polar moment `I_i` (from the table above), and its centroid position `(x_i, y_i)`.
3. Sum the parallel-axis contributions:

  `I_upperBody = Σ ( I_i + m_i · d_i² )`,  with  `d_i² = (x_i − x_pivot)² + (y_i − y_pivot)²`

This `I_upperBody` is what feeds into `bodyUprightGain` in SKILL.md Step 3.3.

### Worked Example: Default Spawner Rig

Upper body is a single `1.5 × 1.5` rectangle, mass 2.25 (area × upper-body density 1, per `mass-calculation.md`), centered at `(0, 0)`. Hip pivots are at `(±0.4, −0.75)`, midpoint `(0, −0.75)`.

  `I_centroid = 2.25 · (1.5² + 1.5²) / 12 = 0.84375`
  `d² = 0² + 0.75² = 0.5625`
  `m · d² = 2.25 · 0.5625 = 1.265625`
  `I_upperBody = 0.84375 + 1.265625 = 2.109375`

Then `bodyUprightGain = 2 · 2.109375 ≈ 4.22`, matching the default in `examples/Brain.cs`.
