CREATE TABLE IF NOT EXISTS Users (
    Id SERIAL PRIMARY KEY,
    Username TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    Role TEXT NOT NULL,
    FullName TEXT NOT NULL,
    IsActive INTEGER DEFAULT 1,
    Email TEXT,
    Phone TEXT,
    Department TEXT,
    Designation TEXT,
    ProfilePicture TEXT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    LastLoginAt TIMESTAMP
);

CREATE TABLE IF NOT EXISTS Projects (
    Id SERIAL PRIMARY KEY,
    ProjectCode TEXT UNIQUE,
    Name TEXT NOT NULL UNIQUE,
    Description TEXT,
    IsActive INTEGER DEFAULT 1,
    BillingFormula TEXT,
    MasterFilePath TEXT,
    QualityBasePath TEXT
);

CREATE TABLE IF NOT EXISTS ProjectFields (
    Id SERIAL PRIMARY KEY,
    ProjectId INTEGER NOT NULL,
    InternalFieldName TEXT,
    FieldLabel TEXT NOT NULL,
    FieldType TEXT NOT NULL,
    IsRequired INTEGER DEFAULT 0,
    IncludeInBilling INTEGER DEFAULT 0,
    Instructions TEXT,
    SortOrder INTEGER DEFAULT 0,
    FOREIGN KEY(ProjectId) REFERENCES Projects(Id)
);

CREATE TABLE IF NOT EXISTS BillingSummary (
    Id SERIAL PRIMARY KEY,
    UserId INTEGER NOT NULL,
    ProjectId INTEGER NOT NULL,
    Month TEXT,
    TotalObjects INTEGER DEFAULT 0,
    TotalPages INTEGER DEFAULT 0,
    TotalCharacters INTEGER DEFAULT 0,
    TotalAmount REAL DEFAULT 0,
    LastUpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(UserId) REFERENCES Users(Id),
    FOREIGN KEY(ProjectId) REFERENCES Projects(Id),
    UNIQUE(UserId, ProjectId, Month)
);

CREATE TABLE IF NOT EXISTS WorkReports (
    Id SERIAL PRIMARY KEY,
    ProjectId INTEGER NOT NULL,
    UserId INTEGER NOT NULL,
    SubmissionDate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    ObjectId TEXT NOT NULL, 
    BillingAmount REAL DEFAULT 0,
    AttachmentPath TEXT,
    AdminNote TEXT,
    ExtraFields TEXT,
    FOREIGN KEY(ProjectId) REFERENCES Projects(Id),
    FOREIGN KEY(UserId) REFERENCES Users(Id)
);

CREATE TABLE IF NOT EXISTS WorkReportItems (
    Id SERIAL PRIMARY KEY,
    WorkReportId INTEGER NOT NULL,
    FieldId INTEGER NOT NULL,
    Value TEXT,
    FOREIGN KEY(WorkReportId) REFERENCES WorkReports(Id),
    FOREIGN KEY(FieldId) REFERENCES ProjectFields(Id)
);

CREATE TABLE IF NOT EXISTS Attendance (
    Id SERIAL PRIMARY KEY,
    UserId INTEGER NOT NULL,
    Date DATE NOT NULL,
    Status TEXT NOT NULL,
    Remarks TEXT,
    ClockInTime TEXT,
    ClockOutTime TEXT,
    HoursWorked REAL DEFAULT 0,
    FOREIGN KEY(UserId) REFERENCES Users(Id),
    UNIQUE(UserId, Date)
);

CREATE TABLE IF NOT EXISTS Leaves (
    Id SERIAL PRIMARY KEY,
    UserId INTEGER NOT NULL,
    LeaveType TEXT NOT NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,
    Reason TEXT,
    Status TEXT DEFAULT 'Pending',
    FOREIGN KEY(UserId) REFERENCES Users(Id)
);

CREATE TABLE IF NOT EXISTS QualityReports (
    Id SERIAL PRIMARY KEY,
    UserId INTEGER NOT NULL,
    ReportDate DATE NOT NULL,
    Accuracy REAL DEFAULT 0,
    ErrorRate REAL DEFAULT 0,
    ReworkCount INTEGER DEFAULT 0,
    QualityScore REAL DEFAULT 0,
    Remarks TEXT,
    FOREIGN KEY(UserId) REFERENCES Users(Id)
);

CREATE TABLE IF NOT EXISTS AuditLogs (
    Id SERIAL PRIMARY KEY,
    UserId INTEGER,
    Username TEXT,
    Action TEXT NOT NULL,
    Details TEXT,
    Timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS Messages (
    Id SERIAL PRIMARY KEY,
    SenderId INTEGER NOT NULL,
    ReceiverId INTEGER,
    Subject TEXT,
    Content TEXT NOT NULL,
    AttachmentPath TEXT,
    AttachmentType TEXT,
    IsRead INTEGER DEFAULT 0,
    IsBroadcast INTEGER DEFAULT 0,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(SenderId) REFERENCES Users(Id),
    FOREIGN KEY(ReceiverId) REFERENCES Users(Id)
);

CREATE INDEX IF NOT EXISTS idx_messages_receiver ON Messages(ReceiverId);
CREATE INDEX IF NOT EXISTS idx_messages_sender ON Messages(SenderId);

CREATE TABLE IF NOT EXISTS MasterData (
    Id SERIAL PRIMARY KEY,
    ProjectId INTEGER NOT NULL,
    ManifestId TEXT,
    ObjectId TEXT NOT NULL,
    ObjectName TEXT,
    Pages INTEGER DEFAULT 0,
    CharacterCount INTEGER DEFAULT 0,
    StartDate DATE,
    EndDate DATE,
    Status TEXT DEFAULT 'Unassigned',
    Batch TEXT,
    QualityScore REAL DEFAULT 0,
    AssignedUserId INTEGER,
    AssignedUserName TEXT,
    ErrorDetails TEXT,
    ImportDate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(ProjectId) REFERENCES Projects(Id),
    FOREIGN KEY(AssignedUserId) REFERENCES Users(Id)
);

CREATE INDEX IF NOT EXISTS idx_masterdata_project ON MasterData(ProjectId);
CREATE INDEX IF NOT EXISTS idx_masterdata_status ON MasterData(Status);
CREATE INDEX IF NOT EXISTS idx_masterdata_manifest ON MasterData(ManifestId);
CREATE UNIQUE INDEX IF NOT EXISTS idx_masterdata_object ON MasterData(ObjectId);

CREATE TABLE IF NOT EXISTS CompanySettings (
    Id INTEGER PRIMARY KEY CHECK (Id = 1),
    CompanyName TEXT DEFAULT 'My Company',
    CompanyAddress TEXT,
    CompanyPhone TEXT,
    CompanyEmail TEXT,
    TaxId TEXT,
    CurrencySymbol TEXT DEFAULT '₹',
    TimeZone TEXT DEFAULT 'India Standard Time',
    LogoPath TEXT,
    QualityReportsPath TEXT,
    AttachmentsPath TEXT,
    ProfilePicturesPath TEXT,
    QualityThreshold REAL DEFAULT 85,
    LateArrivalThreshold TEXT DEFAULT '09:30',
    EarlyDepartureThreshold TEXT DEFAULT '18:00',
    WorkStartTime TEXT DEFAULT '09:00',
    WorkEndTime TEXT DEFAULT '18:00',
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO CompanySettings (Id, CompanyName) VALUES (1, 'My Company') ON CONFLICT DO NOTHING;
