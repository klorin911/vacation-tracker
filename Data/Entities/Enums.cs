namespace VacationTracker.Data.Entities;

public enum Role
{
    Employee = 0,
    Admin = 1,
    Dispatcher = 2
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
