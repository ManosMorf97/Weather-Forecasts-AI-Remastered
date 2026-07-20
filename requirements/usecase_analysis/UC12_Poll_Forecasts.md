# UC12: Poll Forecasts

**ID:** UC12  
**Name:** Poll Forecasts  
**Actor:** Scheduler  
**Description:** System periodically polls partner forecasting services to retrieve latest forecast data for (city, service) combinations that users have selected in their profiles.

**Preconditions:**
- Partner services are configured in system
- Polling schedule is defined

**Main Flow:**
1. Scheduler triggers polling job based on schedule
2. System retrieves unique (city, service) combinations from all user profiles
3. For each service, system identifies cities that users have selected for that service
4. System sends GET request to partner API for those specific cities
5. Partner service returns forecast data for requested cities
6. System validates received data
7. System invokes UC13 (Store Forecasts) for each response
8. System logs polling results
9. System schedules next polling cycle

**Alternative Flows:**
- **A1: Service Unavailable**
  - At step 4, if service doesn't respond, system logs error
  - System continues with next service
- **A2: Rate Limiting**
  - If rate limit reached, system delays next poll for that service
- **A3: No User Preferences**
  - At step 2, if no users have defined (city, service) preferences, system skips polling
  - System reschedules next polling cycle

**Postconditions:**
- Latest forecasts retrieved from available services for user-requested (city, service) combinations
- Data is stored and available for user access

**Exceptions:**
- **E1:** All services fail - system sends alert to administrators
