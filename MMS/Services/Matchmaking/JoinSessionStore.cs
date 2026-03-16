using System.Collections.Concurrent;
using MMS.Models.Matchmaking;

namespace MMS.Services.Matchmaking;

/// <summary>
/// Thread-safe in-memory store for active join sessions and discovery tokens.
/// </summary>
/// <remarks>
/// Both dictionaries use <see cref="ConcurrentDictionary{TKey,TValue}"/> so individual
/// operations are atomic. Callers are responsible for higher-level consistency
/// (e.g. removing a session and its token together).
/// </remarks>
public sealed class JoinSessionStore {
    private readonly ConcurrentDictionary<string, JoinSession> _joinSessions = new();
    private readonly ConcurrentDictionary<string, DiscoveryTokenMetadata> _discoveryMetadata = new();

    /// <summary>Adds or replaces the session keyed by <see cref="JoinSession.JoinId"/>.</summary>
    /// <param name="session">The session to store.</param>
    public void Add(JoinSession session) => _joinSessions[session.JoinId] = session;

    /// <summary>Attempts to retrieve a session by its join identifier.</summary>
    /// <param name="joinId">The join session identifier.</param>
    /// <param name="session">The session if found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the session exists.</returns>
    public bool TryGet(string joinId, out JoinSession? session) =>
        _joinSessions.TryGetValue(joinId, out session);

    /// <summary>Removes a session and returns it.</summary>
    /// <param name="joinId">The join session identifier to remove.</param>
    /// <param name="session">The removed session, or <see langword="null"/> if not found.</param>
    /// <returns><see langword="true"/> if the session was present and removed.</returns>
    public bool Remove(string joinId, out JoinSession? session) =>
        _joinSessions.TryRemove(joinId, out session);

    /// <summary>
    /// Returns the join identifiers for all sessions belonging to the given lobby.
    /// </summary>
    /// <param name="lobbyConnectionData">The lobby's connection data string used as a key.</param>
    public IReadOnlyList<string> GetJoinIdsForLobby(string lobbyConnectionData) =>
        _joinSessions.Values
                     .Where(s => s.LobbyConnectionData == lobbyConnectionData)
                     .Select(s => s.JoinId)
                     .ToList();

    /// <summary>Returns the join identifiers of all sessions whose expiry is before <paramref name="nowUtc"/>.</summary>
    /// <param name="nowUtc">The current UTC time used as the expiry threshold.</param>
    public IReadOnlyList<string> GetExpiredJoinIds(DateTime nowUtc) =>
        _joinSessions.Where(kvp => kvp.Value.ExpiresAtUtc < nowUtc)
                     .Select(kvp => kvp.Key)
                     .ToList();

    /// <summary>Inserts or replaces the metadata associated with a discovery token.</summary>
    /// <param name="token">The discovery token string.</param>
    /// <param name="metadata">Metadata to associate with the token.</param>
    public void UpsertDiscoveryToken(string token, DiscoveryTokenMetadata metadata) =>
        _discoveryMetadata[token] = metadata;

    /// <summary>Returns <see langword="true"/> if the discovery token is currently registered.</summary>
    /// <param name="token">The discovery token to check.</param>
    public bool ContainsDiscoveryToken(string token) => _discoveryMetadata.ContainsKey(token);

    /// <summary>Attempts to retrieve the metadata for a discovery token.</summary>
    /// <param name="token">The discovery token to look up.</param>
    /// <param name="metadata">The associated metadata if found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the token exists.</returns>
    public bool TryGetDiscoveryMetadata(string token, out DiscoveryTokenMetadata? metadata) =>
        _discoveryMetadata.TryGetValue(token, out metadata);

    /// <summary>
    /// Returns the discovered port for a token, or <see langword="null"/> if the token
    /// is unknown or its port has not yet been recorded.
    /// </summary>
    /// <param name="token">The discovery token to query.</param>
    public int? GetDiscoveredPort(string token) =>
        _discoveryMetadata.TryGetValue(token, out var metadata) ? metadata.DiscoveredPort : null;

    /// <summary>Removes a discovery token and its metadata.</summary>
    /// <param name="token">The discovery token to remove.</param>
    /// <returns><see langword="true"/> if the token was present and removed.</returns>
    public void RemoveDiscoveryToken(string token) =>
        _discoveryMetadata.TryRemove(token, out _);

    /// <summary>
    /// Returns the tokens whose metadata was created before <paramref name="cutoffUtc"/>.
    /// Used during periodic cleanup to evict stale tokens that are no longer tied to an active session.
    /// </summary>
    /// <param name="cutoffUtc">Tokens created before this time are considered expired.</param>
    public IReadOnlyList<string> GetExpiredDiscoveryTokens(DateTime cutoffUtc) =>
        _discoveryMetadata.Where(kvp => kvp.Value.CreatedAt < cutoffUtc)
                          .Select(kvp => kvp.Key)
                          .ToList();
}
