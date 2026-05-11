You are a senior enterprise software architect and desktop application engineer.

Design and build a professional production-grade desktop application used by a data services company to manage employee productivity, work reports, billing, quality control, and performance analytics.

The system must be fast, reliable, secure, and easy to operate in an office environment.

The application must run on Windows 7 and later versions.

The system should be capable of managing 100+ employees and hundreds of thousands of work report records efficiently.

1. Core Objectives

The application must solve the following business problems:

• Standardize daily work reporting
• Automatically calculate billing from work data
• Track employee attendance and leave
• Track quality performance of employees
• Provide leaderboards to encourage productivity
• Allow admins to upload reports in bulk
• Provide analytics dashboards for management
• Maintain a reliable database with automatic backups

2. Technology Stack

Select technologies compatible with Windows 7 and stable for enterprise desktop applications.

Frontend UI

Electron + React + TypeScript

OR

.NET Framework WPF

The UI must behave like a native desktop business application.

Backend Logic

Node.js LTS compatible with Windows 7
or
.NET Framework backend services.

Database

Use PostgreSQL centralized database server.

Requirements:

• Centralized high-performance database
• Shared access for multiple LAN users
• High performance for large datasets (100k+ records)
• Scheduled automated backups

Database structure example

app_data/
database.db
backups/
logs/
exports/

3. System Modules

The application must include the following modules:

Authentication and User Management

Project Management

Dynamic Project Field Builder

Work Report Management

Billing Management

Attendance Management

Leave Management

Quality Reporting System

Leaderboard and Performance Dashboard

Global Filtering System

Admin Bulk Operations

Analytics Dashboard

Audit Logging System

Automatic Backup and Restore
Resume Management System (OCR)

4. User Roles

Administrator

Full system access.

Capabilities include:

Create and manage projects
Configure report fields
Define billing formulas
Manage employees
Upload reports in bulk
Upload quality reports
Manage attendance and leave
View analytics dashboards
Export reports
Manage system settings
Manage candidate resumes and perform OCR extraction

Employee

Capabilities include:

Submit daily work reports
View personal report history
Mark attendance
Apply leave requests
View leaderboard rankings
View quality performance reports

5. Project Management

Admins can create and manage projects.

Each project contains:

Project name
Project description
Active status
Custom report fields
Billing configuration

Projects define how work reports are structured.

6. Dynamic Project Field Builder

Admins can define custom fields used in work reports.

Features:

Add new fields
Rename fields
Delete fields
Reorder fields

Each field contains:

Field Label
Displayed in report forms.

Field Type

Text
Number

Required Toggle

If enabled, users must fill the field before submitting reports.

Include in Billing Toggle

Marks the field as usable in billing formulas.

Default Field

Every project must include:

Object_ID

Rules:

Object_ID is mandatory
Object_ID cannot be deleted
Used to detect duplicate work entries

Empty State

If no fields exist show message:

"No fields added yet. Click Add Field to create custom fields for this project."

7. Billing Formula System

Each project must support configurable billing formulas.

Admins define how billing is calculated from report data.

Example formula

(CharacterCount / 1000) * Rate

The formula engine must support:

Arithmetic operations
Parentheses
Constants
Field references

Field Reference Helper

The UI must display available numeric fields that can be used in formulas.

Example fields

CharacterCount
RecordCount
PageCount
Rate

Formula Testing Tool

Admins must be able to test formulas before saving.

Example

CharacterCount = 25000
Rate = 4.85

Result

₹121.25

All monetary values must display using Indian Rupee symbol (₹).

Formula Validation

System must validate formulas.

Rules

Only valid field names allowed
Constants allowed
Formula must evaluate successfully
At least one numeric field required

Errors should show:

Inline error messages
Toast notifications

8. Work Report Management

Employees submit daily work reports.

Reports include:

Project
Object_ID
Custom fields
Numeric values
Submission date
Employee name

Features

Duplicate Object_ID detection
Multi-item entry
Editable reports
Report history
CSV import support

9. Billing Management

Billing must be calculated automatically using project formulas.

Each report stores:

Billing amount
Calculation inputs
Final billing value

Admins can generate:

Employee billing reports
Project billing reports
Monthly billing reports

Exports supported

CSV
Excel

10. Attendance Management

Employees mark daily attendance.

Attendance fields

Employee Name
Date
Status
Remarks

Status values

Present
Absent
Half Day
Leave

Admins can perform

Bulk attendance uploads
Attendance corrections
Attendance analytics

11. Leave Management

Employees submit leave requests.

Fields

Employee
Leave type
Start date
End date
Reason

Leave types

Casual Leave
Sick Leave
Paid Leave
Unpaid Leave

Admins approve or reject requests.

12. Quality Reporting System

Admins upload quality performance reports.

Supported file formats

Excel
CSV

Quality metrics include

Accuracy
Error Rate
Rework Count
Quality Score

Reports can be uploaded for:

Single employee
Multiple employees

13. Leaderboard Dashboard

Leaderboard motivates employees by ranking productivity.

Metrics include

Work volume
Billing value
Quality score
Attendance rate

Leaderboards include

Top employees today
Top employees weekly
Top employees monthly

Include ranking badges.

14. Global Filtering System

Admin must be able to apply global filters.

Filters include

Date range
Employee
Project
Report type

Filters must affect

Reports
Billing
Quality reports
Leaderboards
Analytics dashboards

15. Admin Bulk Operations

Admins can perform bulk operations including

Bulk user creation
Bulk report uploads
Bulk quality uploads
Bulk attendance uploads
Bulk report updates

Supported formats

CSV
Excel

16. Analytics Dashboard

Admin dashboard displays performance analytics.

Metrics include

Total work completed
Total billing generated
Top employees
Top projects
Attendance statistics
Quality statistics

Charts include

Daily productivity
Monthly billing
Employee performance

17. Database Schema (SQLite)

Suggested tables

Users
Projects
ProjectFields
WorkReports
WorkReportItems
BillingRecords
Attendance
Leaves
QualityReports
LeaderboardScores
AuditLogs
Resumes

18. Security

Role based access control required.

Features

User login
Password hashing
Admin action logging

19. Audit Logging

System must track all major actions.

Log events include

User login
Report submission
Report edits
Billing calculations
Admin changes

20. Automatic Backup System

Database must be backed up automatically.

Backup frequency

Daily automatic backup.

Backup location

app_data/backups/

Admins must be able to restore backups.

21. Performance Requirements

The system must support

100+ employees
100,000+ reports
Fast filtering and search operations

22. User Interface Layout

Desktop application layout.

Left sidebar navigation

Dashboard
Projects
Work Reports
Billing
Attendance
Leave
Quality Reports
Leaderboard
Admin Tools
Settings

Main panel displays tables and forms.

23. Export Features

Admins must be able to export data.

Supported exports

Reports
Billing
Attendance
Quality metrics

Formats

CSV
Excel

Final Goal

Build a professional enterprise desktop application that manages employee productivity, work reporting, billing automation, attendance tracking, quality control, and performance analytics for a data services company.

The system must run on Windows 7, use an embedded SQLite database, support bulk operations, provide analytics dashboards, and display all billing values using the Indian Rupee symbol (₹).