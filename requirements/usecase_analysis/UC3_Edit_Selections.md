# UC3: Edit Selections

**ID:** UC3  
**Name:** Edit Selections  
**Actor:** End User  
**Description:** User modifies their city and forecasting service selections. Account details (username, email, password) are handled separately by UC13 (Change Authentication Details) and are outside this use case.

**Preconditions:**
- User is logged in
- User has an existing profile

**Main Flow:**
1. User selects "Edit Selections" option
2. System retrieves and displays the user's current city and service selections
3. System invokes UC4 (Select Cities) if user wants to modify cities
4. System invokes UC5 (Select Forecasting Services) if user wants to modify services
5. User confirms changes
6. System saves updated preferences
7. System displays confirmation message

**Alternative Flows:**
- **A1: No Changes Made**
  - At step 5, if no changes detected, system displays message
  - Preferences remain unchanged

**Postconditions:**
- City/service preferences are updated
- Changes are reflected in user's forecast preferences

**Exceptions:**
- **E1:** Concurrent update conflict - system displays error and reloads current data
