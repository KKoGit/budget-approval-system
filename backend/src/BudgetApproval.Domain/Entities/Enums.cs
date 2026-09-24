namespace BudgetApproval.Domain.Entities;

public enum RequestStatus
{
    Draft = 0,
    Submitted = 1,
    ReturnedForRevision = 2,
    Approved = 3,
    Rejected = 4
}

public enum BudgetCategory
{
    Personnel = 0,
    Equipment = 1,
    Software = 2,
    Training = 3,
    Travel = 4,
    Facilities = 5,
    ProfessionalServices = 6
}

public enum AuditAction
{
    Created = 0,
    Updated = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    ReturnedForRevision = 5
}

[Flags]
public enum UserRole
{
    None = 0,
    Requester = 1,
    Approver = 2
}
