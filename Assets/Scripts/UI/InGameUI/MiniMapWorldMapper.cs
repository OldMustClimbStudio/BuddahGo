using System;

public sealed class MiniMapWorldMapper
{
    // World bounds (single source of truth).
    // Left/Right are defined by world Z.
    // Bottom/Top are defined by world X.
    private readonly float _leftWorldZ;
    private readonly float _rightWorldZ;
    private readonly float _bottomWorldX;
    private readonly float _topWorldX;

    // Minimap image size in UI pixels.
    private readonly float _mapWidth;
    private readonly float _mapHeight;

    private readonly float _zSpan;
    private readonly float _xSpan;

    public MiniMapWorldMapper(
        float leftWorldZ,
        float rightWorldZ,
        float bottomWorldX,
        float topWorldX,
        float mapWidth,
        float mapHeight)
    {
        _leftWorldZ = leftWorldZ;
        _rightWorldZ = rightWorldZ;
        _bottomWorldX = bottomWorldX;
        _topWorldX = topWorldX;
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;

        _zSpan = SafeNonZero(_rightWorldZ - _leftWorldZ);
        _xSpan = SafeNonZero(_bottomWorldX - _topWorldX);
    }

    public (float u, float v) WorldToUV(float worldX, float worldZ)
    {
        float u = (worldZ - _leftWorldZ) / _zSpan;
        float v = (_bottomWorldX - worldX) / _xSpan;
        return (u, v);
    }

    public (float offsetX, float offsetY) UVToCenteredOffset(float u, float v)
    {
        float offsetX = (u - 0.5f) * _mapWidth;
        float offsetY = (v - 0.5f) * _mapHeight;
        return (offsetX, offsetY);
    }

    public (float offsetX, float offsetY) WorldToCenteredOffset(float worldX, float worldZ)
    {
        var uv = WorldToUV(worldX, worldZ);
        return UVToCenteredOffset(uv.u, uv.v);
    }

    private static float SafeNonZero(float value)
    {
        const float minAbs = 0.0001f;
        if (MathF.Abs(value) < minAbs)
            return value >= 0f ? minAbs : -minAbs;

        return value;
    }
}
