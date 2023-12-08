# AgeOfSail-NPR-Unity

Unity implementation of the paper _[Real-time non-photorealistic animation for immersive storytelling in “Age of Sail”](https://www.sciencedirect.com/science/article/pii/S2590148619300123#eq0002)_ ([alt link](https://storage.googleapis.com/pub-tools-public-publication-data/pdf/391e12ba29e5430c9016a1c66846a3dbf6438bb8.pdf)).

Using [Jasper Flick/Catlike Coding](https://catlikecoding.com/)'s [Custom SRP Project](https://bitbucket.org/catlikecoding-projects/custom-srp-project/src/master/) as a base for the Rendering Pipeline.


**Implementation _(based on the Paper)_:**
- 3. MetaTexture
    - [x] 3.1. Texture scales and blend coefficients
    - [X] \(WIP) 3.2. Approximate smooth UV gradients
    - [X] 3.3. Compensating for radial angle
    - [x] 3.4. Compensating for skew
    - [ ] 3.5. Orienting texture to indicate contour
    - [x] 3.6. Compensating for contrast reduction

- 4. Edge Breakup
    - [x] 4.1. Edge inflation
    - [X] 4.2. Animated line boil
    - [ ] 4.3. Compensating for camera roll
    - [x] 4.4. Compensating for distance

- 5. The Rendering Pipeline
    - [ ] 5.1. Shadow shapes, inner glow, and indication
    - [ ] 5.2. Color control
    - [x] Warp Pass
    - [ ] Shadow Pass
    - [ ] Blur Pass

- 6. Applications
    - _Not implementing._