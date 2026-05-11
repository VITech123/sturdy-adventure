# Application Feature Details

## Administrator Capabilities

### 1. User Management
- Add user details (FullName, Email, Phone, Place, Education, Emergency Number, Join Date)
- Set username, password, and profile photo
- View and edit attendance details of all users
- View and edit work reports of all users
- Activate/deactivate users

### 2. Quality Reports
- View quality reports for all users
- Filter by individual, project, or date
- View average quality per user for each project
- Deep folder search to fetch quality CSV/XLSX files
- Highlight low-quality scores in red (danger zone)
- Highlight perfect scores

### 3. Statistics Dashboard
- View statistics for all projects
- Filter by date, project, or manifest
- Shows: manifest count, objects per manifest, total pages, characters per project
- Track status: Shipped, Unassigned, Hold, Error
- Visual chart views with LiveCharts
- Identify error owners

### 4. Billing Management
- View billing details for all users
- Filter by individual, project, or date range
- See objects completed, pages, characters per project
- Create invoices for individuals
- Send notifications before finalizing

### 5. Messaging System
- Internal messaging with other users
- Attach files and download attachments
- Broadcast messages to all users (admin-only)
- View inbox and sent folders

### 6. Bulk Operations
- Add multiple users via CSV/Excel
- Upload work reports in bulk
- Import quality reports in bulk
- Template downloads for all import types

### 7. Settings & Configuration
- Company settings (name, logo, currency)
- LAN server configuration
- Backup and restore database
- Audit log viewer

---

## Employee Capabilities

### 1. Profile Management
- Edit profile details (Email, Phone, Place, Education, Emergency Contact)
- Upload/change profile picture
- Change password

### 2. Attendance
- Clock in and clock out once per day
- Cannot accidentally clock in again if already clocked in
- View personal attendance history

### 3. Work Reports
- Submit work reports for multiple projects
- Upload via template, CSV, XLSX files
- Smart header detection for automatic column mapping
- View and download personal reports
- Duplicate Object_ID prevention with warning

### 4. Leave Management
- Apply for leave (only for self, no name dropdown)
- View leave history and status (Pending/Approved/Rejected)
- Receive notifications for status changes

### 5. Quality Reports
- View personal quality scores
- See average quality per project
- Track improvement over time

### 6. Leaderboard
- View daily, weekly, monthly rankings
- See work count-based and quality-based leaderboards
- View medal positions (Top 3)

### 7. Personalization
- Customize dashboard theme
- Set custom background wallpaper

---

## Core Feature Details

### Attendance System
- One clock-in and clock-out per day per user
- Automatic prevention of multiple clock-ins
- Admin can view/edit all attendance
- Users can only view their own

### Leave System
- Leave types: Casual, Sick, Paid, Unpaid
- Users apply only for themselves
- Admin can approve/reject and apply on behalf of any employee
- Overlapping request prevention

### Work Reports
- Each project has unique fields and billing formula
- Example: Gamma123 fields = Object_ID, Pages, CharacterCount
- Formula: `(CharacterCount/1000)*Rate`
- Multiple project work reports allowed
- Template download available
- Duplicate Object_ID detection with permissions to overwrite

### Projects
- Admin configures projects with unique fields and billing formulas
- Master file path integration for project status tracking
- Master file table structure:
  | Date | Manifest_ID | Object_ID | Pages | Name | Start_date | Charactercount | End_Date | Status | Batch | Quality | Audit |

### Billing Page
- Users view their own billing per date
- Admin views all billing details:
  - Individual names
  - Projects
  - Objects, pages, characters per project
  - Date-wise totals and project-wise details
- Invoice creation capability

### Message System
- Send and receive internal messages
- Date-wise message organization
- File attachments supported
- Download attached files

### Quality Reports
- Admin configures project quality report paths
- Deep folder search for summary report files
- Points out low-quality performers in red
- Identifies perfect scores

### Leader Board
- Two types: Quality-based and Work Count-based
- Daily, weekly, monthly rankings
- Top 3 performers highlighted

### Database
- Centralized SQLite database
- All Windows 7 machines connect via LAN
- No external website access for security

---

## Technical Requirements

- **Target OS**: Windows 7 SP1 and later
- **Framework**: .NET Framework 4.8
- **Database**: SQLite (embedded)
- **Security**: No external website access, LAN-only operation
- **Browser Support**: Chrome, Firefox, Internet Explorer (for LAN server access)