# UC8: Request Analytics

**ID:** UC8  
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
6. System invokes UC14 (Calculate Aggregates) if needed
7. System generates analytics report
8. System presents report preview

**Alternative Flows:**
- **A1: Invalid Parameters**
  - At step 5, if parameters invalid or date range too large, system displays error
  - User returns to step 3

**Postconditions:**
- Analytics report is generated
- Report is ready for download (UC9)

**Exceptions:**
- **E1:** Report generation timeout - system queues request for async processing
