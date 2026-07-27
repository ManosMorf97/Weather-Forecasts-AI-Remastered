# UC13: Change Authentication Details

**ID:** UC13  
**Name:** Change Authentication Details  
**Actor:** End User  
**Description:** User changes their username, email, or password directly within the app's own Account Settings screen. The Frontend calls the Firebase Client SDK directly for these changes; no backend service is involved, and the system never collects, validates, or stores these fields itself.

**Preconditions:**
- User is logged in

**Main Flow:**
1. User selects "Account Settings" from their profile menu
2. Frontend displays editable username, email, and password fields, pre-filled from the current Firebase user object
3. User updates one or more fields and confirms
4. Frontend calls the corresponding Firebase Client SDK method (`updateProfile`, `updateEmail`, `updatePassword`) directly against the Authentication Service
5. Authentication Service validates and applies the change
6. Frontend displays a confirmation message

**Alternative Flows:**
- **A1: Re-authentication Required**
  - At step 4, if the Authentication Service rejects the change because the session isn't recent enough, Frontend prompts the user to re-enter their password, calls the SDK's re-authentication method, then retries the change

**Postconditions:**
- Account details are updated directly in the Authentication Service
- No local data is affected, since the app never stores username/email/password

**Exceptions:**
- **E1:** Authentication Service unavailable - Frontend displays an error and allows the user to retry later
