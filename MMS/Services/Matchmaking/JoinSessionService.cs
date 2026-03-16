using System.Net.WebSockets;
using MMS.Models.Matchmaking;
using _Lobby = MMS.Models.Lobby.Lobby;

namespace MMS.Services.Matchmaking;

/// <summary>
/// Compatibility facade over <see cref="JoinSessionCoordinator"/> for the join-session
/// lifecycle and NAT hole-punch coordination.
/// </summary>
/// <remarks>
/// <para>
/// A join session represents a single client attempt to connect to a lobby host.
/// The typical flow is:
/// </para>
/// <list type="number">
///   <item>
///     Client calls <c>POST /lobby/{id}/join</c> ->
///     <see cref="CreateJoinSession"/> allocates a session and a client discovery token.
///   </item>
///   <item>
///     Client opens a WebSocket ->
///     <see cref="AttachJoinWebSocket"/> stores it on the session so the service can push events.
///   </item>
///   <item>
///     MMS receives the client's UDP discovery packet ->
///     <see cref="SetDiscoveredPortAsync"/> records the client's external port and sends a
///     <c>refresh_host_mapping</c> request to the host.
///   </item>
///   <item>
///     MMS receives the host's UDP discovery packet ->
///     <see cref="SetDiscoveredPortAsync"/> records the host's external port and coordinates
///     synchronized <c>start_punch</c> messages to both sides.
///   </item>
/// </list>
/// </remarks>
public class JoinSessionService(JoinSessionCoordinator coordinator) {
    /// <summary>
    /// Allocates a new join session for a client attempting to connect to <paramref name="lobby"/>.
    /// </summary>
    /// <param name="lobby">The target lobby. Steam lobbies return <see langword="null"/>.</param>
    /// <param name="clientIp">The joining client's IP address.</param>
    /// <returns>The new session, or <see langword="null"/> for Steam lobbies.</returns>
    public JoinSession? CreateJoinSession(_Lobby lobby, string clientIp) =>
        coordinator.CreateJoinSession(lobby, clientIp);

    /// <summary>
    /// Returns an active, non-expired session by its identifier.
    /// </summary>
    /// <param name="joinId">The join session identifier.</param>
    /// <returns>The session, or <see langword="null"/> if not found or expired.</returns>
    public JoinSession? GetJoinSession(string joinId) =>
        coordinator.GetJoinSession(joinId);

    /// <summary>
    /// Associates a WebSocket with an existing session so the server can push events to the client.
    /// </summary>
    /// <param name="joinId">The join session identifier.</param>
    /// <param name="webSocket">The client's WebSocket connection.</param>
    /// <returns><see langword="true"/> if the session was found and the socket was attached.</returns>
    public bool AttachJoinWebSocket(string joinId, WebSocket webSocket) =>
        coordinator.AttachJoinWebSocket(joinId, webSocket);

    /// <summary>
    /// Records the externally observed UDP port for a discovery token and advances the punch flow.
    /// </summary>
    /// <param name="token">The discovery token included in the UDP packet.</param>
    /// <param name="port">The external port observed by the server.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    public Task SetDiscoveredPortAsync(string token, int port, CancellationToken cancellationToken = default) =>
        coordinator.SetDiscoveredPortAsync(token, port, cancellationToken);

    /// <summary>
    /// Returns the externally observed UDP port for a discovery token, or
    /// <see langword="null"/> if not yet recorded.
    /// </summary>
    /// <param name="token">The discovery token to query.</param>
    public int? GetDiscoveredPort(string token) =>
        coordinator.GetDiscoveredPort(token);

    /// <summary>
    /// Sends the <c>begin_client_mapping</c> message to the client identified by <paramref name="joinId"/>.
    /// </summary>
    /// <param name="joinId">The join session identifier.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    public Task SendBeginClientMappingAsync(string joinId, CancellationToken cancellationToken) =>
        coordinator.SendBeginClientMappingAsync(joinId, cancellationToken);

    /// <summary>
    /// Asks the host to refresh its NAT mapping for the given join session.
    /// </summary>
    /// <param name="joinId">The join session identifier.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns><see langword="true"/> if the refresh message was dispatched successfully.</returns>
    public Task<bool> SendHostRefreshRequestAsync(string joinId, CancellationToken cancellationToken) =>
        coordinator.SendHostRefreshRequestAsync(joinId, cancellationToken);

    /// <summary>
    /// Fails a join session: notifies both the client and the host, then cleans up the session.
    /// </summary>
    /// <param name="joinId">The join session identifier.</param>
    /// <param name="reason">A short machine-readable failure reason (e.g. <c>"host_unreachable"</c>).</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    public Task FailJoinSessionAsync(string joinId, string reason, CancellationToken cancellationToken = default) =>
        coordinator.FailJoinSessionAsync(joinId, reason, cancellationToken);

    /// <summary>
    /// Removes all sessions that have passed their expiry time and purges stale discovery tokens.
    /// </summary>
    public void CleanupExpiredSessions() =>
        coordinator.CleanupExpiredSessions();

    /// <summary>
    /// Removes all sessions belonging to <paramref name="lobby"/> and its host discovery token.
    /// Called when a lobby is closed or evicted.
    /// </summary>
    /// <param name="lobby">The lobby being removed.</param>
    internal void CleanupSessionsForLobby(_Lobby lobby) =>
        coordinator.CleanupSessionsForLobby(lobby);
}
