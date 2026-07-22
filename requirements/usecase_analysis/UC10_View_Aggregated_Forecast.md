# UC10: View Aggregated Forecast

**ID:** UC10  
**Name:** View Aggregated Forecast  
**Actor:** End User  
**Description:** User views aggregated forecast where each city shows data from the service with the maximum average rating for that city, considering only the user's selected services.

**Preconditions:**
- User is logged in
- Services have data for the requested city
- Rating data exists for services

**Main Flow:**
1. User selects "View Aggregate Forecast" option
2. System invokes UC14 (Calculate Aggregates)
3. System retrieves aggregated forecast data showing best-rated service per city from user's selected services
4. System displays forecast from service with maximum average rating for each city among user's selected services
5. System shows rating score and service name for each city and the forecast

**Alternative Flows:**
- **A1: Single Service Only**
  - At step 2, if only one service has data, system displays that service's forecast
  - System indicates aggregation not applicable

**Postconditions:**
- Aggregated forecast is displayed to user

**Exceptions:**
- None
