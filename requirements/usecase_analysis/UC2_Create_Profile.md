# UC2: Create Profile

**ID:** UC2  
**Name:** Create Profile  
**Actor:** End User  
**Description:** User creates a new profile with cities of interest and preferred forecasting services.

**Preconditions:**
- User is not logged in
- User does not already have an account

**Main Flow:**
1. User selects "Create Profile" option
2. System displays profile creation form
3. User enters profile details (email, username, and password)
4. System invokes UC4 (Select Cities)
5. System invokes UC5 (Select Forecasting Services)
6. User submits profile
7. System validates input
8. System hashes the password and creates the profile record
9. System displays confirmation message

**Alternative Flows:**
- **A1: Validation Errors**
  - At step 7, if validation fails, system displays error messages
  - User returns to step 3 to correct errors
- **A2: User Exists Error**
  - At step 7, if user already exists, system displays error messages
  - User returns to step 3 to correct errors

**Postconditions:**
- Profile is created and saved in database
- User has selected cities and forecasting services configured

**Exceptions:**
- **E1:** Database connection failure - system displays error and allows retry
