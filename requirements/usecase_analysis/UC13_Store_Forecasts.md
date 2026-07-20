# UC13: Store Forecasts

**ID:** UC13  
**Name:** Store Forecasts  
**Actor:** Scheduler  
**Description:** System stores forecast data received from partner services into the database.

**Preconditions:**
- Forecast data has been received (via UC12)
- Data has been validated

**Main Flow:**
1. System receives forecast data with metadata (serviceId, cityId, timestamp, type)
2. System checks if an identical forecast record already exists (matching serviceId, cityId, timestamp, type, and data values)
3. System prepares database transaction
4. System inserts/updates forecast records
5. System indexes data for efficient searching
6. System updates cache with latest forecasts
7. System commits transaction
8. System returns success status

**Alternative Flows:**
- **A1: Duplicate Data**
  - At step 2, if exact duplicate exists (matching serviceId, cityId, timestamp, type, and data values), system skips storage
  - Returns success without modification
- **A2: Update Existing**
  - At step 2, if a forecast with matching serviceId, cityId, and timestamp exists but with different data values, system updates the existing record
  - System continues to step 3

**Postconditions:**
- Forecast data is persisted in database
- Data is available for user queries
- Cache is updated

**Exceptions:**
- **E1:** Transaction fails - system rolls back and logs error
