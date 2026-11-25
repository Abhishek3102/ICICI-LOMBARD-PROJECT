namespace ClaimGuard.Domain.Enums;

/// <summary>Stored as VARCHAR in the claim_audit_logs.actor_type column.</summary>
public enum ActorType
{
    SystemAi = 0,
    Adjudicator = 1,
    Partner = 2,
    Policyholder = 3
}
