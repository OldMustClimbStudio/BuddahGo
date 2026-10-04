using BuddahGo.Match;
using FishNet.Connection;
using Steamworks;
using Steamworks.Data;

public static class PlayerIdentity
{
    public static string FallbackName(int clientId) => MatchRules.Current.DefaultPlayerName ?? $"Player {clientId}";

    public static string GetSteamIdForConnection(FishNet.Transporting.Transport transport, NetworkConnection connection)
    {
        if (transport == null || connection == null)
            return string.Empty;

        string address = transport.GetConnectionAddress(connection.ClientId);
        return string.IsNullOrWhiteSpace(address) ? string.Empty : address;
    }
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

        if (MatchRules.Current.DefaultPlayerName != null && SteamClient.IsValid)
            return SteamClient.Name;

        return FallbackName(clientId);
    }
}