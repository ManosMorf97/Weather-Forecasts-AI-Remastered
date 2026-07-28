# UC1: Login / Sign Up

**ID:** UC1  
**Name:** Login / Sign Up  
**Actor:** End User  
**Description:** User authenticates via the Authentication Service (Firebase Authentication) using the Frontend's own sign-in form, which calls the Firebase Client SDK directly. This use case is entirely a Frontend-to-Authentication-Service interaction - the User Actions Server (backend) plays no part in it. It ends once the Frontend holds a valid Firebase ID token; the Frontend's first authenticated call to the backend then triggers Create Profile (UC2), which JIT-provisions the user's profile and reports back whether the user has a city/service selection, leaving the Frontend to decide where to redirect based on that response.

**Preconditions:**
- None (applies to both first-time and returning users)

**Main Flow:**
1. User opens the application
2. Frontend's Firebase SDK restores any existing session from local persistence and fires its auth-state listener
3. If a valid session is restored, flow ends here with an active session
4. Otherwise, Frontend displays its own login form (no external redirect)
5. User enters credentials into the Frontend's form; Frontend calls the Firebase Client SDK to sign in, which validates them directly against the Authentication Service
6. Authentication Service issues a Firebase ID token to the Frontend; flow ends with an active session
7. Frontend proceeds to make its first authenticated call to the User Actions Server, which continues into Create Profile (UC2)

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
- Frontend proceeds into Create Profile (UC2) on its next call to the backend, which determines profile provisioning and redirect target

**Exceptions:**
- **E1:** Authentication Service unavailable - display maintenance message
