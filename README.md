# Script Runner

A Windows desktop application for extracting data from SQL DATABASES
records and exporting it to Excel or PDF. Built on .NET 10 with a WPF frontend,
SQL Server backend, and support for scheduled sql query batch exports.

## What it does

- Users log in with a username, password, and department.
- Each user sees the reports (scripts) relevant to their department.
- Running a script executes a read-only SQL query against the
  database and writes the result to an Excel file in a folder of the
  user's choice.
- Admins can manage users, assign scripts to departments, reset passwords,
  view a full audit trail of every action, and export dashboards as PDF.


## Technology Stack

| Layer | Technology |
|---|---|
| Frontend | WPF (.NET 10) |
| Language | C# |
| Data Access | Dapper |
| Database | Microsoft SQL Server |
| Excel | ClosedXML |
| PDF | QuestPDF |
| Charts | LiveCharts2 (SkiaSharp) |
| Password Hashing | BCrypt.Net-Next |
| Logging | Serilog |
| Validation | FluentValidation |
| Resilience | Polly |
| Installer | Inno Setup |
