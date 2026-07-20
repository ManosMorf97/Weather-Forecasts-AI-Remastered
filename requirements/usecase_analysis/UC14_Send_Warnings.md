# UC14: Send Warnings

**ID:** UC14  
**Name:** Send Warnings  
**Actor:** Scheduler  
**Description:** System detects life-threatening weather conditions by (city, service) combinations and triggers warning notifications to affected users.

**Preconditions:**
- Forecast data with danger flags is stored in system
- Warning detection criteria are defined

**Main Flow:**
1. Scheduler triggers warning detection job
2. System checks stored forecast data for danger/warning flags by (city, service) combinations
3. System identifies (city, service) pairs with life-threatening weather conditions
4. For each affected (city, service) pair, system invokes UC11 (Receive Warning Notification)
5. System logs warning detection events
6. System tracks notification delivery status

**Alternative Flows:**
- **A1: No Hazards Detected**
  - At step 3, if no dangerous conditions found, system completes normally
  - No notifications triggered

**Postconditions:**
- Warning notifications triggered for affected (city, service) combinations
- Warning detection events are logged for audit

**Exceptions:**
- **E1:** Notification service unavailable - system queues notifications for retry
