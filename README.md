# Vacation Tracker

A Blazor Server app for tracking vacation requests and approvals.

## Quick Start
- Restore packages: `dotnet restore`
- Build: `dotnet build VacationTracker.sln`
- Run: `dotnet run --project VacationTracker`

## Project Layout
- `VacationTracker/Program.cs` bootstraps the app and DI.
- `VacationTracker/Components/` contains Razor UI (`Layout/`, `Pages/`, `Shared/`).
- `VacationTracker/Data/` contains the EF Core context and entities.
- `VacationTracker/Services/` houses application services.
- `VacationTracker/wwwroot/` holds static assets.

## Configuration
- Connection strings live in `VacationTracker/appsettings.json`.
- Use `VacationTracker/appsettings.Development.json` for local overrides.

## Testing
No test project exists yet. Add a `*Tests` project and run `dotnet test`.
