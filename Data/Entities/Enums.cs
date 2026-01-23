namespace VacationTracker.Data.Entities;

public enum Role
{
    Dispatcher = 0,
    Supervisor = 1
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
