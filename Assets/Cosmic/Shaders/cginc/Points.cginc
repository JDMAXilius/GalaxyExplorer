#ifndef COSMIC_POINTS_INCLUDED
#define COSMIC_POINTS_INCLUDED

// Cosmic.StarVert, field for field: 10 floats, 40 bytes. The C# struct and this must move together.
struct StarVert
{
    float yOffset;
    float curveOffset;
    float ellipseDistance;
    float ellipseOffset;
    float3 color;
    float2 uv;
    float size;
};

StructuredBuffer<StarVert> _Stars;

float _Age;
float4 _EllipseSize;

static const uint StarQuadStripIndex[6] = { 0, 1, 2, 2, 1, 3 };
static const float2 StarQuadOffsets[4] = { float2(0, -1), float2(1, 0), float2(-1, 0), float2(0, 1) };
static const float2 StarQuadUVs[4] = { float2(1, 0), float2(1, 1), float2(0, 0), float2(0, 1) };

void StarQuadCorner(uint vertexId, out uint pointIndex, out uint corner)
{
    pointIndex = vertexId / 6;
    corner = StarQuadStripIndex[vertexId % 6];
}

float4 StarQuadOffset(float4 clipPos, uint corner, float halfSize)
{
    float2 offset = StarQuadOffsets[corner] * max(halfSize, 0);
    clipPos.x += offset.x * UNITY_MATRIX_P._11;
    clipPos.y += offset.y * UNITY_MATRIX_P._22;
    return clipPos;
}

float3 SpiralPosition(StarVert star)
{
    float curveOffset = star.curveOffset + _Age;
    float x = cos(curveOffset) * _EllipseSize.x;
    float z = sin(curveOffset) * _EllipseSize.y;
    float zp = z * cos(star.ellipseOffset) - x * sin(star.ellipseOffset);
    float xp = z * sin(star.ellipseOffset) + x * cos(star.ellipseOffset);
    return float3(xp * star.ellipseDistance, star.yOffset, zp * star.ellipseDistance);
}

float3 CylinderPosition(StarVert p)
{
    float angle = p.curveOffset + _Age;
    return float3(cos(angle) * p.ellipseDistance, p.yOffset, sin(angle) * p.ellipseDistance);
}

float StarPhase(uint pointIndex)
{
    uint h = pointIndex * 747796405u + 2891336453u;
    h = ((h >> ((h >> 28) + 4u)) ^ h) * 277803737u;
    h = (h >> 22) ^ h;
    return (h & 0xFFFFFFu) * (1.0 / 16777216.0);
}

#endif
