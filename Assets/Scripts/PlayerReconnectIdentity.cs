using System;
using UnityEngine;

public static class PlayerReconnectIdentity
{
    private const string PlayerIdKey = "PLAYER_RECONNECT_ID";

    private static Guid playerId;

    public static byte[] ConnectionToken
    {
        get
        {
            if (playerId == Guid.Empty)
            {
                string savedId = PlayerPrefs.GetString(PlayerIdKey);

                if (Guid.TryParse(savedId, out Guid parsedId))
                {
                    playerId = parsedId;
                }
                else
                {
                    playerId = Guid.NewGuid();

                    PlayerPrefs.SetString(PlayerIdKey, playerId.ToString());
                    PlayerPrefs.Save();
                }
            }

            return playerId.ToByteArray();
        }
    }
}
