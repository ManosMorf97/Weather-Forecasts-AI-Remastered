# UC11: Receive Warning Notification

**ID:** UC11  
**Name:** Receive Warning Notification  
**Actor:** Scheduler  
**Description:** System sends warning notifications to users about life-threatening weather conditions in their subscribed cities from their selected services.

**Preconditions:**
- Life-threatening weather condition detected in subscribed city

**Main Flow:**
1. Scheduler checks stored forecast data for danger/warning flags indicating life-threatening weather conditions by city and service
2. For each (city, service) pair with danger flag, system identifies users who have both that city and service in their preferences
3. System sends notification to matching users via configured channel (email/push)
4. System logs notification delivery

**Alternative Flows:**
- **A1: Multiple Danger Warnings for Same User**
  - If a user has multiple (city, service) combinations with danger flags, system consolidates warnings into single notification
- **A2: No Matching Users**
  - At step 2, if no users have the affected (city, service) combination in their preferences, system logs the event and no notifications are sent

**Postconditions:**
- Users with matching (city, service) preferences are notified of dangerous weather conditions
- All notification deliveries and events are logged in system

**Exceptions:**
- **E1:** Notification delivery fails - system retries using alternative channel
