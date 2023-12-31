#ifndef META_TEXTURE_INCLUDED
#define META_TEXTURE_INCLUDED

// Based on "Real-time non-photorealistic animation for immersive storytelling in “Age of Sail”"
// Link: https://www.sciencedirect.com/science/article/pii/S2590148619300123#eq0002
// (alt) https://storage.googleapis.com/pub-tools-public-publication-data/pdf/391e12ba29e5430c9016a1c66846a3dbf6438bb8.pdf

// TODO: Add '3.5. Orienting texture to indicate contour'

void GetUVGradientFromDerivatives (float2 uv, out float2 gradU, out float2 gradV)
{
    // Use ddx_fine/ddy_fine to get better results than ddx/ddy.
    float2 dx = ddx_fine(uv);
    float2 dy = ddy_fine(uv);
    gradU = float2(dx.x, dy.x);
    gradV = float2(dx.y, dy.y);
}

// 3.2. Approximate smooth UV gradients
float4 GetApproximateSmoothUVGradient (float3 tangentVS, float3 binormalVS, float3 viewDir, float2 gradUWS, float2 gradVWS)
{
    // Project the view-space vectors onto the view direction to get vectors orthogonal to the camera
    //   Tₛ = screen-space projections of the unit tangent
	float3 tangentSS = tangentVS - (dot(tangentVS, viewDir) * viewDir);
    //   Bₛ = screen-space projections of the unit binormal
	float3 binormalSS = binormalVS - (dot(binormalVS, viewDir) * viewDir);

    // Eq.9
    // ∇ₛu = |∇ᵥᵥu|Tₛ/|Tₛ|²
	float2 gradU = gradUWS * tangentSS.xy / pow(length(tangentSS), 2.0); 

    // Eq.10
    // ∇ₛv = |∇ᵥᵥv|Bₛ/|Bₛ|²
	float2 gradV = gradVWS * binormalSS.xy / pow(length(binormalSS), 2.0);

    // Return ∇ₛu as XY and ∇ₛv as ZW
    return float4(gradU, gradV);
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

// Implementing '3.5. Orienting texture to indicate contour'
// _REORIENT_CONTOUR: Reorients the direction of the texture (switches X and Y values of the UVs) to follow the contours of the mesh using
//                    the relative magnitudes of ∇u and ∇v. This is how they do it in the short film 'Age of Sail'.
// _REORIENT_ALL: Simply reorients the direction of the texture by switching X and Y values of the UVs. This is especially helpful for some
//                meshes like a cylinder mesh which would not have its UVs reoriented using the _REORIENT_CONTOUR method presented in the
//                original paper.
// This effect can be deactivated too by simply not modifying the UVs.
void Reorient(in float diff, inout float2 uv0, inout float2 uv1, inout float2 uv2, inout float2 uv3)
{
#if defined(_REORIENT_CONTOUR)
    float diffA = step( 1.0, diff);
    float diffB = step( 0.0, diff);
    float diffC = step(-1.0, diff);
    
    uv0.xy = lerp(uv0.xy, uv0.yx, diffB);
    uv1.xy = lerp(uv1.xy, uv1.yx, diffA);
    uv2.xy = lerp(uv2.xy, uv2.yx, diffC);
    uv3.xy = lerp(uv3.xy, uv3.yx, diffB);
#elif defined(_REORIENT_ALL)
    uv0.xy = uv0.yx;
    uv1.xy = uv1.yx;
    uv2.xy = uv2.yx;
    uv3.xy = uv3.yx;
#endif
}
void Reorient(in float diff, inout float2 uv00, inout float2 uv01, inout float2 uv02, inout float2 uv03,
                             inout float2 uv10, inout float2 uv11, inout float2 uv12, inout float2 uv13)
{
#if defined(_REORIENT_CONTOUR)
    float diffA = step( 1.0, diff);
    float diffB = step( 0.0, diff);
    float diffC = step(-1.0, diff);

    uv00.xy = lerp(uv00.xy, uv00.yx, diffB);
    uv01.xy = lerp(uv01.xy, uv01.yx, diffA);
    uv02.xy = lerp(uv02.xy, uv02.yx, diffC);
    uv03.xy = lerp(uv03.xy, uv03.yx, diffB);

    uv10.xy = lerp(uv10.xy, uv10.yx, diffB);
    uv11.xy = lerp(uv11.xy, uv11.yx, diffA);
    uv12.xy = lerp(uv12.xy, uv12.yx, diffC);
    uv13.xy = lerp(uv13.xy, uv13.yx, diffB);
#elif defined(_REORIENT_ALL)
    uv00.xy = uv00.yx;
    uv01.xy = uv01.yx;
    uv02.xy = uv02.yx;
    uv03.xy = uv03.yx;

    uv10.xy = uv10.yx;
    uv11.xy = uv11.yx;
    uv12.xy = uv12.yx;
    uv13.xy = uv13.yx;
#endif
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
    float2 flooredE = floor(E);

    // Eq.3
    // U₀ = { 2⌊ᴱᵘ⌋u, 2⌊ᴱᵛ⌋v }
    // 
    // float2 uv0 = float2(pow(2, (floor(E.x))) * uv.x, pow(2, (floor(E.y))) * uv.y);
    // float2 uv0 = float2(exp2(floor(E.x)) * uv.x, exp2(floor(E.y)) * uv.y);
    float2 uv0 = exp2(flooredE) * uv;

    // Eq.4 (Error in original paper: Eq.4 and Eq.5 are inverted)
    // U₁ = { 2u₀, v₀ }
    float2 uv1 = float2(uv0.x, 2 * uv0.y);

    // Eq.5 (Error in original paper: Eq.4 and Eq.5 are inverted)
    // U₂ = { u₀, 2v₀ }
    float2 uv2 = float2(2 * uv0.x, uv0.y);

    // Eq.6
    // U₃ = { 2u₀, 2v₀ }
    float2 uv3 = float2(2 * uv0.x, 2 * uv0.y);

    // '3.5. Orienting texture to indicate contour'
    Reorient(flooredE.x - flooredE.y, uv0, uv1, uv2, uv3);
    
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
    float2 flooredE = floor(E);
    float2 scaledFlooredE = exp2(flooredE);

    float2 uv00 = scaledFlooredE * uv0;                                         // U′₀ (Eq.3)
    float2 uv01 = float2(uv00.x, 2 * uv00.y);                                   // U′₁ (Eq.4)
    float2 uv02 = float2(2 * uv00.x, uv00.y);                                   // U′₂ (Eq.5)
    float2 uv03 = float2(2 * uv00.x, 2 * uv00.y);                               // U′₃ (Eq.6)
    float2 uv10 = scaledFlooredE * uv1;                                         // U′′₀ (Eq.3)
    float2 uv11 = float2(uv10.x, 2 * uv10.y);                                   // U′′₁ (Eq.4)
    float2 uv12 = float2(2 * uv10.x, uv10.y);                                   // U′′₂ (Eq.5)
    float2 uv13 = float2(2 * uv10.x, 2 * uv10.y);                               // U′′₃ (Eq.6)

    // '3.5. Orienting texture to indicate contour'
    Reorient(flooredE.x - flooredE.y, uv00, uv01, uv02, uv03, uv10, uv11, uv12, uv13);

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