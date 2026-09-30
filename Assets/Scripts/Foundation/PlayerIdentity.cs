using FishNet.Connection;
using Steamworks;
using Steamworks.Data;

public static class PlayerIdentity
{
    public static int GetLocalClientId(NetworkConnection connection)
    {
        return connection == null ? -1 : connection.ClientId;
    }

    // Keep the existing order: lobby member, valid local Steam identity, numeric fallback.
    public static string ResolvePlayerName(string steamId, int clientId, Lobby? lobby)
    {
        if (!string.IsNullOrWhiteSpace(steamId) && lobby.HasValue)
        {
            foreach (Friend member in lobby.Value.Members)
            {
                if (member.Id.Value.ToString() == steamId)
                    return member.Name;
            }
        }

        if (SteamClient.IsValid && SteamClient.SteamId.Value.ToString() == steamId)
            return SteamClient.Name;

        return $"Player {clientId}";
    }
}