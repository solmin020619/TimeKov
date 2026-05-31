using System.Collections.Generic;
using UnityEngine;

public enum PortType
{
    Input,
    Output
}

public class BuildPort : MonoBehaviour
{
    /// <summary>씬에 활성화된 BuildPort 전체 목록. BeltSegment의 그리드 연결 감지에 사용.</summary>
    public static readonly List<BuildPort> All = new();

    private void OnEnable()  => All.Add(this);
    private void OnDisable() => All.Remove(this);

    [Header("Port Type")]
    public PortType portType = PortType.Input;

    [Header("Grid (Prefab Local)")]
    public Vector2Int localCellOffset = Vector2Int.zero;

    public Vector2Int localDirection = Vector2Int.right;

    [Header("Connection")]
    public int maxConnections = 1;
    public int connectionCount = 0;

    private PlacedBuilding cachedBuilding;

    public PlacedBuilding OwnerBuilding
    {
        get
        {
            if (cachedBuilding == null)
                cachedBuilding = GetComponentInParent<PlacedBuilding>();

            return cachedBuilding;
        }
    }

    public bool HasCapacity => connectionCount < maxConnections;

    public bool CanStartConnection()
    {
        return HasCapacity && portType == PortType.Output;
    }

    public bool CanEndConnection()
    {
        return HasCapacity && portType == PortType.Input;
    }

    public void AddConnection()
    {
        connectionCount++;
    }

    public void RemoveConnection()
    {
        connectionCount = Mathf.Max(0, connectionCount - 1);
    }

    public Vector2Int GetWorldDirection()
    {
        if (OwnerBuilding == null)
            return localDirection;

        Vector2Int dir = localDirection;
        int rot = NormalizeRotation(OwnerBuilding.transform.eulerAngles.y);

        switch (rot)
        {
            case 90:
                return new Vector2Int(dir.y, -dir.x);

            case 180:
                return new Vector2Int(-dir.x, -dir.y);

            case 270:
                return new Vector2Int(-dir.y, dir.x);

            default:
                return dir;
        }
    }

    public Vector2Int GetWorldCell()
    {
        if (OwnerBuilding == null)
            return Vector2Int.zero;

        Vector2Int size = GetOwnerSize();

        Vector2Int offsetFromStart = CenterToStartOffset(localCellOffset, size);

        int rot = NormalizeRotation(OwnerBuilding.transform.eulerAngles.y);
        Vector2Int rotatedOffset = RotateOffsetInFootprint(offsetFromStart, size, rot);

        Vector2Int worldCell = OwnerBuilding.originCell + rotatedOffset;

        return worldCell;
    }

    public Vector2Int GetFrontCell()
    {
        Vector2Int worldCell = GetWorldCell();
        Vector2Int worldDir = GetWorldDirection();
        Vector2Int frontCell = worldCell + worldDir;

        return frontCell;
    }

    private Vector2Int GetOwnerSize()
    {
        if (OwnerBuilding == null || OwnerBuilding.occupiedCells == null || OwnerBuilding.occupiedCells.Count == 0)
            return Vector2Int.one;

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;

        for (int i = 0; i < OwnerBuilding.occupiedCells.Count; i++)
        {
            Vector2Int c = OwnerBuilding.occupiedCells[i];

            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.y > maxY) maxY = c.y;
        }

        return new Vector2Int(maxX - minX + 1, maxY - minY + 1);
    }

    private Vector2Int CenterToStartOffset(Vector2Int centerOffset, Vector2Int size)
    {
        return new Vector2Int(
            centerOffset.x + (size.x / 2),
            centerOffset.y + (size.y / 2)
        );
    }

    private Vector2Int RotateOffsetInFootprint(Vector2Int offset, Vector2Int size, int rot)
    {
        switch (rot)
        {
            case 90:
                return new Vector2Int(offset.y, size.x - 1 - offset.x);

            case 180:
                return new Vector2Int(size.x - 1 - offset.x, size.y - 1 - offset.y);

            case 270:
                return new Vector2Int(size.y - 1 - offset.y, offset.x);

            default:
                return offset;
        }
    }

    private int NormalizeRotation(float y)
    {
        int rot = Mathf.RoundToInt(y) % 360;
        if (rot < 0)
            rot += 360;
        return rot;
    }
}
