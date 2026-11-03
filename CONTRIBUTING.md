# Contributing

## Workflow

1. Pick a task in Jira and move it to **Devam Ediyor** (In Progress).
2. Update `main` and create a branch named after the Jira key:
   ```bash
   git checkout main
   git pull origin main
   git checkout -b SCRUM-XX-short-name
   ```
3. Commit with the Jira key at the start of the message:
   ```bash
   git commit -m "SCRUM-XX Short description of the change"
   ```
4. Before opening a PR, bring in the latest `main` and make sure everything passes:
   ```bash
   git pull origin main
   dotnet build
   dotnet test
   ```
5. Push and open a pull request into `main`. Fill in the PR template.
6. Move the Jira task to **İncelemede** (In Review).
7. At least **1 teammate must approve** the PR before it is merged.
8. After the merge, move the Jira task to **Tamam** (Done).

## Rules

- Never push directly to `main`; `main` is protected and only accepts pull requests.
- Never commit secrets (passwords, tokens, connection strings). Use environment variables or user secrets.
- Keep PRs small: one Jira task per PR.
- Report bugs in Jira as issues with the `bug` label and link them to the related task.
