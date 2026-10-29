# CS 308 Online Store

Online store for phones and tablets, built for the Sabancı University CS 308 course project.

## Tech stack

- Backend: C# with ASP.NET Core (.NET 8)
- Front end: plain HTML, CSS and JavaScript served from `wwwroot`
- Database: MySQL (Entity Framework Core)
- Version control: Git and GitHub; planning in Jira (Scrum)

## Project structure

```
OnlineStore/          ASP.NET Core project
  Program.cs          application entry point
  wwwroot/            static front end files
```

## Getting started

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Clone the repository and open it in VS Code.
3. Run the app:

   ```
   cd OnlineStore
   dotnet run
   ```

4. Open the address printed in the terminal (for example `http://localhost:5xxx`).
   The home page is served from `wwwroot`, and `/api/health` returns `{"status":"ok"}`.

## Team workflow

- Work is tracked in Jira (project SCRUM) in two-week sprints.
- Each task is developed on its own branch and merged into `main` through a pull request.
- Commit messages are short and describe the change, for example `Add product list API`.
