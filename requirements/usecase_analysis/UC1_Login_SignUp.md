# UC1: Login / Sign Up

**ID:** UC1  
**Name:** Login / Sign Up  
**Actor:** End User  
**Description:** User authenticates via the Authentication Service (Firebase Authentication) using the Frontend's own sign-in form, which calls the Firebase Client SDK directly. On success, the system provisions a local profile if one does not exist yet (UC2) and routes the user to city/service selection if their setup is incomplete, otherwise to the dashboard.

**Preconditions:**
- None (applies to both first-time and returning users)

**Main Flow:**
1. User opens the application
2. Frontend's Firebase SDK restores any existing session from local persistence and fires its auth-state listener
3. If a valid session is restored, system proceeds directly to step 6
4. Otherwise, Frontend displays its own login form (no external redirect)
5. User enters credentials into the Frontend's form; Frontend calls the Firebase Client SDK to sign in, which validates them directly against the Authentication Service
6. System verifies the resulting Firebase ID token (Firebase Admin SDK) and invokes UC2 (Create Profile) to ensure a local profile record exists
7. If the user has no city/service selection yet, system redirects into UC4 (Select Cities) and UC5 (Select Forecasting Services)
8. Otherwise, system redirects the user to the dashboard

**Alternative Flows:**
- **A1: New Account**
  - At step 4, user selects "Register" on the Frontend's own form instead of logging in
  - Frontend calls the Firebase Client SDK to create the account (username/email/password) directly against the Authentication Service
  - Flow continues at step 5 as a first-time login
- **A2: Invalid Credentials**
  - At step 5, the Firebase Client SDK returns an error; Frontend displays it and re-prompts for credentials
- **A3: Forgot Password**
  - At step 4/5, user selects "Forgot Password" on the Frontend's form
  - Frontend calls the Firebase Client SDK's password-reset flow; Authentication Service handles recovery (e.g. emailing a reset link) directly

**Postconditions:**
- User holds a valid Firebase ID token and an active session
- A local profile record exists for the user (see UC2)
- User is on the dashboard, or on the city/service selection screen if setup is incomplete

**Exceptions:**
- **E1:** Authentication Service unavailable - display maintenance message
- **E2:** Token verification fails - treat user as unauthenticated and return to step 4
