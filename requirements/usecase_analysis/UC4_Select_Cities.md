# UC4: Select Cities

**ID:** UC4  
**Name:** Select Cities  
**Actor:** End User  
**Description:** User selects one or more cities to track weather forecasts, and can delete cities they are no longer interested in.

**Preconditions:**
- User is in Create Profile (UC2) or Edit Profile (UC3) flow

**Main Flow:**
1. System displays city selection interface with currently selected cities (if any)
2. User searches for cities by name or location
3. System calls external City API to retrieve matching cities
4. System displays matching cities from API response
5. User selects desired cities to add
6. User can delete existing cities from their selection
7. User confirms selection
8. System validates that at least one city remains selected
9. System saves updated city preferences

**Alternative Flows:**
- **A1: City Not Found**
  - At step 4, if API returns no matches, system displays "No results" message
  - User can refine search criteria
- **A2: Delete City**
  - At step 6, user selects city to remove from their list
  - System removes city from selection
  - User can continue adding/removing more cities
- **A3: All Cities Deleted**
  - At step 8, if user attempts to confirm with no cities selected, system displays error
  - User returns to step 2 to select at least one city
- **A4: API Unavailable**
  - At step 3, if City API is unavailable, system displays message indicating limited search results

**Postconditions:**
- Selected cities are associated with user profile
- Deleted cities are removed from user profile
- User will receive forecasts only for currently selected cities

**Exceptions:**
- **E1:** API timeout - system retries once, then falls back to cached data
