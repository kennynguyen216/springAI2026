using Microsoft.Agents.AI;

public class AgentSessionManager
{
    public Dictionary<string, Microsoft.Agents.AI.AgentSession> Sessions { get; } = new();
}