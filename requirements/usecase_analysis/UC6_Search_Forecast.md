# UC6: Search Forecast by City

**ID:** UC6  
**Name:** Search Forecast by City  
**Actor:** End User  
**Description:** User searches for weather forecasts for a specific city. Upon login, system automatically displays forecasts based on user's saved cities and selected services.

**Preconditions:**
- User is logged in
- Forecast data exists for at least one city

**Main Flow:**
1. User logs in and navigates to forecast dashboard, system retrieves user's profile preferences (cities and selected services)
2. System retrieves forecast data for all saved cities from selected services
3. System sorts forecasts by SiteName (service), CityName, and Time
4. System displays forecast dashboard with organized forecast information
5. User views forecasts for their preferred cities and their selected services

**Alternative Flows:**
- **A1: Manual Search**
  - At step 1, user enters city name in search field instead of viewing saved cities
  - User submits search request
  - System retrieves forecast data for the specified city
  - If forecasts available, system displays search results
  - If no forecasts available, system displays informative message and activity ends
- **A2: No Forecasts Available for Saved Cities**
  - At step 2, if no forecasts exist for saved cities, system displays informative message
  - User can search for other cities or update profile

**Postconditions:**
- Forecast data is displayed to user ordered by SiteName, CityName, and Time
- User has viewed forecast information for saved and/or searched cities

**Exceptions:**
- **E1:** Forecast retrieval timeout - system displays cached data if available
- **E2:** Search service timeout - system displays error and suggests retry
- **E3:** User not logged in - system redirects user to login page (UC1: Login) and denies access to forecast dashboard and search; use case ends
