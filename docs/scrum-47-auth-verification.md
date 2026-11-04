# SCRUM-47 — Authentication Verification Report

**Date:** November 4, 2026  
**Jira Task:** SCRUM-47 — User registration and login  
**Branch:** `SCRUM-47-auth-integration`  
**Status:** Integration verification in progress

## 1. Objective

Verify that the existing authentication backend works correctly with the frontend login implementation and a real MySQL database, without implementing a duplicate authentication system.

## 2. Automated Test Results

Command:

```powershell
dotnet test .\OnlineStore.Tests\OnlineStore.Tests.csproj
```

Results:

| Result | Count |
|---|---:|
| Passed | 219 |
| Failed | 0 |
| Skipped | 12 |
| Total | 231 |

The 12 skipped tests require an explicitly configured real MySQL integration-test environment. They were not executed as part of this test run.

### New Tests Added for SCRUM-47

**Registration_DoesNotSignInUntilLogin**

- Verifies successful customer registration.
- Confirms registration does not automatically create a login session.
- Confirms login creates a valid authenticated session.
- Verifies `/api/auth/me` returns the correct account after login.

**CsrfToken_MustBeRefreshedAfterLogin**

- Verifies an anonymous CSRF token can be used for login.
- Confirms the previous token is rejected after authentication.
- Confirms the rejected logout attempt does not terminate the session.
- Verifies a newly fetched CSRF token permits logout.
- Confirms the session becomes unauthorized after logout.

Both tests passed and were committed to the SCRUM-47 branch.

Commits:
- `2c8bba7` — Test registration and login session flow
- `9e6b394` — Test CSRF token refresh after login

## 3. Local Integration Environment

- ASP.NET Core / .NET 8
- MySQL Server 26.7.0
- Local development database: `onlinestore`
- Dedicated database account: `cs308_dev`
- EF Core migrations applied successfully
- Application served at `http://localhost:5200`

Authentication credentials were stored locally using ASP.NET Core User Secrets and were not committed to the repository.

## 4. Manual Browser Verification

The frontend was tested using a separate worktree based on the `SCRUM-68-login-page` branch, at commit `eaf711b`.

| Scenario | Observed Result | Status |
|---|---|---|
| `GET /api/health` | Returned `{"status":"ok"}` | PASS |
| Load `/login.html` | Login form displayed | PASS |
| Register test customer through API | Customer successfully created | PASS |
| Login using valid credentials | Redirected to storefront | PASS |
| Display authenticated user | Customer name and Logout button visible | PASS |
| Refresh page | Authenticated session retained | PASS |
| Logout | Session closed | PASS |
| Call `/api/auth/me` after logout | HTTP 401 Unauthorized | PASS |
| Access `/api/customer/profile` while logged in | Complete test profile returned | PASS |
| Exclude password information from profile response | No password or password hash exposed | PASS |
| Login using incorrect password | Generic error: "Invalid email or password." | PASS |

Only generated test data was used for registration.

## 5. Integration Dependencies and Remaining Work

- Registration was verified through the backend API, not a browser registration page.
- The reviewed SCRUM-68 snapshot did not contain `register.html`; coordinate with the frontend owner before final acceptance.
- Automated authentication tests passed, but the skipped real-MySQL integration-test cases remain unverified.
- Browser-level role-isolation behavior and the completed registration interface still require final acceptance checks.
- The SCRUM-47 branch has not yet been merged into `main`.

## 6. Conclusion

The existing authentication backend was successfully exercised with real MySQL and the SCRUM-68 login frontend. Registration through the API, authenticated login, session persistence, logout, profile retrieval, and generic invalid-credential handling were verified.

Two additional automated authentication tests were implemented and pushed to GitHub. Final completion of SCRUM-47 depends on remaining frontend integration checks and team review.