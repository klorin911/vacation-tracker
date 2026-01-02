namespace VacationTracker.Data.Entities;

public enum Role
{
    User,
    Admin
}

public enum Status
{
    Pending,
    Approved,
    Rejected
}

public enum RequestType
{
    Vacation,
    Sick,
    Personal,
    Other
}
