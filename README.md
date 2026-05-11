# Enterprise Work Report Management System

A comprehensive, enterprise-grade WPF desktop application built with **.NET Framework 4.8**. This system manages employee productivity, work reports, billing, quality control, and performance analytics.

## 🚀 Quick Start

### Prerequisites
- Windows 7 SP1 or later
- .NET Framework 4.8 Runtime (pre-installed on Windows 10/11)

### Running the Application
1. Download the latest `EnterpriseWorkReport_Release_v1.0.x.zip` from the releases folder
2. Extract to a local folder (e.g., `C:\Apps\EnterpriseWorkReport`)
3. Run `EnterpriseWorkReport.exe` from the extracted folder

### Default Login Credentials
| Role | Username | Password |
|------|----------|----------|
| Administrator | `admin` | `admin123` |

**⚠️ Important**: Change the default admin password immediately after first login!

### Build & Publish
```bash
dotnet restore EnterpriceWorkReporApp/EnterpriseWorkReport.csproj
dotnet build EnterpriceWorkReporApp/EnterpriseWorkReport.csproj -c Release
dotnet publish EnterpriceWorkReporApp/EnterpriseWorkReport.csproj -c Release -o ./publish
```

## 🏛️ Application Architecture

### Technology Stack
- **Frontend**: WPF (.NET Framework 4.8) with XAML
- **Database**: SQLite (embedded, local file-based)
- **Architecture Pattern**: Code-Behind with Singleton Services

### Solution Structure
```
EnterpriceWorkReporApp/
├── Views/
│   ├── Pages/          # Main application pages (Dashboard, Projects, Reports, etc.)
│   └── Dialogs/        # Modal dialogs (AddUser, BulkOps, etc.)
├── Services/           # Business logic layer
├── Models/             # Data models and entities
├── Properties/         # App configuration
├── app_data/           # Database and user data
│   ├── database.db     # SQLite database
│   ├── backups/        # Database backups
│   ├── uploads/        # User uploaded files
│   └── wallpapers/     # Dashboard wallpapers
└── Resources/          # Images, icons, styles
```

### Core Services
| Service | Purpose |
|---------|---------|
| `DatabaseService` | SQLite connection, CRUD operations |
| `SessionManager` | User authentication, session state |
| `AuditService` | Activity logging |
| `BackgroundSyncService` | Automated data synchronization |
| `CompanyService` | Company settings management |
| `WallpaperService` | Wallpaper folder management |
| `LanServerService` | LAN browser access server |

## 📋 Complete Feature Documentation

### 1. Authentication System
- **Login**: Secure authentication with PBKDF2 password hashing (100,000 iterations)
- **Session Management**: Role-based access control (Admin/Employee)
- **Password Change**: Self-service password updates with old password verification
- **Profile Photos**: User avatar support with automatic resizing
- **Extended Profiles**: Email, Phone, Place, Education, Emergency Contact

### 2. Project Management
- **Project Creation**: Define project name, description, status, billing formula
- **Dynamic Fields**: Add custom text/number fields per project with drag-drop ordering
- **Billing Formulas**: Math expressions using field values (e.g., `(CharCount/1000)*Rate`)
- **Field Requirements**: Mark fields as mandatory or billing-related
- **Master File Integration**: Sync master file data via file path configuration
- **Status Tracking**: Track objects through workflow (Assigned, In Progress, Shipped, Error)

### 3. Work Reports
- **Dynamic Forms**: Fields adapt based on selected project
- **Multi-Entry**: Submit multiple items per report
- **Duplicate Prevention**: Object_ID uniqueness check with warning
- **Import Support**: Paste from Excel or import CSV/XLSX with smart header detection
- **Export**: PDF and CSV formats with customizable columns
- **Edit Protection**: Admin can edit any report; users can edit their own

### 4. Billing Engine
- **Automatic Calculation**: Based on project formulas (supports +, -, *, /, parentheses)
- **Rate Management**: Per-project billing rates and currency symbols
- **Billing Reports**: Filter by employee, project, date range
- **Export Options**: CSV for accounting systems
- **Formula Testing**: Test billing formulas with sample values before deployment
- **Billing History**: Track calculation inputs and results

### 5. Attendance Management
- **Punch In/Out**: Daily attendance tracking with single-click
- **Status Types**: Present, Absent, Half Day, WFH, Leave
- **Break Tracking**: Session-based time logging
- **Admin Override**: Manual corrections with reason notes
- **Attendance Grid**: Visual calendar view of team attendance
- **Export**: Monthly attendance reports

### 6. Leave Management
- **Leave Types**: Casual, Sick, Paid, Unpaid
- **Request Workflow**: Employee submits → Admin approves/rejects
- **Conflict Detection**: Prevents overlapping requests
- **Calendar View**: Visual scheduling for administrators
- **Email Notifications**: Status change notifications
- **Leave Balance Tracking**: Track available leave days

### 7. Quality Reports
- **Quality Metrics**: Accuracy, Error Rate, Rework Count, Quality Score
- **Score Calculation**: Automated quality scoring (0-100)
- **Quality Audits**: Per-work-report quality assessments
- **Trend Analysis**: Quality improvement tracking over time
- **Low Quality Alerts**: Red highlighting for below-threshold scores
- **Project Quality Summary**: Aggregate quality per project
- **File Import**: Deep folder search for quality CSV/XLSX files

### 8. Leaderboard & Analytics
- **Daily/Weekly/Monthly Rankings**: Productivity competitions
- **Metric Categories**: Work Volume, Billing, Quality Score, Attendance Rate
- **Medals System**: Gold/Silver/Bronze for top performers
- **Dashboard Charts**: LiveCharts for visual analytics
- **Performance Cards**: Quick stats on dashboard (total projects, reports, billing)
- **Object Statistics**: Total objects, pages, characters processed

### 9. Admin Tools
- **User Management**: Create, edit, deactivate employees
- **Bulk Operations**: Import users, reports, quality data via CSV/XLSX
- **Audit Logs**: Complete activity history with timestamps
- **LAN Server**: Browser-based access from other computers (http://server-ip:5050)
- **Backup/Restore**: Database backup with timestamp naming
- **Company Settings**: Configure company name, logo, currency, master names file

### 10. Personalization
- **Theme Selection**: Pastel Blue, Green, Purple, Pink, Orange, Dark Navy, AMOLED Black
- **Dashboard Wallpaper**: Custom background images with preview
- **Logo Upload**: Company branding support (200x200 PNG/JPG recommended)
- **Individual Settings**: Each user can customize independently

### 11. Messaging System
- **Internal Messages**: Send messages between users
- **File Attachments**: Attach files to messages (any type)
- **Folders**: Inbox, Sent, Broadcast (admin-only)
- **Read/Unread Status**: Visual indicators for message status
- **Real-time Updates**: Message list refreshes on new messages

### 12. Resume Management (OCR)
- **Image Upload**: Upload candidate resume images
- **OCR Extraction**: Automatic text extraction using Tesseract
- **Status Tracking**: Ongoing, Pending, Rejected, Relieved
- **Visual Gallery**: Grid view of all resumes with photos
- **Search**: Search by candidate name or content

## 🔐 User Roles & Permissions

| Feature | Employee | Administrator |
|---------|----------|---------------|
| Submit Work Reports | ✅ | ✅ |
| View Personal Reports | ✅ | ✅ |
| Mark Attendance | ✅ | ✅ |
| Apply Leave | ✅ | ✅ |
| View Leaderboard | ✅ | ✅ |
| View Quality Reports | ✅ | ✅ |
| Create/Edit Projects | ❌ | ✅ |
| Manage Users | ❌ | ✅ |
| Admin Settings | ❌ | ✅ |
| Audit Log Access | ❌ | ✅ |
| LAN Server Control | ❌ | ✅ |
| Backup/Restore | ❌ | ✅ |
| Bulk Operations | ❌ | ✅ |
| Resume Management | ❌ | ✅ |
| Messaging | ✅ | ✅ |

## 📦 Latest Release

**Version 1.0.0** - Production Ready
- Build Status: ✅ Passing
- Security Audit: ✅ Passed
- Test Coverage: ✅ 7/7 automated tests
- Database: SQLite embedded (no external server required)

## 📁 Code Details

For detailed feature specifications, see:
- [EnterpriceWorkReporApp/Database/details of the application..md](EnterpriceWorkReporApp/Database/details%20of%20the%20application..md) - Original feature requirements
- [EnterpriceWorkReporApp/README.md](EnterpriceWorkReporApp/README.md) - Technical documentation
- [EnterpriceWorkReporApp/TEST_REPORT.md](EnterpriceWorkReporApp/TEST_REPORT.md) - Test results and security audit