# UC14: Logout

**ID:** UC14
**Name:** Logout
**Actor:** End User
**Description:** User logs out of the application. The Frontend calls the Appwrite Web SDK's `account.deleteSession('current')`, which deletes this device's session (server-side) and clears the stored session from local persistence (Option A: single-device logout). No User Actions Server call is made and other sessions are not revoked - any other device or browser where the user is logged in remains active. This use case is entirely a Frontend-to-Authentication-Service interaction, same as UC13; the User Actions Server plays no part in it.

**Preconditions:**
- User holds an active Appwrite session (from UC1)

**Main Flow:**
1. User selects "Log out"
2. Frontend calls the Appwrite Web SDK's `account.deleteSession('current')`
3. Appwrite Web SDK clears the stored session from local persistence on this device
4. Frontend redirects the user to the login form (UC1)

**Alternative Flows:**
- None

**Postconditions:**
- No active session remains on this device; Frontend shows the login form
- Only this device's session is deleted; any JWT already minted for this device stays technically valid until its natural expiry (~15 min); other devices/sessions, if any, are unaffected and remain logged in

**Exceptions:**
- None

**Notes:**
- **Accepted risk (Option A, by design):** if a JWT was captured before logout (e.g. via XSS), deleting this device's session does not invalidate that already-issued JWT - it remains usable until it naturally expires (~15 min). A stronger "Log out of all devices" action (server-side revocation of every session via the Appwrite Server SDK, e.g. `users.deleteSessions`) is intentionally out of scope for this use case and would be a separate use case if ever needed.
