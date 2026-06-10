-- SQL Table Creation Scripts for Healthcare CRM Database
-- Target Database: SQLite (can be adapted for SQL Server/MySQL)

-- 1. Users Table (Auth)
CREATE TABLE IF NOT EXISTS [Users] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [FullName] NVARCHAR(100) NOT NULL,
    [Email] NVARCHAR(150) NOT NULL UNIQUE,
    [PasswordHash] NVARCHAR(MAX) NOT NULL,
    [CreatedAt] DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Index on User Email for faster lookups and unique enforcement
CREATE UNIQUE INDEX IF NOT EXISTS [IX_Users_Email] ON [Users] ([Email]);


-- 2. Patients Table
CREATE TABLE IF NOT EXISTS [Patients] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [FullName] NVARCHAR(100) NOT NULL,
    [Email] NVARCHAR(150) NULL,
    [PhoneNumber] NVARCHAR(20) NOT NULL,
    [DateOfBirth] DATETIME NOT NULL,
    [Gender] NVARCHAR(20) NOT NULL,
    [Address] NVARCHAR(250) NULL,
    [CreatedAt] DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Index on Patient Name for registry search efficiency
CREATE INDEX IF NOT EXISTS [IX_Patients_FullName] ON [Patients] ([FullName]);


-- 3. Appointments Table
CREATE TABLE IF NOT EXISTS [Appointments] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [PatientId] UNIQUEIDENTIFIER NOT NULL,
    [DoctorName] NVARCHAR(100) NOT NULL,
    [AppointmentDate] DATETIME NOT NULL,
    [Reason] NVARCHAR(500) NULL,
    [Status] NVARCHAR(50) NOT NULL DEFAULT 'Scheduled', -- Scheduled, Completed, Cancelled
    [CreatedAt] DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE CASCADE
);

-- Index on foreign key relation for query performance
CREATE INDEX IF NOT EXISTS [IX_Appointments_PatientId] ON [Appointments] ([PatientId]);
