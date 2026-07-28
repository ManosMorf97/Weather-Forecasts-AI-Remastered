# UC14: Logout

**ID:** UC14
**Name:** Logout
**Actor:** End User
**Description:** User logs out of the application. The Frontend calls the Firebase Client SDK's sign-out method, which clears the cached ID token and refresh token from local persistence on this device only (Option A: local-only logout). No backend call is made and no server-side token revocation occurs - any other device or browser where the user is logged in remains active. This use case is entirely a Frontend-to-Firebase-Client-SDK interaction, same as UC13; the User Actions Server plays no part in it.

**Preconditions:**
- User holds an active session (valid Firebase ID token, from UC1)

**Main Flow:**
1. User selects "Log out"
2. Frontend calls the Firebase Client SDK's sign-out method
3. Firebase Client SDK clears the cached ID token and refresh token from local persistence on this device
4. Frontend redirects the user to the login form (UC1)

**Alternative Flows:**
- None

**Postconditions:**
- No active session remains on this device; Frontend shows the login form
- The tokens previously issued to this device are not server-side revoked - the short-lived ID token remains technically valid until its natural expiry, and the refresh token remains valid until it separately expires or is explicitly revoked; other devices/sessions, if any, are unaffected and remain logged in

**Exceptions:**
- None

**Notes:**
- **Accepted risk (Option A, by design):** if a token was compromised before logout (e.g. via XSS), this action does not stop it from being used elsewhere until it naturally expires. A stronger "Log out of all devices" action (explicit server-side refresh-token revocation via the Firebase Admin SDK) is intentionally out of scope for this use case and would be a separate use case if ever needed.
