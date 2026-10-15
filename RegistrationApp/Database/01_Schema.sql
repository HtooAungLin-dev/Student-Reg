/* =====================================================================
   RegistrationDb - schema, constraints, seed data
   Target: SQL Server 2012+
   ===================================================================== */
IF DB_ID(N'RegistrationDb') IS NULL CREATE DATABASE RegistrationDb;
GO
USE RegistrationDb;
GO

CREATE TABLE dbo.[Subject]
(
    Id   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Subject PRIMARY KEY,
    Name NVARCHAR(50)      NOT NULL CONSTRAINT UQ_Subject_Name UNIQUE
);

CREATE TABLE dbo.Student
(
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Student PRIMARY KEY,
    NRIC          VARCHAR(9)        NOT NULL,
    Name          NVARCHAR(100)     NOT NULL,
    Gender        CHAR(1)           NOT NULL,
    Birthday      DATE              NOT NULL,
    AvailableDate DATE              NULL,
    CreatedAt     DATETIME2(0)      NOT NULL CONSTRAINT DF_Student_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt     DATETIME2(0)      NULL,
    RowVer        ROWVERSION        NOT NULL,           -- optimistic concurrency token

    CONSTRAINT UQ_Student_NRIC     UNIQUE (NRIC),       -- no duplicate registrations
    CONSTRAINT CK_Student_NRIC     CHECK  (NRIC LIKE '[STFGM][0-9][0-9][0-9][0-9][0-9][0-9][0-9][A-Z]'),
    CONSTRAINT CK_Student_Gender   CHECK  (Gender IN ('M','F')),
    CONSTRAINT CK_Student_Birthday CHECK  (Birthday >= '19000101' AND Birthday <= CAST(GETDATE() AS DATE)),
    CONSTRAINT CK_Student_Name     CHECK  (LEN(LTRIM(RTRIM(Name))) > 0)
);

CREATE TABLE dbo.StudentSubject
(
    StudentId INT NOT NULL CONSTRAINT FK_StudentSubject_Student REFERENCES dbo.Student(Id) ON DELETE CASCADE,
    SubjectId INT NOT NULL CONSTRAINT FK_StudentSubject_Subject REFERENCES dbo.[Subject](Id),
    CONSTRAINT PK_StudentSubject PRIMARY KEY (StudentId, SubjectId)   -- same subject can't be added twice
);
CREATE INDEX IX_StudentSubject_SubjectId ON dbo.StudentSubject(SubjectId);
CREATE INDEX IX_Student_Name ON dbo.Student(Name);

/* Every user action */
CREATE TABLE dbo.ActionLog
(
    Id        BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ActionLog PRIMARY KEY,
    [Action]  VARCHAR(100)   NOT NULL,
    Details   NVARCHAR(1000) NULL,
    UserName  NVARCHAR(100)  NULL,
    IpAddress VARCHAR(45)    NULL,
    CreatedAt DATETIME2(0)   NOT NULL CONSTRAINT DF_ActionLog_CreatedAt DEFAULT SYSUTCDATETIME()
);

/* Every unhandled error */
CREATE TABLE dbo.ErrorLog
(
    Id         BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ErrorLog PRIMARY KEY,
    Message    NVARCHAR(2000) NULL,
    StackTrace NVARCHAR(MAX)  NULL,
    Url        NVARCHAR(500)  NULL,
    UserName   NVARCHAR(100)  NULL,
    IpAddress  VARCHAR(45)    NULL,
    CreatedAt  DATETIME2(0)   NOT NULL CONSTRAINT DF_ErrorLog_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

/* ---------- Seed data ---------- */
INSERT dbo.[Subject](Name) VALUES (N'English'), (N'Math'), (N'Science');

INSERT dbo.Student (NRIC, Name, Gender, Birthday) VALUES
 ('S1234567A', N'Tom',   'M', '2005-01-01'),
 ('S7654321A', N'Green', 'F', '2002-01-01');

INSERT dbo.StudentSubject (StudentId, SubjectId)
SELECT s.Id, sub.Id FROM dbo.Student s CROSS JOIN dbo.[Subject] sub WHERE s.NRIC = 'S1234567A';   -- Tom: 3 subjects
GO
