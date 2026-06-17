using System;

namespace EscapeBlock9.ProcGen.Data
{
    public enum TileCategory
    {
        Room,
        Corridor,
        Stair,
        Connector,
        Exit,
        Special,
        Portal,
        Utility
    }

    public enum ConnectorKind
    {
        None,
        Door,
        OpenFrame,
        CorridorJoin,
        Stair,
        FireExit,
        Portal,
        Sealed
    }

    [Flags]
    public enum AllowedYawRotations
    {
        OnlyAuthored = 1,
        Yaw90 = 2,
        Yaw180 = 4,
        Yaw270 = 8,
        AnyRightAngle = Yaw90 | Yaw180 | Yaw270
    }

    public enum SpawnMarkerKind
    {
        PlayerStart,
        Loot,
        Objective,
        Enemy,
        PatrolPath,
        Light,
        Audio,
        Hazard,
        Exit,
        Debug
    }

    public enum TileAuthoringSeverity
    {
        Info,
        Warning,
        Error
    }
}
