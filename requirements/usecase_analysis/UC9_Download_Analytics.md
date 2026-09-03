# UC9: Download Analytics

**ID:** UC9
**Name:** Download Analytics
**Actor:** End User
**Status:** **PLANNED - NOT YET IMPLEMENTED**

> **Current behaviour:** there is no interactive download. The analytics report is
> delivered automatically by **email as a PDF attachment** at the end of UC8
> (see UC8 Main Flow step 8, and UC8 A4 / A5 for the failure and text-only cases).
> This document describes the *intended* future feature: letting the user pull the
> report on demand and choose its format.

**Description:** User downloads a previously generated analytics report in a structured
format (JSON / CSV / PDF).

**Preconditions:**
- User has requested analytics (UC8) and the report batch has finished generating
- User is logged in (valid Appwrite JWT)

**Main Flow (target design):**
1. User selects "Download" for a completed report
2. System displays format options (JSON, CSV, PDF)
3. User selects the desired format
4. System converts the stored report data to the selected format
5. System returns the file and the browser downloads it
6. User receives the file

**Alternative Flows (target design):**
- **A1: Large file**
  - At step 5, if the file is too large to return inline, the system fetches the
    user's email from the Authentication Service (single-user Appwrite Server SDK
    lookup, `users.get(userId)`, since email is never stored locally) and sends a
    download link there. User can download from the link later.

**Postconditions (target design):**
- Analytics file is downloaded to the user's device

**Exceptions (target design):**
- **E1:** Format conversion fails - system offers an alternative format

**Gap vs. current implementation:**
- No `GET` download endpoint exists.
- Only PDF is produced; there is no JSON or CSV serialisation of report data.
- Delivery is push (worker emails the PDF), not pull (user requests it).
- `AnalyticsReportBatch.format` is stored but unused (always `"JSON"`).
