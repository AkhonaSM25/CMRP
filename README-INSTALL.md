# CMRP - install into your existing Visual Studio project

These files replace the incremental Identity/roles/seed steps done earlier. They assume your project is the `CMRP`
ASP.NET Core MVC (.NET 10) project with SQLite and Individual Accounts.

## 1. Copy files
Copy these into your project root, overwriting when asked:

    Controllers/  Data/  Models/  Services/  ViewModels/  Views/  wwwroot/css/site.css  wwwroot/js/site.js
    Program.cs  appsettings.json

Do NOT delete `wwwroot/lib` (Bootstrap and jQuery from the template are used).
Optional tidy-up: delete `Views/Home/Privacy.cshtml`, `Views/Shared/_Layout.cshtml.css` and the `Areas/Identity` folder,
and uninstall the `Microsoft.AspNetCore.Identity.UI` NuGet package (CMRP now has its own login pages).

## 2. Reset the database (the model changed)
Delete the `Migrations` folder and the `cmrp.db*` files, then in Package Manager Console run:

    Add-Migration InitialCreate

Do not run Update-Database: the app migrates and seeds itself on start-up.

## 3. Ignore uploads
Add this line to `.gitignore`:  `App_Data/`

## 4. Run (F5) and sign in
| Role          | Email                   | Password    |
|---------------|-------------------------|-------------|
| Reporter      | reporter@cmrp.test      | Demo@12345  |
| Technician    | technician@cmrp.test    | Demo@12345  |
| Coordinator   | coordinator@cmrp.test   | Demo@12345  |
| Administrator | admin@cmrp.test         | Demo@12345  |

Also seeded: reporter2@ and technician2@ (same password) and 15 demo reports.
Turn demo data off in `appsettings.json` (`Seed:DemoAccounts`, `Seed:DemoReports`).

## 5. First smoke test (10 minutes)
1. Reporter: submit a report with a PNG photo. Confirm the reference number appears, then find it under My Reports and Track Progress.
2. Coordinator: open it from Complaints, assign a technician, set the priority.
3. Technician: Assigned Work -> Update Status -> In Progress -> Resolved (add a note).
4. Reporter: refresh the report. The timeline shows the public note but not internal ones.
5. Reporter: try to open someone else's report URL (`/Reports/Details/1`). Expect Access denied.
6. Administrator: Manage Users -> create a staff account, change a role, deactivate an account and confirm it cannot sign in.

## Design decisions to record in your Task 4 documentation
- Login has no role toggle: the role comes from the account. Self-registration always creates a Reporter.
- Reference format stays `CMR-YYYY-NNNN` (Task 3). Change `ReportService.ReferencePrefix` to switch to `INC`.
- Incident types replace the earlier "category" list; there is no separate title field (title = type + place).
- Status lifecycle follows Task 2. "Assigned" is only reachable through the Assign action.
- Photos are stored outside wwwroot (`App_Data/uploads`), validated by size, extension and file signature, and served only after an access check.
- Dashboard deltas compare the last 30 days with the 30 days before.
- Not built yet: map on the dashboard, notifications, reference-data admin screens, CSV export, unit tests.
- Seeded incident types and buildings are sample data. Replace them in `Data/SeedData.cs`.
