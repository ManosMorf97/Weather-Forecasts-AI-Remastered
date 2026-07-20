# UC3: Edit Profile

**ID:** UC3  
**Name:** Edit Profile  
**Actor:** End User  
**Description:** User modifies existing profile including cities and forecasting services.

**Preconditions:**
- User is logged in
- User has an existing profile

**Main Flow:**
1. User selects "Edit Profile" option
2. System retrieves and displays current profile data
3. User modifies profile fields (email, username, and password)
4. System invokes UC4 (Select Cities) if user wants to modify cities
5. System invokes UC5 (Select Forecasting Services) if user wants to modify services
6. User submits changes
7. System validates input and checks that the updated email and username are not already used by another account
8. If password was changed, system hashes the new password and updates the profile record
9. System displays confirmation message

**Alternative Flows:**
- **A1: No Changes Made**
  - At step 6, if no changes detected, system displays message
  - Profile remains unchanged
- **A2: Username or Email Already Exists**
  - At step 7, if the updated username or email is already used by another account, system displays conflict error messages
  - User returns to step 3 to correct conflicting fields

**Postconditions:**
- Profile is updated with new information
- Changes are reflected in user's forecast preferences

**Exceptions:**
- **E1:** Concurrent update conflict - system displays error and reloads current data
