# UC1: Login

**ID:** UC1  
**Name:** Login  
**Actor:** End User  
**Description:** User authenticates to access the Weather Forecasts system.

**Preconditions:**
- User is not already authenticated

**Main Flow:**
1. User navigates to the login page
2. System displays login form
3. User enters credentials (username/email and password)
4. The submitted password is hashed and compared with the stored hashed password 
5. System creates user session
6. System redirects user to dashboard

**Alternative Flows:**
- **A1: Invalid Credentials**
  - At step 4, if credentials are invalid, system displays error message
  - User returns to step 3
- **A2: Forgot Password**
  - At step 3, user selects "Forgot Password"
  - System initiates password recovery process

**Postconditions:**
- User is authenticated and has active session
- User can access protected features

**Exceptions:**
- **E1:** System authentication service unavailable - display maintenance message
