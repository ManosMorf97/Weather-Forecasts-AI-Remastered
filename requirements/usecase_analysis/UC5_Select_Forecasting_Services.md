# UC5: Select Forecasting Services

**ID:** UC5  
**Name:** Select Forecasting Services  
**Actor:** End User  
**Description:** User selects which partner forecasting services to include in their forecast data, and can remove services they no longer want.

**Preconditions:**
- User is in Create Profile (UC2) or Edit Selections (UC3) flow

**Main Flow:**
1. System displays list of available forecasting services with currently selected services (if any).
2. User selects one or more services to add.
3. User can remove existing services from their selection.
4. User confirms selection.
5. System saves updated service preferences.

**Alternative Flows:**
- **A1: Remove Service**
  - At step 3, user selects service to remove from their list.
  - System removes service from selection.
  - User can continue adding/removing more services.
- **A2: All Services Removed**
  - At step 4, if user attempts to confirm with no services selected, system displays error.
  - User returns to step 2 to select at least one service.

**Postconditions:**
- Selected services are associated with user profile.
- Removed services are no longer associated with user profile.
- User will receive forecasts only from selected services.

**Exceptions:**
- None
