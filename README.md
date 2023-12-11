# AgeOfSail-NPR-Unity

Unity implementation of the paper _[Real-time non-photorealistic animation for immersive storytelling in “Age of Sail”](https://www.sciencedirect.com/science/article/pii/S2590148619300123#eq0002)_ ([alt link](https://storage.googleapis.com/pub-tools-public-publication-data/pdf/391e12ba29e5430c9016a1c66846a3dbf6438bb8.pdf)).

Using [Jasper Flick/Catlike Coding](https://catlikecoding.com/)'s [Custom SRP Project](https://bitbucket.org/catlikecoding-projects/custom-srp-project/src/master/) as a base for the Rendering Pipeline.

![Screenshot of the result in Unity.](/screenshot.jpg)

**Disclaimer:**
I mostly wanted to implement and share the MetaTexture so I tried to comment it as well as possible to link the shader code to the original paper.
The rest of the code, mostly the modified elements from the Rendering Pipeline, is very messy and was built for quickly to showcase possible effects. It might get fixed in the future though.

**Implementation _(based on the Paper)_:**
- 3. MetaTexture
    - [x] 3.1. Texture scales and blend coefficients
    - [X] 3.2. Approximate smooth UV gradients
    - [X] 3.3. Compensating for radial angle
    - [x] 3.4. Compensating for skew
    - [ ] 3.5. Orienting texture to indicate contour
    - [x] 3.6. Compensating for contrast reduction

- 4. Edge Breakup
    - [x] 4.1. Edge inflation
    - [X] 4.2. Animated line boil
    - [X] 4.3. Compensating for camera roll
    - [x] 4.4. Compensating for distance

- 5. The Rendering Pipeline
    - [x] Warp Pass
    - [X] Shadow Pass
    - [X] \(WIP) Blur Pass
    - [X] 5.1. Shadow shapes, inner glow, and indication
    - [X] 5.2. Color control

- 6. Applications
    - _Not planned._

**Improvements:**
- [X] Mesh importer that creates proper normals for edge inflation (especially for facetted meshes with non smooth normals).
- [X] Mesh importer support for open meshes (like planes, tubes, etc.).
- [X] Colored lights.
- [ ] Simplified shading model.
- [ ] MetaTexture distance fade zoom support.