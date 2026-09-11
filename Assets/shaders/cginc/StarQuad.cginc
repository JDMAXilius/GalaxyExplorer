// Licensed under the MIT License. See LICENSE in the project root for license information.

// Stars are camera-facing quads expanded in the vertex shader. Geometry shaders used to do this, but Meta Quest's
// GPU driver does not support them together with multiview stereo. Draw with MeshTopology.Triangles and
// 6 vertices per star (two triangles), and get the star and corner from SV_VertexID.

#ifndef STAR_QUAD_INCLUDED
#define STAR_QUAD_INCLUDED

// The geometry shaders emitted a 4-vertex strip: bottom, right, left, top.
static const uint StarQuadStripIndex[6] = { 0, 1, 2, 2, 1, 3 };
static const float2 StarQuadOffsets[4] = { float2(0, -1), float2(1, 0), float2(-1, 0), float2(0, 1) };
static const float2 StarQuadUVs[4] = { float2(1, 0), float2(1, 1), float2(0, 0), float2(0, 1) };

// Splits a vertex id into the index of the star it belongs to and the quad corner (0-3) it draws.
void StarQuadCorner(uint vertexId, out uint starIndex, out uint corner)
{
    starIndex = vertexId / 6;
    corner = StarQuadStripIndex[vertexId % 6];
}

// Moves a clip-space star position to one corner of a quad with the given half size (view-space units).
float4 StarQuadOffset(float4 clipPos, uint corner, float halfSize)
{
    float2 offset = StarQuadOffsets[corner] * max(halfSize, 0);
    clipPos.x += offset.x * UNITY_MATRIX_P._11;
    clipPos.y += offset.y * UNITY_MATRIX_P._22;
    return clipPos;
}

#endif
