# UC1: Login / Sign Up

**ID:** UC1  
**Name:** Login / Sign Up  
**Actor:** End User  
**Description:** User authenticates via the Authentication Service (Appwrite Authentication) using the Frontend's own sign-in form, which calls the Appwrite Web SDK directly. This use case is entirely a Frontend-to-Authentication-Service interaction - the User Actions Server (backend) plays no part in it. It ends once the Frontend holds an active Appwrite session; the Frontend's first authenticated call to the backend then triggers Create Profile (UC2), which JIT-provisions the user's profile and reports back whether the user has a city/service selection, leaving the Frontend to decide where to redirect based on that response.

The Frontend also enforces this as a route guard on every page, not just at app launch: any page other than the login form itself requires a valid session, checked the same way (restored Appwrite session). If that check ever fails - on initial load, on direct/deep-link navigation, or because the session expired while the user was on another page - the Frontend redirects to this login form (see A4).

**Preconditions:**
- None (applies to both first-time and returning users)

**Main Flow:**
1. User opens the application
2. Frontend's Appwrite SDK restores any existing session from local persistence
3. If a valid session is restored, flow ends here with an active session
4. Otherwise, Frontend displays its own login form (no external redirect)
5. User enters credentials into the Frontend's form; Frontend calls the Appwrite Web SDK to sign in (`account.createEmailPasswordSession`), which validates them directly against the Authentication Service
6. Authentication Service creates a session, which the Appwrite Web SDK persists locally; flow ends with an active session
7. Frontend mints a short-lived JWT from the session (`account.createJWT()`) and makes its first authenticated call to the User Actions Server, which continues into Create Profile (UC2)

**Alternative Flows:**
- **A1: New Account**
  - At step 4, user selects "Register" on the Frontend's own form instead of logging in
  - Frontend calls the Appwrite Web SDK to create the account (`account.create` with name/email/password) directly against the Authentication Service
  - Flow continues at step 5 as a first-time login
- **A2: Invalid Credentials**
  - At step 5, the Appwrite Web SDK returns an error; Frontend displays it and re-prompts for credentials
- **A3: Forgot Password**
  - At step 4/5, user selects "Forgot Password" on the Frontend's form
  - Frontend calls the Appwrite Web SDK's password-recovery flow (`account.createRecovery`); Authentication Service handles recovery (emailing a reset link) directly
- **A4: Route Guard - Unauthenticated Page Access**
  - At any point, on any page other than the login form itself, if the user has no valid session - a deep link, a manually typed URL, or a session that expired while the user was on another page - the Frontend's route guard redirects to the login form (step 4), same as a fresh app open with no session to restore
  - This is the same check as steps 2-3, just re-run on every page rather than only at initial launch

**Postconditions:**
- User holds an active Appwrite session
- Frontend proceeds into Create Profile (UC2) on its next call to the backend, which determines profile provisioning and redirect target

**Exceptions:**
- **E1:** Authentication Service unavailable - display maintenance message
