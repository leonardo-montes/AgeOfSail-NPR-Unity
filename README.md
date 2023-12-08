# AgeOfSail-NPR-Unity

Unity implementation of the paper _[Real-time non-photorealistic animation for immersive storytelling in “Age of Sail”](https://www.sciencedirect.com/science/article/pii/S2590148619300123#eq0002)_ ([alt link](https://storage.googleapis.com/pub-tools-public-publication-data/pdf/391e12ba29e5430c9016a1c66846a3dbf6438bb8.pdf)).

Using [Jasper Flick/Catlike Coding](https://catlikecoding.com/)'s [Custom SRP Project](https://bitbucket.org/catlikecoding-projects/custom-srp-project/src/master/) as a base for the Rendering Pipeline.

Implemented (based on the Paper):
- 3.1. Texture scales and blend coefficients
- 3.4. Compensating for skew
- 3.6. Compensating for contrast reduction
- 4.1. Edge inflation
- 4.4. Compensating for distance
- Warp Pass

TODO (based on the Paper):
- 3.2. Approximate smooth UV gradients
- 3.3. Compensating for radial angle
- 3.5. Orienting texture to indicate contour
- 4.2. Animated line boil
- 4.3. Compensating for camera roll
- 5.1. Shadow shapes, inner glow, and indication
- 5.2. Color control
- Shadow Pass
- Blur Pass