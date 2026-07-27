# UC2: Create Profile

**ID:** UC2  
**Name:** Create Profile  
**Actor:** System (invoked by UC1)  
**Description:** On every successful authentication, the system ensures a local profile row exists for the authenticated user, keyed by the Authentication Service's user_id. No username, email, or password is copied into this row - those live only in the Authentication Service (Firebase Authentication) and are fetched on demand when actually needed (e.g. UC9, UC11).

**Preconditions:**
- User holds a valid Firebase ID token issued by the Authentication Service (see UC1: Login / Sign Up)

**Main Flow:**
1. System extracts user_id from the verified ID token
2. System performs an idempotent upsert of the User row keyed by user_id (insert if absent; no-op if already present)
3. System checks whether the user has at least one CitySite selection
4. If none exists, system invokes UC4 (Select Cities) and UC5 (Select Forecasting Services) to complete initial setup
5. System returns control to UC1 (Login / Sign Up)

**Alternative Flows:**
- **A1: Existing, Already Configured User**
  - At step 3, if the user already has at least one CitySite selection, step 4 is skipped

**Postconditions:**
- Local profile row exists, keyed by user_id
- New users are routed into initial city/service selection

**Exceptions:**
- **E1:** Database write failure during upsert - system displays an error and allows retry; user is treated as not yet provisioned until the profile row exists
