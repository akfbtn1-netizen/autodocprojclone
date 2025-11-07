
using System.Linq.Expressions;
using Enterprise.Documentation.Core.Domain.Entities;

namespace Enterprise.Documentation.Core.Domain.Specifications;

/// <summary>
/// Specification for agents that are currently online and available.
/// </summary>
public class AvailableAgentsSpecification : Specification<Agent>
{
    public override Expression<Func<Agent, bool>> ToExpression()
    {
        return agent => agent.Status == AgentStatus.Online && agent.IsAvailable && !agent.IsDeleted;
    }
}

/// <summary>
/// Specification for agents with specific capabilities.
/// </summary>
public class AgentsWithCapabilitySpecification : Specification<Agent>
{
    private readonly AgentCapability _capability;

    public AgentsWithCapabilitySpecification(AgentCapability capability)
    {
        _capability = capability;
    }

    public override Expression<Func<Agent, bool>> ToExpression()
    {
        return agent => agent.Capabilities.Contains(_capability);
    }

    public override bool IsSatisfiedBy(Agent entity)
    {
        return entity.HasCapability(_capability);
    }
}

/// <summary>
/// Specification for agents that can handle a specific security clearance level.
/// </summary>
public class AgentsWithSecurityClearanceSpecification : Specification<Agent>
{
    private readonly SecurityClearanceLevel _requiredClearance;

    public AgentsWithSecurityClearanceSpecification(SecurityClearanceLevel requiredClearance)
    {
        _requiredClearance = requiredClearance;
    }

    public override Expression<Func<Agent, bool>> ToExpression()
    {
        return agent => agent.MaxSecurityClearance >= _requiredClearance;
    }

    public override bool IsSatisfiedBy(Agent entity)
    {
        return entity.CanHandleSecurityLevel(_requiredClearance);
    }
}

/// <summary>
/// Specification for agents of a specific type.
/// </summary>
public class AgentsByTypeSpecification : Specification<Agent>
{
    private readonly AgentType _agentType;

    public AgentsByTypeSpecification(AgentType agentType)
    {
        _agentType = agentType;
    }

    public override Expression<Func<Agent, bool>> ToExpression()
    {
        return agent => agent.Type == _agentType;
    }
}

/// <summary>
/// Specification for agents with available capacity for new requests.
/// </summary>
public class AgentsWithCapacitySpecification : Specification<Agent>
{
    public override Expression<Func<Agent, bool>> ToExpression()
    {
        return agent => agent.IsAvailable && agent.ActiveRequestCount < agent.MaxConcurrentRequests;
    }
}

/// <summary>
/// Specification for high-performing agents based on success rate.
/// </summary>
public class HighPerformingAgentsSpecification : Specification<Agent>
{
    private readonly double _minimumSuccessRate;

    public HighPerformingAgentsSpecification(double minimumSuccessRate = 90.0)
    {
        _minimumSuccessRate = minimumSuccessRate;
    }

    public override Expression<Func<Agent, bool>> ToExpression()
    {
        return agent => agent.TotalRequestsProcessed > 10 && 
                       (agent.SuccessfulRequests * 100.0 / agent.TotalRequestsProcessed) >= _minimumSuccessRate;
    }

    public override bool IsSatisfiedBy(Agent entity)
    {
        return entity.TotalRequestsProcessed > 10 && entity.GetSuccessRate() >= _minimumSuccessRate;
    }
}

/// <summary>
/// Specification for agents that need health checks (haven't reported recently).
/// </summary>
public class AgentsNeedingHealthCheckSpecification : Specification<Agent>
{
    private readonly DateTime _cutoffTime;

    public AgentsNeedingHealthCheckSpecification(TimeSpan maxInterval)
    {
        _cutoffTime = DateTime.UtcNow - maxInterval;
    }

    public override Expression<Func<Agent, bool>> ToExpression()
    {
        return agent => agent.Status == AgentStatus.Online && 
                       (!agent.LastHealthCheckAt.HasValue || agent.LastHealthCheckAt.Value < _cutoffTime);
    }
}