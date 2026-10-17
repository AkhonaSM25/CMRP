# CMRP - Campus Maintenance Reporting Platform

Software Engineering 2 (ENEL3SF), University of KwaZulu-Natal. Students and staff report campus maintenance problems;
facilities staff triage, assign, fix and track them until closed.

**Team:** Mokgoadi Gustave Saboshego, Akhona Mkhwanazi (Technical Lead / Integrator), Kakhele Hadebe, Nkosinathi Tsvuura

## Features
- Four roles: Reporter, Technician, Coordinator, Administrator (role comes from the account)
- Report an incident with type, building, description and an optional PNG/JPG photo (max 5 MB)
- Reference numbers (`CMR-YYYY-NNNN`), status lifecycle, priority, assignment and public/internal notes
- Full audit trail of status changes and assignments
- Dashboard and analytics for coordinators and administrators
- Administrator screen to create accounts, change roles and deactivate accounts

## Tech stack
ASP.NET Core MVC (.NET 10), Razor + Bootstrap 5, Entity Framework Core (SQLite), ASP.NET Core Identity, xUnit.

## Run it
1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. From the repository root: `dotnet run` (or press F5 in Visual Studio).
3. Open the address printed in the console and sign in with a demo account (password `Demo@12345`):

| Role | Email |
|------|-------|
| Reporter | reporter@cmrp.test |
| Technician | technician@cmrp.test |
| Coordinator | coordinator@cmrp.test |
| Administrator | admin@cmrp.test |

The database is created and seeded automatically on first start. **Demo accounts use a known password: set
`Seed:DemoAccounts` and `Seed:DemoReports` to `false` in `appsettings.json` for any real deployment.**

## Test it
```
dotnet test CMRP.slnx            # run all tests
.\run-tests.ps1                  # run all tests + coverage report (PowerShell)
```
The `CMRP.Tests` project holds the unit and integration tests (workflow rules, access control, photo validation,
dashboard calculations, report creation, assignment, notes, database rules and seeding). Integration tests use a
throw-away in-memory SQLite database. GitHub Actions runs the build and tests on every push (`.github/workflows/ci.yml`).

## Project layout
| Folder | Contents |
|--------|----------|
| `Controllers/` | MVC controllers (reports, facilities, admin, account) |
| `Services/` | Business rules: workflow, reports, attachments, dashboard, access |
| `Models/`, `ViewModels/` | Entities and form/page models |
| `Data/` | EF Core context and seed routine |
| `Views/`, `wwwroot/` | Razor pages and static files |
| `CMRP.Tests/` | xUnit test project |

See `README-INSTALL.md` for the original install notes.
