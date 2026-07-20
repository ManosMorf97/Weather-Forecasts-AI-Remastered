# UC15: Calculate Aggregates

**ID:** UC15  
**Name:** Calculate Aggregates  
**Actor:** End User  
**Description:** System calculates aggregated forecasts by selecting the forecast from the service with the maximum average rating for each city. This use case is invoked by UC8 (Request Analytics) and UC10 (View Aggregated Forecast).

**Preconditions:**
- Forecast services have data for requested cities
- Rating data exists for services

**Main Flow:**
1. System receives request with parameters (cities, timeframe, metrics, selected services)
2. For each city, system retrieves forecasts from all selected services
3. For each city, system retrieves average rating for each service
4. For each city, system identifies the service with maximum average rating
5. System selects the forecast from the highest-rated service for each city
6. System packages aggregated data with metadata (service name, rating score)
7. System returns aggregated forecast

**Alternative Flows:**
- **A1: No Ratings Available**
  - At step 3, if no ratings exist for a city, system selects the service name closest to A alphabetically
  - System flags result as "unrated selection"
- **A2: Tie in Ratings**
  - If multiple services have same maximum rating, system selects the service name closest to A alphabetically
  - System indicates tie in metadata

**Postconditions:**
- Aggregated forecast is calculated with best-rated service per city
- Result includes service name and rating score for each city

**Exceptions:**
- **E1:** Insufficient data - system returns error indicating minimum requirements not met
