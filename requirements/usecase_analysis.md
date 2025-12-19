# Weather Forecasts — Use Case Analysis

## Overview
This document provides detailed specifications for each use case identified in the Weather Forecasts system. Each use case includes actors, preconditions, main flow, alternative flows, postconditions, and exception handling.

---

## UC1: Login

**ID:** UC1  
**Name:** Login  
**Actor:** End User  
**Description:** User authenticates to access the Weather Forecasts system.

**Preconditions:**
- User has a registered account in the system

**Main Flow:**
1. User navigates to the login page
2. System displays login form
3. User enters credentials (username/email and password)
4. System validates credentials
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

---

## UC2: Create Profile

**ID:** UC2  
**Name:** Create Profile  
**Actor:** End User  
**Description:** User creates a new profile with cities of interest and preferred forecasting services.

**Preconditions:**
- User is logged in
- User does not have an existing profile

**Main Flow:**
1. User selects "Create Profile" option
2. System displays profile creation form
3. User enters profile details (name, preferences)
4. System invokes UC4 (Select Cities)
5. System invokes UC5 (Select Forecasting Services)
6. User submits profile
7. System validates input
8. System creates profile record
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

---

## UC3: Edit Profile

**ID:** UC3  
**Name:** Edit Profile  
**Actor:** End User  
**Description:** User modifies existing profile including cities and forecasting services.

**Preconditions:**
- User is logged in
- User has an existing profile

**Main Flow:**
1. User selects "Edit Profile" option
2. System retrieves and displays current profile data
3. User modifies profile fields
4. System invokes UC4 (Select Cities) if user wants to modify cities
5. System invokes UC5 (Select Forecasting Services) if user wants to modify services
6. User submits changes
7. System validates input
8. System updates profile record
9. System displays confirmation message

**Alternative Flows:**
- **A1: No Changes Made**
  - At step 6, if no changes detected, system displays message
  - Profile remains unchanged

**Postconditions:**
- Profile is updated with new information
- Changes are reflected in user's forecast preferences

**Exceptions:**
- **E1:** Concurrent update conflict - system displays error and reloads current data

---

## UC4: Select Cities

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
8. System saves updated city preferences

**Alternative Flows:**
- **A1: City Not Found**
  - At step 4, if API returns no matches, system displays "No results" message
  - User can refine search criteria
- **A2: Delete City**
  - At step 6, user selects city to remove from their list
  - System removes city from selection
  - User can continue adding/removing more cities
- **A3: API Unavailable**
  - At step 3, if City API is unavailable, system displays message indicating limited search results

**Postconditions:**
- Selected cities are associated with user profile
- Deleted cities are removed from user profile
- User will receive forecasts only for currently selected cities

**Exceptions:**
- **E1:** API timeout - system retries once, then falls back to cached data
- **E2:** All cities deleted - system displays warning that at least one city should be selected

---

## UC5: Select Forecasting Services

**ID:** UC5  
**Name:** Select Forecasting Services  
**Actor:** End User  
**Description:** User selects which partner forecasting services to include in their forecast data.

**Preconditions:**
- User is in Create Profile (UC2) or Edit Profile (UC3) flow

**Main Flow:**
1. System displays list of available forecasting services.
2. User selects one or more services.
3. User confirms selection.
4. System saves service preferences.

**Alternative Flows:**
- **A1: No Service Selected**
  - At step 4, if no service selected, system displays warning.
  - System requires at least one service selection.

**Postconditions:**
- Selected services are associated with user profile.
- User will receive forecasts only from selected services.

**Exceptions:**
- None

---

## UC6: Search Forecast by City

**ID:** UC6  
**Name:** Search Forecast by City  
**Actor:** End User  
**Description:** User searches for weather forecasts for a specific city. Upon login, system automatically displays forecasts based on user's saved cities and selected services.

**Preconditions:**
- User is logged in
- Forecast data exists for at least one city

**Main Flow:**
1. When user logs in (UC1), system retrieves user's profile preferences (cities and selected services)
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
- **A2: No Profile Preferences**
  - At step 1, if user has no saved cities or services, system displays empty dashboard
  - System prompts user to configure profile (UC3)
- **A3: No Forecasts Available for Saved Cities**
  - At step 2, if no forecasts exist for saved cities, system displays informative message
  - User can search for other cities or update profile

**Postconditions:**
- Forecast data is displayed to user ordered by SiteName, CityName, and Time
- User has viewed forecast information for saved and/or searched cities

**Exceptions:**
- **E1:** Forecast retrieval timeout - system displays cached data if available
- **E2:** Search service timeout - system displays error and suggests retry

---


## UC11: Request Analytics

**ID:** UC11  
**Name:** Request Analytics  
**Actor:** End User  
**Description:** User requests analytics report containing forecast data and statistics.

**Preconditions:**
- User is logged in
- Historical forecast data exists

**Main Flow:**
1. User selects "Request Analytics" option
2. System displays analytics request form
3. User specifies parameters (cities, date range, services, metrics)
4. User submits request
5. System validates parameters
6. System invokes UC18 (Calculate Aggregates) if needed
7. System generates analytics report
8. System presents report preview

**Alternative Flows:**
- **A1: Invalid Parameters**
  - At step 5, if parameters invalid or date range too large, system displays error
  - User returns to step 3

**Postconditions:**
- Analytics report is generated
- Report is ready for download (UC12)

**Exceptions:**
- **E1:** Report generation timeout - system queues request for async processing

---

## UC12: Download Analytics

**ID:** UC12  
**Name:** Download Analytics  
**Actor:** End User  
**Description:** User downloads analytics report in structured format (JSON/CSV/PDF).

**Preconditions:**
- User has requested analytics (UC11)
- Analytics report is generated

**Main Flow:**
1. User selects "Download" option
2. System displays format options (JSON, CSV, PDF)
3. User selects desired format
4. System converts report to selected format
5. System initiates download
6. User receives file

**Alternative Flows:**
- **A1: Large File**
  - At step 5, if file too large, system sends download link via email
  - User can download from link later

**Postconditions:**
- Analytics file is downloaded to user's device

**Exceptions:**
- **E1:** Format conversion fails - system offers alternative format

---

## UC13: View Aggregated Forecast

**ID:** UC13  
**Name:** View Aggregated Forecast  
**Actor:** End User  
**Description:** User views aggregated forecast where each city shows data from the service with the maximum average rating for that city.

**Preconditions:**
- User is logged in
- Services have data for the requested city
- Rating data exists for services

**Main Flow:**
1. User selects "View Aggregate Forecast" option
2. System invokes UC18 (Calculate Aggregates)
3. System retrieves aggregated forecast data showing best-rated service per city
4. System displays forecast from service with maximum average rating for each city
5. System shows rating score and service name for each city and the forecast
6. User can compare with individual service forecasts

**Alternative Flows:**
- **A1: Single Service Only**
  - At step 2, if only one service has data, system displays that service's forecast
  - System indicates aggregation not applicable

**Postconditions:**
- Aggregated forecast is displayed to user

**Exceptions:**
- None

---

## UC14: Receive Warning Notification

**ID:** UC14  
**Name:** Receive Warning Notification  
**Actor:** End User  
**Description:** User receives warning notification about life-threatening weather conditions in subscribed cities.

**Preconditions:**
- User is registered and has profile
- User has subscribed cities
- Life-threatening weather condition detected in subscribed city

**Main Flow:**
1. System (UC17) triggers warning notification
2. System retrieves user notification preferences
3. System sends notification via configured channel (email/SMS/push)
4. User receives notification with warning details
5. User can view full forecast details by clicking notification link

**Alternative Flows:**
- **A1: Multiple Notifications**
  - If multiple cities affected, system consolidates into single notification

**Postconditions:**
- User is informed of dangerous weather conditions
- Notification is logged in system

**Exceptions:**
- **E1:** Notification delivery fails - system retries using alternative channel

---

## UC15: Poll Forecasts

**ID:** UC15  
**Name:** Poll Forecasts  
**Actor:** Scheduler  
**Description:** System periodically polls partner forecasting services to retrieve latest forecast data.

**Preconditions:**
- Partner services are configured in system
- Polling schedule is defined

**Main Flow:**
1. Scheduler triggers polling job based on schedule
2. System retrieves list of partner services to poll
3. For each service, system sends GET request to partner API
4. Partner service returns forecast data
5. System validates received data
6. System invokes UC16 (Store Forecasts) for each response
7. System logs polling results
8. System schedules next polling cycle

**Alternative Flows:**
- **A1: Service Unavailable**
  - At step 4, if service doesn't respond, system logs error
  - System continues with next service
- **A2: Rate Limiting**
  - If rate limit reached, system delays next poll for that service

**Postconditions:**
- Latest forecasts retrieved from available services
- Data is stored for user access

**Exceptions:**
- **E1:** All services fail - system sends alert to administrators

---

## UC16: Store Forecasts

**ID:** UC16  
**Name:** Store Forecasts  
**Actor:** Scheduler  
**Description:** System stores forecast data received from partner services into the database.

**Preconditions:**
- Forecast data has been received (via UC15)
- Data has been validated

**Main Flow:**
1. System receives forecast data with metadata (serviceId, cityId, timestamp, type)
2. System checks for duplicate entries
3. System prepares database transaction
4. System inserts/updates forecast records
5. System indexes data for efficient searching
6. System updates cache with latest forecasts
7. System commits transaction
8. System returns success status

**Alternative Flows:**
- **A1: Duplicate Data**
  - At step 2, if exact duplicate exists, system skips storage
  - Returns success without modification
- **A2: Update Existing**
  - If newer version of same forecast exists, system updates instead of inserting

**Postconditions:**
- Forecast data is persisted in database
- Data is available for user queries
- Cache is updated

**Exceptions:**
- **E1:** Transaction fails - system rolls back and logs error

---

## UC17: Send Warnings

**ID:** UC17  
**Name:** Send Warnings  
**Actor:** Scheduler  
**Description:** System detects life-threatening weather conditions and sends warning notifications to affected users.

**Preconditions:**
- Forecast data is stored in system
- Warning criteria are defined
- Users have subscribed cities

**Main Flow:**
1. Scheduler triggers warning detection job
2. System analyzes stored forecasts for hazardous conditions
3. System identifies cities with life-threatening weather
4. System queries users subscribed to affected cities
5. For each affected user, system invokes UC14 (Receive Warning Notification)
6. System logs warning events
7. System tracks notification delivery status

**Alternative Flows:**
- **A1: No Hazards Detected**
  - At step 3, if no dangerous conditions found, system completes normally
  - No notifications sent
- **A2: Warning Already Sent**
  - If warning already sent for same condition, system skips duplicate notification

**Postconditions:**
- Users subscribed to affected cities receive warnings
- Warning events are logged for audit

**Exceptions:**
- **E1:** Notification service unavailable - system queues notifications for retry

---

## UC18: Calculate Aggregates

**ID:** UC18  
**Name:** Calculate Aggregates  
**Actor:** System (invoked by UC11, UC13)  
**Description:** System calculates aggregated forecasts by selecting the forecast from the service with the maximum average rating for each city based on their chosen forecasts.

**Preconditions:**
- Forecast services have data for requested cities
- Rating data exists for services

**Main Flow:**
1. System receives request with parameters (cities, timeframe, metrics)
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

---

## Summary

This use case analysis covers all 18 use cases in the Weather Forecasts system:
- **User-facing (UC1-UC14):** Authentication, profile management, forecast viewing, ratings, analytics, and notifications
- **System operations (UC15-UC18):** Data ingestion, storage, warning detection, and aggregate calculations

Each use case is designed to support the functional requirements outlined in the requirements analysis document while ensuring proper error handling and alternative flows.

---
*Generated from requirements_analysis.md and usecase_diagram.puml*
