# UC11: Poll Forecasts and Notify Users

**ID:** UC11  
**Name:** Poll Forecasts and Notify Users  
**Actor:** Scheduler  
**Description:** System periodically polls partner forecasting services for the (city, service) combinations users have selected, stores new or changed forecast data, and notifies users of any stored forecast that indicates a life-threatening condition and that they have not yet been notified about — whether that forecast was just polled or was already stored before they subscribed.

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
7. System stores new or updated forecast records (skipping exact duplicates)
8. System finds all stored forecast records with a danger flag for a (city, service) combination and identifies the users who have both that city and service in their preferences
9. System excludes, for each danger forecast record, any user who has already been notified for that specific record
10. System groups the remaining danger forecasts by user, building one consolidated warning list per user who has at least one unnotified warning
11. System batch-fetches emails for all these users' ids from the Authentication Service (Firebase Admin SDK, up to 100 ids per call)
12. System sends each such user a single notification containing their full list of warnings via configured channel (email/push)
13. System logs notification delivery for each user, associated with the specific forecast records covered
14. System logs polling results
15. System schedules next polling cycle

**Alternative Flows:**
- **A1: Service Unavailable**
  - At step 4, if service doesn't respond, system logs error
  - System continues with next service
- **A2: Rate Limiting**
  - If rate limit reached, system delays next poll for that service
- **A3: No User Preferences**
  - At step 2, if no users have defined (city, service) preferences, system skips polling
  - System reschedules next polling cycle
- **A4: Duplicate Forecast Data**
  - At step 7, if an exact duplicate exists (matching serviceId, cityId, timestamp, type, and data values), system skips storage for that record
- **A5: Updated Forecast Data**
  - At step 7, if a forecast with matching serviceId, cityId, and timestamp exists but with different data values, system updates the existing record
- **A6: No Matching Users for a Danger Forecast**
  - At step 8, if no users have the affected (city, service) combination in their preferences, system logs the event and sends no notification for that forecast
- **A7: New Subscriber to an Existing Danger**
  - If a user adds a (city, service) combination that already has a stored danger forecast they have not been notified about, that forecast is included the next time this use case runs, since the user has not yet received a notification for that specific record
- **A8: Batch Lookup Failure**
  - At step 11, if a batch call to the Authentication Service fails, system retries that batch with backoff
  - If it still fails, system logs the failure and excludes the affected users from this cycle; they remain unnotified and are retried next cycle

**Postconditions:**
- Latest forecasts are retrieved and stored for user-requested (city, service) combinations
- Each user is notified exactly once per distinct danger forecast record they are eligible for, regardless of whether the forecast was just polled or was already stored before they subscribed
- Users are not re-notified for a danger forecast record they have already received; a new notification is only triggered when the underlying forecast data actually changes (e.g., a newer timestamp or updated values produces a new record)
- All polling results and notification deliveries are logged

**Exceptions:**
- **E1:** All services fail - system sends alert to administrators
- **E2:** Storage transaction fails for a forecast record - system rolls back and logs error
- **E3:** Notification delivery fails - system retries using an alternative channel; if that also fails, system logs the delivery as failed on all channels
