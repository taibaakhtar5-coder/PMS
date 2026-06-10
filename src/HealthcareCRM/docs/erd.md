# Database Design Schema (ERD)

This document contains the schema details and relationships for the Healthcare CRM database.

## Entity Relationship Diagram (ERD)

```mermaid
erDiagram
    Users {
        Guid Id PK
        string FullName
        string Email UK
        string PasswordHash
        DateTime CreatedAt
    }
    Patients {
        Guid Id PK
        string FullName
        string Email
        string PhoneNumber
        DateTime DateOfBirth
        string Gender
        string Address
        DateTime CreatedAt
    }
    Appointments {
        Guid Id PK
        Guid PatientId FK
        string DoctorName
        DateTime AppointmentDate
        string Reason
        string Status
        DateTime CreatedAt
    }

    Patients ||--o{ Appointments : "schedules"
```

---

## Database Table Schemas

### 1. Users Table
Stores physician login credentials and account information.

| Column Name | Data Type | Nullable | Constraints | Description |
|:---|:---|:---:|:---|:---|
| `Id` | `UniqueIdentifier` | No | Primary Key | Unique identifier for each user |
| `FullName` | `NVarChar(100)` | No | - | Full name of the user/physician |
| `Email` | `NVarChar(150)` | No | Unique Key | Unique login email address |
| `PasswordHash` | `NVarChar(Max)` | No | - | Securely hashed user password |
| `CreatedAt` | `DateTime` | No | Default: Now | Timestamp of profile registration |

### 2. Patients Table
Stores patient clinical charts and basic contacts.

| Column Name | Data Type | Nullable | Constraints | Description |
|:---|:---|:---:|:---|:---|
| `Id` | `UniqueIdentifier` | No | Primary Key | Unique identifier for each patient |
| `FullName` | `NVarChar(100)` | No | - | Full name of the patient |
| `Email` | `NVarChar(150)` | Yes | - | Optional email address |
| `PhoneNumber` | `NVarChar(20)` | No | - | Active phone contact |
| `DateOfBirth` | `DateTime` | No | - | Birth date (used to compute age) |
| `Gender` | `NVarChar(20)` | No | - | Male, Female, or Other |
| `Address` | `NVarChar(250)` | Yes | - | Primary residence address |
| `CreatedAt` | `DateTime` | No | Default: Now | Date chart was registered |

### 3. Appointments Table
Tracks medical appointments scheduled by patients.

| Column Name | Data Type | Nullable | Constraints | Description |
|:---|:---|:---:|:---|:---|
| `Id` | `UniqueIdentifier` | No | Primary Key | Unique identifier for each appointment |
| `PatientId` | `UniqueIdentifier` | No | Foreign Key | References `Patients.Id` (Cascade delete) |
| `DoctorName` | `NVarChar(100)` | No | - | Name of the assigned physician |
| `AppointmentDate` | `DateTime` | No | - | Day and time of the appointment |
| `Reason` | `NVarChar(500)` | Yes | - | Chief complaints / reason for visit |
| `Status` | `NVarChar(50)` | No | Default: 'Scheduled' | Scheduled, Completed, or Cancelled |
| `CreatedAt` | `DateTime` | No | Default: Now | Timestamp of booking |
