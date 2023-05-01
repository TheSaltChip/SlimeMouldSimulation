using UnityEngine;

public struct Agent
{
    public Vector2 Position;
    public float Angle;
    public Vector3Int SpeciesMask;
    private int _unusedSpeciesChannel;
    public int SpeciesIndex;

    public override string ToString()
    {
        return $"{nameof(Position)}: {Position}, {nameof(Angle)}: {Angle}";
    }
}