#ifndef META_TEXTURE_INCLUDED
#define META_TEXTURE_INCLUDED

// Based on "Real-time non-photorealistic animation for immersive storytelling in “Age of Sail”"
// Link: https://www.sciencedirect.com/science/article/pii/S2590148619300123#eq0002
// (alt) https://storage.googleapis.com/pub-tools-public-publication-data/pdf/391e12ba29e5430c9016a1c66846a3dbf6438bb8.pdf

// TODO: Add '3.5. Orienting texture to indicate contour'
// TODO: Add '4.3. Compensating for camera roll'

void GetUVGradientFromDerivatives (float2 uv, out float2 gradU, out float2 gradV)
{
    // Use ddx_fine/ddy_fine to get better results than ddx/ddy.
    float2 dx = ddx_fine(uv);
    float2 dy = ddy_fine(uv);
    gradU = float2(dx.x, dy.x);
    gradV = float2(dx.y, dy.y);
}

// 3.2. Approximate smooth UV gradients
void GetApproximateSmoothUVGradient (float2 uv, float4 UVGradWS, float3 tangentVS, float3 binormalVS, out float2 gradU, out float2 gradV)
{
    // Tₛ = screen-space projections of the unit tangent
    float3 tangentSS = tangentVS;

    // Bₛ = screen-space projections of the unit binormal
    float3 binormalSS = binormalVS;

    // ∇ᵥᵥu = world-space UV gradients
    float2 gradUWS = UVGradWS.xy;

    // ∇ᵥᵥv = world-space UV gradients
    float2 gradVWS = UVGradWS.zw;

    // Eq.9
    // ∇ₛu = |∇ᵥᵥu|Tₛ/|Tₛ|²
    // ∇ₛv = |∇ᵥᵥv|Bₛ/|Bₛ|²
    gradV = length(gradVWS) * tangentSS.xy / length(tangentSS) * length(tangentSS);

    // Eq.10
    // ∇ₛv = |∇ᵥᵥv|Bₛ/|Bₛ|²
    gradU = length(gradUWS) * binormalSS.xy / length(binormalSS) * length(binormalSS);
}

float CubicBlend (float x)
{
    // β(x) = −2x³ + 3x²
    return -2 * x * x * x + 3 * x * x;
}
float2 CubicBlend (float2 x)
{
    return float2(-2 * x.x * x.x * x.x + 3 * x.x * x.x, -2 * x.y * x.y * x.y + 3 * x.y * x.y);
}

// Eq.25
// k(t) = 2t² − 2t + 1
float K (float t)
{
    return 2 * t * t - 2 * t + 1;
}

// '3.3. Compensating for radial angle': Obtain the radial angle compensation coefficient
float GetRadialAngleCompensationCoefficient(float2 S, float4x4 P)
{
    // Eq.12
    //      /  Sₓ     Sᵧ  \
    // Q = <  ---- , ----  >
    //      \ P₀,₀   P₁,₁ /
    float2 Q = float2(S.x / P[0][0], S.y / P[1][1]);

    // Eq.13
    // α = |Q|² + 1
    float lenQ = length(Q);
    return lenQ * lenQ + 1.0;
}

// Implementing '3.6. Compensating for contrast reduction'
float4 Contrast(float4 color, float contrast)
{
    // From 'Shader function to adjust texture contrast' post on Unity Forums by Ben Golus
    // https://forum.unity.com/threads/shader-function-to-adjust-texture-contrast.457635/#post-2968747
    return saturate(lerp(float4(0.5, 0.5, 0.5, 0.5), color, contrast));
}
void CompensateContrastReduction (in float2 blend, inout float4 x)
{
    // Eq.24 (Modified)
    // c = k(Bᵤ)k(Bᵥ)k(Bᵥᵥ)
    float c = K(blend.x) * K(blend.y);

    // Apply contrast: "simply multiply the contrast by a single compensation factor c"
    x = Contrast(x, 1.0 + (1.0 - saturate(c)) * 2.0);
}
void CompensateContrastReduction (in float2 blend, in float skewBlend, inout float4 x)
{
    // Eq.24
    // c = k(Bᵤ)k(Bᵥ)k(Bᵥᵥ)
    float c = K(blend.x) * K(blend.y) * K(skewBlend);

    // Apply contrast: "simply multiply the contrast by a single compensation factor c"
    x = Contrast(x, 1.0 + (1.0 - saturate(c)) * 2.0);
}

// '4.2. Animated line boil'
// TODO!

// '4.3. Compensating for camera roll'
// TODO!

// '4.4. Compensating for distance'
void CompensateDistance(float intensity, float d, inout float4 result)
{
    // Eq.26
    // ρ = intensity of the warp effect.
    // d = distance from camera.
    //
    //        ρ
    // ρ′ = ------
    //      1 + d²
    float pPrime = saturate(intensity / (1.0 + d * d));

    // Implement with lerp between no warp value and warp value.
    result = lerp(float4(0.5, 0.5, 0.0, 1.0), result, saturate(pPrime));
}
float CompensateDistance(float intensity, float d)
{
    return saturate(intensity / (1.0 + d * d));
}

// Implementing '3.1. Texture scales and blend coefficients'
float4 SampleMetaTexture(TEXTURE2D(_Tex), SAMPLER(sampler_Tex), float2 uv, float2 gradU, float2 gradV, float a, float w)
{
    // Eq.1
    //      /   1       1   \
    // S = <  ----- , -----  >
    //      \ w|∇u|   w|∇v| /
    //
    //float2 S = float2(1.0 / (w * length(gradU)), 1.0 / (w * length(gradV)));

    // Eq.11 (Replaces Eq.1)
    //      /    1        1   \
    // S = <  ------ , ------  >
    //      \ αw|∇u|   αw|∇v| /
    float2 S = float2(1.0 / (a * w * length(gradU)), 1.0 / (a * w * length(gradV)));

    // Eq.2
    // E = { log₂(Sᵤ), log₂(Sᵥ) }
    //
    // float2 E = float2(log2(S.x), log2(S.y));
    float2 E = log2(S);

    // Eq.3
    // U₀ = { 2⌊ᴱᵘ⌋u, 2⌊ᴱᵛ⌋v }
    // 
    // float2 uv0 = float2(pow(2, (floor(E.x))) * uv.x, pow(2, (floor(E.y))) * uv.y);
    float2 uv0 = float2(exp2(floor(E.x)) * uv.x, exp2(floor(E.y)) * uv.y);

    // Eq.4 (Error in original paper: Eq.4 and Eq.5 are inverted)
    // U₁ = { 2u₀, v₀ }
    float2 uv1 = float2(uv0.x, 2 * uv0.y);

    // Eq.5 (Error in original paper: Eq.4 and Eq.5 are inverted)
    // U₂ = { u₀, 2v₀ }
    float2 uv2 = float2(2 * uv0.x, uv0.y);

    // Eq.6
    // U₃ = { 2u₀, 2v₀ }
    float2 uv3 = float2(2 * uv0.x, 2 * uv0.y);

    // Eq.7
    // B = { β(Eᵤ − ⌊Eᵤ⌋), β(Eᵥ − ⌊Eᵥ⌋) }
    //
    // float2 blend = float2(CubicBlend(E.x - floor(E.x)), CubicBlend(E.y - floor(E.y)));
    // float2 blend = float2(CubicBlend(frac(E.x)), CubicBlend(frac(E.y)));
    float2 blend = CubicBlend(frac(E));

    float4 tex0 = SAMPLE_TEXTURE2D_LOD(_Tex, sampler_Tex, uv0, 0); // T(U₀)
    float4 tex1 = SAMPLE_TEXTURE2D_LOD(_Tex, sampler_Tex, uv1, 0); // T(U₁)
    float4 tex2 = SAMPLE_TEXTURE2D_LOD(_Tex, sampler_Tex, uv2, 0); // T(U₂)
    float4 tex3 = SAMPLE_TEXTURE2D_LOD(_Tex, sampler_Tex, uv3, 0); // T(U₃)

    // Eq.8
    // M(U) = (1 − Bᵥ)[(1 − Bᵤ)T(U₀) + BᵤT(U₂)] +
    //             Bᵥ [(1 − Bᵤ)T(U₁) + BᵤT(U₃)]
    //
    // float4 result = (1.0 - blend.y) * ((1.0 - blend.x) * tex0 + blend.x * tex2) +
    //                        blend.y  * ((1.0 - blend.x) * tex1 + blend.x * tex3);
    float4 result = lerp(lerp(tex0, tex2, blend.x), lerp(tex1, tex3, blend.x), blend.y);

    // '3.6. Compensating for contrast reduction'
    CompensateContrastReduction(blend, result);

    // Done
    return result;
}

// Implementing '3.1. Texture scales and blend coefficients' & '3.4. Compensating for skew'
float4 SampleMetaTextureSkewed(TEXTURE2D(_Tex), SAMPLER(sampler_Tex), float2 uv, float2 gradU, float2 gradV, float a, float w, float n)
{
    // Cache
    float lenGradU = length(gradU);
    float lenGradV = length(gradV);

    // Eq.15
    //      / |∇u|/|∇v|, if |∇u| ≤ |∇v
    // e = <
    //      \ |∇v|/|∇u|, otherwise
    float e = lenGradU <= lenGradV ? lenGradU / lenGradV : lenGradV / lenGradU;
    
    // Eq.14
    //                 ∇u     ∇v  
    // σ = tan⁻¹(ǫ) ( ---- · ---- )
    //                |∇u|   |∇v| 
    //
    // float sigma = atan(e) * dot(gradU / length(gradU), gradV / length(gradV));
    float sigma = atan(e) * dot(normalize(gradU), normalize(gradV));

    // Eq.16
    //      ⌊nσ⌋
    // σ₀ = ---
    //       n
    //
    // float skew0 = floor(sigma * n) / n;
    float sigmaN = sigma * n;
    float flooredSigmaN = floor(sigmaN);
    float skew0 = flooredSigmaN / n;

    // Eq.17
    //      ⌊nσ⌋ + 1
    // σ₁ = -------
    //         n
    //
    // float skew1 = (floor(sigma * n) + 1.0) / n;
    float skew1 = (flooredSigmaN + 1.0) / n;

    // Eq.18
    //              σ₀
    // ψ₀ = tan⁻¹( --- )
    //              2
    float psi0 = atan(skew0 / 2);

    // Eq.19
    //              σ₁
    // ψ₁ = tan⁻¹( --- )
    //              2
    float psi1 = atan(skew1 / 2);

    // Eq.20
    // U′ = { u cos(ψ₀) + v sin(ψ₀), v cos(ψ₀) + u sin(ψ₀) }
    float2 uv0 = float2(uv.x * cos(psi0) + uv.y * sin(psi0), uv.y * cos(psi0) + uv.x * sin(psi0));

    // Eq.21
    // U′′ = { u cos(ψ₁) + v sin(ψ₁), v cos(ψ₁) + u sin(ψ₁) }
    float2 uv1 = float2(uv.x * cos(psi1) + uv.y * sin(psi1), uv.y * cos(psi1) + uv.x * sin(psi1));

    // Eq.22
    // Bᵥᵥ = β(nσ − ⌊nσ⌋)
    //
    // float skewBlend = CubicBlend(n * sigma - floor(n * sigma));
    float skewBlend = CubicBlend(sigmaN - flooredSigmaN);

    // Eq.1
    //      /   1       1   \
    // S = <  ----- , -----  >
    //      \ w|∇u|   w|∇v| /
    //
    //float2 S = float2(1.0 / (w * lenGradU), 1.0 / (w * lenGradV));

    // Eq.11 (Replaces Eq.1)
    //      /    1        1   \
    // S = <  ------ , ------  >
    //      \ αw|∇u|   αw|∇v| /
    float2 S = float2(1.0 / (a * w * length(gradU)), 1.0 / (a * w * length(gradV)));

    // Eq.2
    // E = { log₂(Sᵤ), log₂(Sᵥ) }
    //
    // float2 E = float2(log2(S.x), log2(S.y));
    float2 E = log2(S);

    float2 uv00 = float2(exp2(floor(E.x)) * uv0.x, exp2(floor(E.y)) * uv0.y);   // U′₀ (Eq.3)
    float2 uv01 = float2(uv00.x, 2 * uv00.y);                                   // U′₁ (Eq.4)
    float2 uv02 = float2(2 * uv00.x, uv00.y);                                   // U′₂ (Eq.5)
    float2 uv03 = float2(2 * uv00.x, 2 * uv00.y);                               // U′₃ (Eq.6)
    float2 uv10 = float2(exp2(floor(E.x)) * uv1.x, exp2(floor(E.y)) * uv1.y);   // U′′₀ (Eq.3)
    float2 uv11 = float2(uv10.x, 2 * uv10.y);                                   // U′′₁ (Eq.4)
    float2 uv12 = float2(2 * uv10.x, uv10.y);                                   // U′′₂ (Eq.5)
    float2 uv13 = float2(2 * uv10.x, 2 * uv10.y);                               // U′′₃ (Eq.6)

    // Eq.7
    // B = { β(Eᵤ − ⌊Eᵤ⌋), β(Eᵥ − ⌊Eᵥ⌋) }
    //
    // float2 blend = float2(CubicBlend(E.x - floor(E.x)), CubicBlend(E.y - floor(E.y)));
    // float2 blend = float2(CubicBlend(frac(E.x)), CubicBlend(frac(E.y)));
    float2 blend = CubicBlend(frac(E));

    float4 tex00 = SAMPLE_TEXTURE2D_LOD(_Tex, sampler_Tex, uv00, 0); // T(U′₀)
    float4 tex01 = SAMPLE_TEXTURE2D_LOD(_Tex, sampler_Tex, uv01, 0); // T(U′₁)
    float4 tex02 = SAMPLE_TEXTURE2D_LOD(_Tex, sampler_Tex, uv02, 0); // T(U′₂)
    float4 tex03 = SAMPLE_TEXTURE2D_LOD(_Tex, sampler_Tex, uv03, 0); // T(U′₃)
    float4 tex10 = SAMPLE_TEXTURE2D_LOD(_Tex, sampler_Tex, uv10, 0); // T(U′′₀)
    float4 tex11 = SAMPLE_TEXTURE2D_LOD(_Tex, sampler_Tex, uv11, 0); // T(U′′₁)
    float4 tex12 = SAMPLE_TEXTURE2D_LOD(_Tex, sampler_Tex, uv12, 0); // T(U′′₂)
    float4 tex13 = SAMPLE_TEXTURE2D_LOD(_Tex, sampler_Tex, uv13, 0); // T(U′′₃)

    // Eq.23
    // M(U) = (1 − Bᵥᵥ) { (1 − Bᵥ) [ (1 − Bᵤ)T(U′₀)  + BuT(U′₂)  ]   +
    //                         Bᵥ  [ (1 − Bᵤ)T(U′₁)  + BuT(U′₃)  ] } +
    //             Bᵥᵥ  { (1 − Bᵥ) [ (1 − Bᵤ)T(U′′₀) + BuT(U′′₂) ]   +
    //                         Bᵥ  [ (1 − Bᵤ)T(U′′₁) + BuT(U′′₃) ] }
    //
    // float4 result = (1.0 - skewBlend) * ((1.0 - blend.y) * ((1.0 - blend.x) * tex00 + blend.x * tex02) + blend.y * ((1.0 - blend.x) * tex01 + blend.x * tex03)) +
    //                        skewBlend  * ((1.0 - blend.y) * ((1.0 - blend.x) * tex10 + blend.x * tex12) + blend.y * ((1.0 - blend.x) * tex11 + blend.x * tex13));
    float4 result = lerp(lerp(lerp(tex00, tex02, blend.x), lerp(tex01, tex03, blend.x), blend.y),
                         lerp(lerp(tex10, tex12, blend.x), lerp(tex11, tex13, blend.x), blend.y), skewBlend);

    // '3.6. Compensating for contrast reduction'
    CompensateContrastReduction(blend, skewBlend, result);

    // Done
    return result;
}

#endif