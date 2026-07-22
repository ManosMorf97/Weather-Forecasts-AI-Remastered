# UC7: Rate Forecasting Service

**ID:** UC7  
**Name:** Rate Forecasting Service  
**Actor:** End User  
**Description:** User submits a rating for a specific forecast (city, service, timestamp)  combination based on their experience with forecast accuracy. Ratings are aggregated to calculate average service ratings used in aggregated forecasts.

**Preconditions:**
- User is logged in
- User has viewed forecasts from at least one (city, service) combination

**Main Flow:**
1. User views forecasts in the system (via UC6)
2. System displays forecasts with rating option for each (city, service, timestamp) combination
3. User selects a specific forecast to rate
4. System displays current rating (if any) for that specific (city, service, timestamp) and rating scale (1-5 stars)
5. User submits rating value (1-5)
6. System validates rating input
7. System stores or updates the rating record associated with (user, city, service, timestamp)
8. System recalculates the average rating for the service based on all ratings for that service across all cities and timestamps
9. System displays confirmation message

**Alternative Flows:**
- **A1: Update Existing Rating**
  - At step 4, if user has already rated this specific (city, service, timestamp), system displays existing rating
  - User can update their rating
  - System updates the existing rating record instead of creating new one
- **A2: Remove Rating**
  - At step 5, user selects option to remove their rating
  - System deletes the rating record for that (city, service, timestamp)
  - System recalculates average rating for the service
  - System displays confirmation message


**Postconditions:**
- User's rating is stored and associated with the specific (city, service, timestamp) combination
- Service's average rating is updated across all rated forecasts
- Updated ratings are available for aggregated forecast calculations (UC10, UC14)

**Exceptions:**
- **E1:** Database update fails - system displays error and allows retry
