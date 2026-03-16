using System.Collections.Concurrent;
using MMS.Models.Matchmaking;

namespace MMS.Services.Matchmaking;

/// <summary>
/// Thread-safe in-memory store for active join sessions and discovery tokens.
/// Individual operations are atomic; callers own higher-level consistency
/// (e.g. removing a session and its token together).
/// </summary>
public sealed class JoinSessionStore {
    private readonly ConcurrentDictionary<string, JoinSession> _joinSessions = new();
    private readonly ConcurrentDictionary<string, DiscoveryTokenMetadata> _discoveryMetadata = new();

    /// <summary>Adds or replaces the session keyed by <see cref="JoinSession.JoinId"/>.</summary>
    public void Add(JoinSession session) => _joinSessions[session.JoinId] = session;

    /// <summary>Attempts to retrieve a session by its join identifier.</summary>
    public bool TryGet(string joinId, out JoinSession? session) =>
        _joinSessions.TryGetValue(joinId, out session);

    /// <summary>Removes a session and returns it. Returns <see langword="false"/> if not found.</summary>
    public bool Remove(string joinId, out JoinSession? session) =>
        _joinSessions.TryRemove(joinId, out session);

    /// <summary>Returns the join identifiers for all sessions belonging to the given lobby.</summary>
    public IReadOnlyList<string> GetJoinIdsForLobby(string lobbyConnectionData) =>
        _joinSessions.Values
                     .Where(s => s.LobbyConnectionData == lobbyConnectionData)
                     .Select(s => s.JoinId)
                     .ToList();

    /// <summary>Returns the join identifiers of all sessions expired before <paramref name="nowUtc"/>.</summary>
    public IReadOnlyList<string> GetExpiredJoinIds(DateTime nowUtc) =>
        _joinSessions.Where(kvp => kvp.Value.ExpiresAtUtc < nowUtc)
                     .Select(kvp => kvp.Key)
                     .ToList();

    /// <summary>Inserts or replaces the metadata associated with a discovery token.</summary>
    public void UpsertDiscoveryToken(string token, DiscoveryTokenMetadata metadata) =>
        _discoveryMetadata[token] = metadata;

    /// <summary>Returns <see langword="true"/> if the discovery token is currently registered.</summary>
    public bool ContainsDiscoveryToken(string token) => _discoveryMetadata.ContainsKey(token);

    /// <summary>Attempts to retrieve the metadata for a discovery token.</summary>
    public bool TryGetDiscoveryMetadata(string token, out DiscoveryTokenMetadata? metadata) =>
        _discoveryMetadata.TryGetValue(token, out metadata);

    /// <summary>
    /// Returns the discovered port for a token, or <see langword="null"/> if the token
    /// is unknown or its port has not yet been recorded.
    /// </summary>
    public int? GetDiscoveredPort(string token) =>
        _discoveryMetadata.TryGetValue(token, out var metadata) ? metadata.DiscoveredPort : null;

    /// <summary>Removes a discovery token and its metadata.</summary>
    public void RemoveDiscoveryToken(string token) =>
        _discoveryMetadata.TryRemove(token, out _);

    /// <summary>
    /// Returns tokens created before <paramref name="cutoffUtc"/>.
    /// Used during periodic cleanup to evict stale tokens no longer tied to an active session.
    /// </summary>
    public IReadOnlyList<string> GetExpiredDiscoveryTokens(DateTime cutoffUtc) =>
        _discoveryMetadata.Where(kvp => kvp.Value.CreatedAt < cutoffUtc)
                          .Select(kvp => kvp.Key)
                          .ToList();
}
