# Repository Guidelines

## Project Structure & Module Organization
- `VacationTracker.sln` is the solution root; the main Blazor Server app lives in `VacationTracker/`.
- `VacationTracker/Program.cs` wires up dependency injection, middleware, and the app host.
- Razor UI lives in `VacationTracker/Components/` with `Layout/`, `Pages/`, and `Shared/` subfolders.
- Data access is in `VacationTracker/Data/` with entities under `VacationTracker/Data/Entities/`.
- Application services live in `VacationTracker/Services/` (e.g., `IUserService`, `IVacationService`).
- Custom auth lives in `VacationTracker/Auth/`; static assets are in `VacationTracker/wwwroot/`.
- Local SQLite files are `VacationTracker/vacation.db` and `VacationTracker/vacation.db-*` (delete to reset local data).

## Build, Test, and Development Commands
- `dotnet restore` restores NuGet packages.
- `dotnet build VacationTracker.sln` builds the solution.
- `dotnet run --project VacationTracker` runs the app locally.
- `dotnet test` runs tests once a test project exists.

## Coding Style & Naming Conventions
- Use 4-space indentation and file-scoped namespaces (e.g., `namespace VacationTracker.Services;`).
- Prefer `var` for obvious types; use `async`/`await` for `Task`-returning methods.
- Use PascalCase for types and components (`VacationRequest`, `VacationRequests.razor`).
- Prefix interfaces with `I` (`IUserService`); use camelCase for locals and parameters.

## Testing Guidelines
- No test projects are present yet. If adding tests, create a `*Tests` project and add it to the solution.
- Use descriptive test names, e.g., `GetUserByEmailAsync_ReturnsUser`.
- Run all tests with `dotnet test`.

## Commit & Pull Request Guidelines
- No Git history is available here, so use short, imperative commit messages (e.g., `Add vacation approval flow`).
- PRs should include a clear description, testing notes (commands run), and screenshots for UI changes.

## Configuration & Data
- Configure connection strings in `VacationTracker/appsettings.json`; use `VacationTracker/appsettings.Development.json` for local overrides.
- Avoid committing secrets; prefer user secrets or environment variables.

## Subagents
- Use subagents when parallel research or exploration would speed up delivery (e.g., scanning multiple files/areas or comparing approaches).
- Keep subagent scopes small and focused; consolidate findings before making edits.
