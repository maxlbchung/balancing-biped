### Calculating Mass of Body Part

For each body part:

1. Determine the formula for the primitive 2D shape being used (e.g. Capsule = pi * r^2 + 2 * r * l)

2. Use formula to calculate the area of the body part (e.g. Area = pi * 2^2 + 2 * 2 * 5 = 32.57)

3. Multiply area by the density factor for that part to get mass:
   - **Legs (thigh, shin, foot, and any additional leg segments):** density = 2
   - **Upper body (body/torso, head, arms, ears, and anything else rigidly attached above the hips):** density = 1

   (e.g. for a leg part of area 32.57: Mass = 32.57 * 2 = 65.14)