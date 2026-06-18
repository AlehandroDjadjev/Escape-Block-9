using System;
using UnityEngine;

[Serializable]
public sealed class MultiplayerUserDto
{
    public int id;
    public string username;
}

[Serializable]
public sealed class MultiplayerTokenResponseDto
{
    public string token;
    public MultiplayerUserDto user;
}

[Serializable]
public sealed class MultiplayerLobbyMemberDto
{
    public int userId;
    public string username;
    public string playerId;
    public int slot;
    public bool isReady;
    public string joined_at;
}

[Serializable]
public sealed class MultiplayerLobbyDto
{
    public int id;
    public string code;
    public int hostId;
    public int maxPlayers;
    public bool isStarted;
    public string created_at;
    public MultiplayerLobbyMemberDto[] members;
}

[Serializable]
public sealed class MultiplayerJoinLobbyResponseDto
{
    public MultiplayerLobbyDto lobby;
    public MultiplayerLobbyMemberDto member;
}

[Serializable]
public sealed class MultiplayerCreateLobbyRequestDto
{
    public int maxPlayers = 2;
}

[Serializable]
public sealed class MultiplayerReadyRequestDto
{
    public bool isReady;
}

[Serializable]
public sealed class MultiplayerStartLobbyRequestDto
{
    public string mapId = "escape_block_9";
}

[Serializable]
public sealed class MultiplayerSocketTypeEnvelopeDto
{
    public string type;
}

[Serializable]
public sealed class MultiplayerPingDto
{
    public string type = "ping";
    public double clientTime;
}

[Serializable]
public sealed class MultiplayerHeartbeatDto
{
    public string type = "heartbeat";
    public double clientTime;
}

[Serializable]
public sealed class MultiplayerPongDto
{
    public string type;
    public double clientTime;
    public double serverTime;
}

[Serializable]
public sealed class MultiplayerLobbySnapshotDto
{
    public string type;
    public int lobbyId;
    public string code;
    public int hostId;
    public bool isStarted;
    public MultiplayerLobbyMemberDto[] players;
}

[Serializable]
public sealed class MultiplayerLobbyEventDto
{
    public string type;
    public int lobbyId;
    public string playerId;
    public int userId;
    public int slot;
    public bool isReady;
}

[Serializable]
public sealed class MultiplayerGameStartedDto
{
    public string type;
    public int lobbyId;
    public string mapId;
    public MultiplayerGameStartedPlayerDto[] players;
}

[Serializable]
public sealed class MultiplayerGameStartedPlayerDto
{
    public string playerId;
    public int userId;
    public int slot;
}

[Serializable]
public sealed class MultiplayerPlayerStateDto
{
    public string type = "player_state";
    public string playerId;
    public int userId;
    public int seq;
    public double clientTime;
    public double serverTime;
    public float[] position;
    public float[] rotation;
    public float[] velocity;
    public string animationState = "idle";

    public static MultiplayerPlayerStateDto FromTransform(string playerId, int userId, int seq, Transform target, Vector3 velocity)
    {
        return new MultiplayerPlayerStateDto
        {
            type = "player_state",
            playerId = playerId,
            userId = userId,
            seq = seq,
            clientTime = Time.realtimeSinceStartupAsDouble,
            position = MultiplayerJson.VectorToArray(target != null ? target.position : Vector3.zero),
            rotation = MultiplayerJson.VectorToArray(target != null ? target.eulerAngles : Vector3.zero),
            velocity = MultiplayerJson.VectorToArray(velocity),
            animationState = velocity.sqrMagnitude > 0.01f ? "run" : "idle"
        };
    }
}

[Serializable]
public sealed class MultiplayerRoomSnapshotDto
{
    public string type;
    public int lobbyId;
    public MultiplayerPlayerStateDto[] players;
}

[Serializable]
public sealed class MultiplayerErrorDetailDto
{
    public string detail;
}

// Sent by each player at the end of the pre-game setup phase. The Key Hider
// fills in keyPosition (and leaves teacherPositions empty); the Teacher Placer
// will fill in teacherPositions (and leave keyPosition empty) once chunk 2b
// lands. The receiving side stores whatever fields are populated. Positions are
// flat float arrays so JsonUtility serializes them cleanly.
[Serializable]
public sealed class MultiplayerSetupPlacementDto
{
    public string type = "setup_placement";
    public string playerId;
    public bool isKeyHider;
    public float[] keyPosition;        // length 3, world space, null if not the hider
    public float[] teacherPositionsX;  // teachers[i] = (x[i], y[i], z[i])
    public float[] teacherPositionsY;
    public float[] teacherPositionsZ;
}

public readonly struct MultiplayerApiResult<T>
{
    public readonly bool IsSuccess;
    public readonly T Value;
    public readonly string Error;

    private MultiplayerApiResult(bool isSuccess, T value, string error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static MultiplayerApiResult<T> Success(T value)
    {
        return new MultiplayerApiResult<T>(true, value, null);
    }

    public static MultiplayerApiResult<T> Failure(string error)
    {
        return new MultiplayerApiResult<T>(false, default, error);
    }
}

public static class MultiplayerJson
{
    public static float[] VectorToArray(Vector3 value)
    {
        return new[] { value.x, value.y, value.z };
    }

    public static Vector3 ArrayToVector(float[] value)
    {
        if (value == null || value.Length < 3)
        {
            return Vector3.zero;
        }

        return new Vector3(value[0], value[1], value[2]);
    }

    public static string ExtractError(string json, string fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            MultiplayerErrorDetailDto error = JsonUtility.FromJson<MultiplayerErrorDetailDto>(json);
            if (!string.IsNullOrWhiteSpace(error.detail))
            {
                return error.detail;
            }
        }
        catch (ArgumentException)
        {
        }

        return $"{fallback}: {json}";
    }

    public static int DeterministicHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                char c = char.ToLowerInvariant(text[i]);
                hash ^= c;
                hash *= 16777619u;
            }

            return (int)(hash & 0x7fffffff);
        }
    }
}
