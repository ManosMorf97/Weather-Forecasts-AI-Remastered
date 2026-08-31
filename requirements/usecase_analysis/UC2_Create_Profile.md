# UC2: Create Profile

**ID:** UC2  
**Name:** Create Profile  
**Actor:** User Actions Server (invoked by the Frontend's first authenticated backend call after UC1)  
**Description:** On the Frontend's first authenticated call to the User Actions Server after Login / Sign Up (UC1), the User Actions Server ensures a local profile row exists for the authenticated user, keyed by the Authentication Service's user_id, and reports back whether the user has at least one CitySite selection. No name, email, or password is copied into this row - those live only in the Authentication Service (Appwrite Authentication) and are fetched on demand when actually needed (e.g. UC9, UC11). The User Actions Server only checks and reports state; the Frontend is responsible for redirecting the user based on the response.

**Preconditions:**
- User holds an active Appwrite session issued by the Authentication Service (see UC1: Login / Sign Up)

**Main Flow:**
1. Frontend calls the User Actions Server, presenting an Appwrite JWT (minted from the session via `account.createJWT()`) as a Bearer token
2. User Actions Server verifies the JWT against the Authentication Service (Appwrite Server SDK: `Client.setJWT(jwt)` then `account.get()`) and extracts user_id
3. User Actions Server performs an idempotent upsert of the User row keyed by user_id (insert if absent; no-op if already present)
4. User Actions Server checks whether the user has at least one CitySite selection
5. User Actions Server responds to the Frontend with the CitySite-selection result
6. If the response indicates none exists, Frontend redirects into Select Cities (UC4) and Select Forecasting Services (UC5) to complete initial setup
7. Otherwise, Frontend redirects the user to the dashboard

**Alternative Flows:**
- **A1: Existing, Already Configured User**
  - At step 4, if the user already has at least one CitySite selection, the response at step 5 reflects that and step 6 is skipped

**Postconditions:**
- Local profile row exists, keyed by user_id
- Frontend has redirected new users into initial city/service selection, or already-configured users to the dashboard

**Exceptions:**
- **E1:** Database write failure during upsert - User Actions Server displays an error and allows retry; user is treated as not yet provisioned until the profile row exists
- **E2:** Token verification against the Authentication Service fails - User Actions Server responds as unauthenticated; Frontend treats the user as logged out and returns to the Login / Sign Up (UC1) form
