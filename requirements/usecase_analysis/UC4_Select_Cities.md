# UC4: Select Cities

**ID:** UC4  
**Name:** Select Cities  
**Actor:** End User  
**Description:** User selects one or more cities to track weather forecasts, and can delete cities they are no longer interested in.

**Preconditions:**
- User is in Create Profile (UC2) or Edit Selections (UC3) flow

**Main Flow:**
1. Frontend displays city selection interface with currently selected cities (fetched from the User Actions Server, if any)
2. User searches for cities by name or location
3. Frontend calls the external City API directly to retrieve matching cities - no backend involvement, no auth required for this stateless lookup
4. Frontend displays matching cities from the API response
5. User selects desired cities to add
6. User can delete existing cities from their selection (local, unsaved change)
7. User confirms selection
8. Frontend validates that at least one city remains selected
9. Frontend sends the updated city selection to the User Actions Server, which saves it

**Alternative Flows:**
- **A1: City Not Found**
  - At step 4, if the API returns no matches, Frontend displays "No results" message
  - User can refine search criteria
- **A2: Delete City**
  - At step 6, user selects city to remove from their list
  - Frontend removes city from the local selection
  - User can continue adding/removing more cities
- **A3: All Cities Deleted**
  - At step 8, if user attempts to confirm with no cities selected, Frontend displays error
  - User returns to step 2 to select at least one city
- **A4: API Unavailable**
  - At step 3, if the City API is still unavailable after the E1 retry, Frontend falls back to cached city data from the User Actions Server and displays a message indicating limited search results

**Postconditions:**
- Selected cities are associated with user profile
- Deleted cities are removed from user profile
- User will receive forecasts only for currently selected cities

**Exceptions:**
- **E1:** City API timeout - Frontend retries once directly against the API, then falls back to cached city data via the User Actions Server (see A4)
