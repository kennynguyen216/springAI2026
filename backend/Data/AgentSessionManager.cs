using Microsoft.Agents.AI;

/// <summary>
/// Stores active in-memory agent sessions keyed by conversation identifier.
/// </summary>
public class AgentSessionManager
{
    /// <summary>
    /// Gets the currently tracked agent sessions.
    /// </summary>
    public Dictionary<string, Microsoft.Agents.AI.AgentSession> Sessions { get; } = new();

    /// <summary>
    /// Gets an existing agent session or creates a new one when needed.
    /// </summary>
    /// <param name="sessionKey">The session lookup key.</param>
    /// <param name="agent">The agent that owns the session.</param>
    /// <returns>The existing or newly created session.</returns>
    public async Task<Microsoft.Agents.AI.AgentSession> GetOrCreateSessionAsync(string sessionKey, AIAgent agent)
    {
        if (!Sessions.TryGetValue(sessionKey, out var session))
        {
            session = await agent.CreateSessionAsync();
            Sessions[sessionKey] = session;
        }

        return session;
    }
}
