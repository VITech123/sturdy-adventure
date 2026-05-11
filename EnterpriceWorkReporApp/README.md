# Enterprise Work Report Management System

A comprehensive, enterprise-grade WPF desktop application built with **.NET 4.8**. This system is designed to handle employee productivity tracking, automated billing calculations, quality assessments, attendance, and leave management within a unified, premium user interface.

## 🌟 Key Features

### 1. Advanced Project Management
* **Dynamic Form Fields:** Administrators can define custom input fields (Text/Number) specifically required for different projects.
* **Billing Formula Engine:** Calculate task billing dynamically based on submitted field values using parsed Math formulas (e.g., `(CharacterCount / 1000) * Rate`).
* **Integrated Testing:** Test billing formulas with mock variables directly from the project setup screen.

### 2. Work Reports & Analytics
* **Dynamic Work Reports:** Employee submission forms dynamically adapt to show only the required fields assigned to the selected project.
* **Smart Header Import:** Automatically detect and map column headers when pasting data from Excel or tables.
* **Duplicate Prevention:** Seamless verification against Object IDs preventing unintentional overwrites and duplicate billings.
* **Exporting & Reporting:** Export summary and detailed Work Reports seamlessly to PDF and CSV formats.
* **Analytics Board:** Refactored with high-performance Common Table Expressions (CTEs) for accurate, real-time aggregation across millions of records.

### 3. Comprehensive HR Operations
* **Extended User Profiles:** Store detailed demographics including Email, Phone, Place, Education, and Emergency Contact.
* **Profile Photos:** Support for user and admin managed profile pictures.
* **User Archive (Gallery View):** A new grid-based visual archive for administrators to browse employee profiles with photos.
* **Attendance System:** Easy "Punch In/Out" capability, along with break tracking and manual administrative adjustments.
* **Leave Management:** Robust PTO handling with restricted self-only application for employees, overlapping request prevention, and a centralized visual Admin Leave Calendar.
* **Dashboard Personalization:** Every user can individually customize their workspace with unique Themes (Emerald Green, Deep Purple, etc.) and custom Wallpapers.
* **Role-Based Access Control (RBAC):** Granular permissions ensuring Employees and Administrators see completely tailored views and toolsets.

### 4. Quality & Performance Tracking
* **Quality Audits:** Peer reviews and defect tracking integrated per individual Work Report.
* **Leaderboards:** Tabular competitive sorting highlighting high-performers based on Work Volume, Billing generated, and Quality scores (complete with Medals).

---

## 🏛 Architecture & Tech Stack

This application utilizes a standard Monolithic Architecture optimized for Windows Desktop environments. 

### Core Tech Stack
* **Framework**: .NET Framework 4.8 (Compatible with Windows 7 SP1 through Windows 11).
* **UI Technology**: WPF (Windows Presentation Foundation) using native XAML with custom hardware-accelerated animations and gradient styles.
* **Database**: PostgreSQL 13+ (Enterprise-grade relational database for high-concurrency and large datasets).
* **Architecture Pattern**: Code-Behind with abstracted Singleton Services. 

### Application Layers
1. **Presentation Layer (UI)**: Built with pure WPF (`Views/Pages/` and `Views/Dialogs/`). It handles dynamic UI generation and triggers smooth animations.
2. **Business Logic & Services (`Services/`)**: Employs independent structured services (`DatabaseService`, `SessionManager`, `AuditService`, `ReportingService`) to completely abstract logic from the Views.
3. **Data Access Layer**: Facilitated by Dapper, communicating securely with a centralized PostgreSQL database.

### Major Dependencies (NuGet Packages)
* **`Dapper`**: High-performance Micro-ORM used to map SQL queries securely to strongly-typed models (found in `/Models/`).
* **`Npgsql`**: The open-source .NET data provider for PostgreSQL, enabling high-performance database connectivity.
* **`QuestPDF`**: Handles complex tabular generation and detailed itemized structural output for exporting PDF Billing/Work Summaries.
* **`LiveCharts.Wpf`**: Generates vibrant, interactive analytic line/bar charts located on the Dashboard tracking productivity over time.
* **`ExcelDataReader` & `ExcelDataReader.DataSet`**: Used by the Admin Bulk Operations system for securely parsing mass uploads of CSV and Excel spreadsheets.
* **`Newtonsoft.Json`**: Manages underlying settings extraction and complex dynamic data handling.

---

## 🚀 Installation & Setup

Designed for enterprise deployment, this tool utilizes a centralized **PostgreSQL** database ensuring data consistency across multiple LAN-connected workstations.

### Prerequisites (End-Users)
* **OS**: Windows 7 (SP1), Windows 8.1, Windows 10, or Windows 11.
* **Runtime**: [.NET Framework 4.8 Runtime](https://dotnet.microsoft.com/download/dotnet-framework/net48) (pre-installed natively on most modern machines).

### Running The Application (End-Users)
1. Download the latest compiled release build (`EnterpriseWorkReport_Release.zip`).
2. Extract the enclosed folder to a secure local disk location (e.g., `C:\Programs\EnterpriseWorkReport`).
3. Inside the folder, open `EnterpriseWorkReport.exe` to launch the application.

*Note: Ensure the database connection string in `App.config` or service configuration is correctly pointed to your centralized PostgreSQL server.*

---

## 💻 For Developers: Building & Publishing From Source

If you wish to compile the application yourself or extend its logic:

### 1. Restoration & Initialization
Ensure the **.NET Desktop Development** workload is installed within Visual Studio, or simply use the raw `dotnet` CLI tool.
1. Clone the repository and open a terminal at the project root (`d:\EnterpriceWorkReporApp`).
2. Restore all vital NuGet dependencies:
   ```powershell
   dotnet restore EnterpriseWorkReport.csproj
   ```

### 2. Compilation and Release
1. Build the application executing MSBuild logic:
   ```powershell
   dotnet build -c Release
   ```
2. Publish into a standalone Release folder directly:
   ```powershell
   dotnet publish EnterpriseWorkReport.csproj -c Release -o ./publish
   ```
3. The fully compiled `.exe` files and their local DLL bundles will now be constructed cleanly inside the `./publish` directory!
