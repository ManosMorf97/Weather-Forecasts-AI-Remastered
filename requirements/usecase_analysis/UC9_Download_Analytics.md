# UC9: Download Analytics

**ID:** UC9  
**Name:** Download Analytics  
**Actor:** End User  
**Description:** User downloads analytics report in structured format (JSON/CSV/PDF).

**Preconditions:**
- User has requested analytics (UC8)
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
