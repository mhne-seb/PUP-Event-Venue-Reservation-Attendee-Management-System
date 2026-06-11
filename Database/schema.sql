-- =============================================
-- PUP Event Venue Reservation System
-- SQL Server Database Schema
-- =============================================

USE master;
GO

IF EXISTS (SELECT name FROM sys.databases WHERE name = 'PUPEventVenueDB')
    DROP DATABASE PUPEventVenueDB;
GO

CREATE DATABASE PUPEventVenueDB;
GO

USE PUPEventVenueDB;
GO

-- =============================================
-- TABLES
-- =============================================

CREATE TABLE AspNetRoles (
    Id NVARCHAR(450) PRIMARY KEY,
    Name NVARCHAR(256),
    NormalizedName NVARCHAR(256),
    ConcurrencyStamp NVARCHAR(MAX)
);

CREATE TABLE AspNetUsers (
    Id NVARCHAR(450) PRIMARY KEY,
    UserName NVARCHAR(256),
    NormalizedUserName NVARCHAR(256),
    Email NVARCHAR(256),
    NormalizedEmail NVARCHAR(256),
    EmailConfirmed BIT NOT NULL,
    PasswordHash NVARCHAR(MAX),
    SecurityStamp NVARCHAR(MAX),
    ConcurrencyStamp NVARCHAR(MAX),
    PhoneNumber NVARCHAR(MAX),
    PhoneNumberConfirmed BIT NOT NULL,
    TwoFactorEnabled BIT NOT NULL,
    LockoutEnd DATETIMEOFFSET,
    LockoutEnabled BIT NOT NULL,
    AccessFailedCount INT NOT NULL,
    -- Custom Fields
    FullName NVARCHAR(200) NOT NULL DEFAULT '',
    StudentNumber NVARCHAR(50),
    OrganizationName NVARCHAR(200),
    Department NVARCHAR(200),
    ProfileImageUrl NVARCHAR(500),
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);

CREATE TABLE AspNetUserRoles (
    UserId NVARCHAR(450) NOT NULL REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    RoleId NVARCHAR(450) NOT NULL REFERENCES AspNetRoles(Id) ON DELETE CASCADE,
    PRIMARY KEY (UserId, RoleId)
);

CREATE TABLE AspNetUserClaims (
    Id INT IDENTITY PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    ClaimType NVARCHAR(MAX),
    ClaimValue NVARCHAR(MAX)
);

CREATE TABLE AspNetUserLogins (
    LoginProvider NVARCHAR(128) NOT NULL,
    ProviderKey NVARCHAR(128) NOT NULL,
    ProviderDisplayName NVARCHAR(MAX),
    UserId NVARCHAR(450) NOT NULL REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    PRIMARY KEY (LoginProvider, ProviderKey)
);

CREATE TABLE AspNetUserTokens (
    UserId NVARCHAR(450) NOT NULL REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    LoginProvider NVARCHAR(128) NOT NULL,
    Name NVARCHAR(128) NOT NULL,
    Value NVARCHAR(MAX),
    PRIMARY KEY (UserId, LoginProvider, Name)
);

CREATE TABLE AspNetRoleClaims (
    Id INT IDENTITY PRIMARY KEY,
    RoleId NVARCHAR(450) NOT NULL REFERENCES AspNetRoles(Id) ON DELETE CASCADE,
    ClaimType NVARCHAR(MAX),
    ClaimValue NVARCHAR(MAX)
);

-- Venues Table
CREATE TABLE Venues (
    VenueId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX),
    Location NVARCHAR(500),
    Capacity INT NOT NULL,
    ImageUrl NVARCHAR(500),
    Amenities NVARCHAR(MAX),   -- JSON or comma-separated
    IsAvailable BIT NOT NULL DEFAULT 1,
    PricePerHour DECIMAL(10,2) DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- Reservations Table
CREATE TABLE Reservations (
    ReservationId INT IDENTITY(1,1) PRIMARY KEY,
    VenueId INT NOT NULL REFERENCES Venues(VenueId),
    OrganizerId NVARCHAR(450) NOT NULL REFERENCES AspNetUsers(Id),
    EventName NVARCHAR(300) NOT NULL,
    EventDescription NVARCHAR(MAX),
    EventType NVARCHAR(100),
    StartDateTime DATETIME2 NOT NULL,
    EndDateTime DATETIME2 NOT NULL,
    ExpectedAttendees INT NOT NULL DEFAULT 0,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Approved, Rejected, Cancelled
    AdminNotes NVARCHAR(MAX),
    RejectionReason NVARCHAR(MAX),
    ApprovedBy NVARCHAR(450) REFERENCES AspNetUsers(Id),
    ApprovedAt DATETIME2,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- Attendees Table
CREATE TABLE Attendees (
    AttendeeId INT IDENTITY(1,1) PRIMARY KEY,
    ReservationId INT NOT NULL REFERENCES Reservations(ReservationId) ON DELETE CASCADE,
    FullName NVARCHAR(200) NOT NULL,
    Email NVARCHAR(256) NOT NULL,
    StudentNumber NVARCHAR(50),
    Department NVARCHAR(200),
    PhoneNumber NVARCHAR(50),
    HasAttended BIT NOT NULL DEFAULT 0,
    RegisteredAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    AttendedAt DATETIME2,
    RegistrationToken NVARCHAR(100) UNIQUE
);

-- Notifications Table
CREATE TABLE Notifications (
    NotificationId INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    Title NVARCHAR(300) NOT NULL,
    Message NVARCHAR(MAX) NOT NULL,
    IsRead BIT NOT NULL DEFAULT 0,
    NotificationType NVARCHAR(100), -- Approved, Rejected, Reminder, General, PaymentSubmitted, PaymentApproved, PaymentRejected
    RelatedReservationId INT REFERENCES Reservations(ReservationId),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- Payments Table
CREATE TABLE Payments (
    PaymentId INT IDENTITY(1,1) PRIMARY KEY,
    ReservationId INT NOT NULL REFERENCES Reservations(ReservationId),
    OrganizerId NVARCHAR(450) NOT NULL REFERENCES AspNetUsers(Id),
    Amount DECIMAL(10,2) NOT NULL DEFAULT 0,
    HourlyRate DECIMAL(10,2) NOT NULL DEFAULT 0,
    DurationHours INT NOT NULL DEFAULT 0,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Submitted, Verified, Approved, Rejected
    ReceiptImageUrl NVARCHAR(500),
    VerificationNotes NVARCHAR(MAX),
    VerifiedBy NVARCHAR(450) REFERENCES AspNetUsers(Id),
    VerifiedAt DATETIME2,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_Payments_ReservationId UNIQUE (ReservationId)
);

-- =============================================
-- INDEXES
-- =============================================
CREATE INDEX IX_Reservations_VenueId ON Reservations(VenueId);
CREATE INDEX IX_Reservations_OrganizerId ON Reservations(OrganizerId);
CREATE INDEX IX_Reservations_Status ON Reservations(Status);
CREATE INDEX IX_Reservations_StartDateTime ON Reservations(StartDateTime);
CREATE INDEX IX_Attendees_ReservationId ON Attendees(ReservationId);
CREATE INDEX IX_Notifications_UserId ON Notifications(UserId);
CREATE INDEX IX_Payments_ReservationId ON Payments(ReservationId);
CREATE INDEX IX_Payments_OrganizerId ON Payments(OrganizerId);
CREATE INDEX IX_Payments_Status ON Payments(Status);
CREATE INDEX IX_AspNetUsers_NormalizedEmail ON AspNetUsers(NormalizedEmail);

-- =============================================
-- SEED DATA
-- =============================================

-- Seed Roles
INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) VALUES
(NEWID(), 'Admin', 'ADMIN', NEWID()),
(NEWID(), 'Organizer', 'ORGANIZER', NEWID());

-- Seed Venues
INSERT INTO Venues (Name, Description, Location, Capacity, ImageUrl, Amenities, IsAvailable, PricePerHour) VALUES
('Main Auditorium', 'The largest venue in PUP, perfect for major university events, graduation ceremonies, and large-scale gatherings.', 'Main Building, Ground Floor', 1500, '/images/venues/auditorium.jpg', 'Stage,Projector,Sound System,Air Conditioning,Parking', 1, 5000),
('Audio Visual Room 1', 'A well-equipped AV room suitable for seminars, workshops, and academic presentations.', 'Main Building, 2nd Floor, Room 201', 80, '/images/venues/avr1.jpg', 'Projector,Whiteboard,Air Conditioning,Microphone', 1, 1500),
('Audio Visual Room 2', 'Similar to AVR 1, ideal for smaller group discussions, trainings, and departmental meetings.', 'Main Building, 2nd Floor, Room 202', 60, '/images/venues/avr2.jpg', 'Projector,Whiteboard,Air Conditioning', 1, 1200),
('University Gymnasium', 'Spacious gymnasium used for sports events, major assemblies, and large gatherings.', 'Sports Complex, East Wing', 3000, '/images/venues/gym.jpg', 'Stage,Sound System,Bleachers,Parking', 1, 4000),
('College of Engineering Conference Room', 'Professional conference room for engineering department events and inter-college meetings.', 'Engineering Building, 3rd Floor', 100, '/images/venues/confroom.jpg', 'Projector,Whiteboard,Air Conditioning,Video Conferencing', 1, 2000),
('Open Grounds / Oval', 'The PUP oval open grounds, suitable for large outdoor events, fairs, and university-wide activities.', 'PUP Campus Grounds', 5000, '/images/venues/oval.jpg', 'Open Space,Electrical Outlets,Nearby Parking', 1, 3000);

PRINT 'Database schema and seed data created successfully.';
GO
