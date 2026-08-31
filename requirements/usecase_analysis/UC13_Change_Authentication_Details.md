# UC13: Change Authentication Details

**ID:** UC13  
**Name:** Change Authentication Details  
**Actor:** End User  
**Description:** User changes their name, email, or password directly within the app's own Account Settings screen. The Frontend calls the Appwrite Web SDK directly for these changes; no backend service is involved, and the system never collects, validates, or stores these fields itself.

**Preconditions:**
- User is logged in

**Main Flow:**
1. User selects "Account Settings" from their profile menu
2. Frontend displays editable name, email, and password fields, pre-filled from the current Appwrite account (`account.get()`)
3. User updates one or more fields and confirms
4. Frontend calls the corresponding Appwrite Web SDK method (`account.updateName`, `account.updateEmail`, `account.updatePassword`) directly against the Authentication Service
5. Authentication Service validates and applies the change
6. Frontend displays a confirmation message

**Alternative Flows:**
- **A1: Current Password Required**
  - `account.updateEmail` and `account.updatePassword` require the user's current password as a parameter. At step 4, if it is missing or wrong, the Authentication Service rejects the change; Frontend prompts the user to enter their current password, then retries the same call with it. (`account.updateName` needs no password.)

**Postconditions:**
- Account details are updated directly in the Authentication Service
- No local data is affected, since the app never stores name/email/password

**Exceptions:**
- **E1:** Authentication Service unavailable - Frontend displays an error and allows the user to retry later
