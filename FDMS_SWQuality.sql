/* ============================================================
   FDMS Database Deployment Script
   PURPOSE: Create core tables, relationships, seed data, queries
   TARGET: Azure SQL Database (FDMS_SWQuality)
   AUTHOR: Mher Keshishian
   DATE:   2025-11-25
   ============================================================ */


-- Database for Software Quality Final Project 


-- Create the database for the Fog Lamp Assembly project
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'FlightDataManagementSystem')
BEGIN
    CREATE DATABASE FlightDataManagementSystem;
END
GO

-- Switch to the newly created database
USE FlightDataManagementSystem;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO





/* ------------------------------------------------------------
   1. Drop existing objects (for re-deploys)
   ------------------------------------------------------------ */

IF OBJECT_ID('dbo.GetLatestTelemetryForFlight', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetLatestTelemetryForFlight;
GO

IF OBJECT_ID('dbo.SearchTelemetry', 'P') IS NOT NULL
    DROP PROCEDURE dbo.SearchTelemetry;
GO

IF OBJECT_ID('dbo.TelemetrySample', 'U') IS NOT NULL
    DROP TABLE dbo.TelemetrySample;
GO

IF OBJECT_ID('dbo.TransmissionErrorLog', 'U') IS NOT NULL
    DROP TABLE dbo.TransmissionErrorLog;
GO

IF OBJECT_ID('dbo.Flight', 'U') IS NOT NULL
    DROP TABLE dbo.Flight;
GO

IF OBJECT_ID('dbo.Aircraft', 'U') IS NOT NULL
    DROP TABLE dbo.Aircraft;
GO

IF OBJECT_ID('dbo.Channel', 'U') IS NOT NULL
    DROP TABLE dbo.Channel;
GO

/* ------------------------------------------------------------
   2. Create tables
   ------------------------------------------------------------ */

-- 2.1 Channel (lookup)
CREATE TABLE dbo.Channel
(
    ChannelId      INT IDENTITY(1,1) PRIMARY KEY,
    ChannelName    NVARCHAR(50)  NOT NULL,
    ChannelCode    NVARCHAR(10)  NOT NULL,
    Description    NVARCHAR(200) NULL
);
GO

-- 2.2 Aircraft
CREATE TABLE dbo.Aircraft
(
    AircraftId     INT IDENTITY(1,1) PRIMARY KEY,
    TailNumber     NVARCHAR(20) NOT NULL,
    Model          NVARCHAR(50) NOT NULL,
    Manufacturer   NVARCHAR(50) NULL
);
GO

-- 2.3 Flight
CREATE TABLE dbo.Flight
(
    FlightId       INT IDENTITY(1,1) PRIMARY KEY,
    AircraftId     INT          NOT NULL,
    FlightCode     NVARCHAR(20) NOT NULL,
    DepartureTime  DATETIME2(0) NOT NULL,
    ArrivalTime    DATETIME2(0) NULL,

    CONSTRAINT FK_Flight_Aircraft
        FOREIGN KEY (AircraftId)
        REFERENCES dbo.Aircraft (AircraftId)
        ON DELETE CASCADE
);
GO

-- 2.4 TelemetrySample
CREATE TABLE dbo.TelemetrySample
(
    TelemetrySampleId INT IDENTITY(1,1) PRIMARY KEY,
    FlightId          INT          NOT NULL,
    SampleTime        DATETIME2(0) NOT NULL,

    -- Generic numeric fields (map them to columns from the .txt files)
    Field1            FLOAT        NULL,
    Field2            FLOAT        NULL,
    Field3            FLOAT        NULL,
    Field4            FLOAT        NULL,
    Field5            FLOAT        NULL,
    Field6            FLOAT        NULL,
    Field7            FLOAT        NULL,

    ChannelId         INT          NULL,

    CONSTRAINT FK_TelemetrySample_Flight
        FOREIGN KEY (FlightId)
        REFERENCES dbo.Flight (FlightId)
        ON DELETE CASCADE,

    CONSTRAINT FK_TelemetrySample_Channel
        FOREIGN KEY (ChannelId)
        REFERENCES dbo.Channel (ChannelId)
);
GO

-- 2.5 TransmissionErrorLog
CREATE TABLE dbo.TransmissionErrorLog
(
    TransmissionErrorId INT IDENTITY(1,1) PRIMARY KEY,
    FlightId            INT          NULL,
    ErrorTime           DATETIME2(0) NOT NULL,
    ErrorCode           INT          NULL,
    ErrorMessage        NVARCHAR(4000) NULL,

    CONSTRAINT FK_TransmissionErrorLog_Flight
        FOREIGN KEY (FlightId)
        REFERENCES dbo.Flight (FlightId)
);
GO

-- 2.6 SystemLogs table
CREATE TABLE dbo.SystemLogs
(
    LogId       INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Timestamp] DATETIME2(0)      NOT NULL
                 CONSTRAINT DF_SystemLogs_Timestamp DEFAULT SYSDATETIME(),
    [Level]     NVARCHAR(20)      NOT NULL,
    [Source]    NVARCHAR(100)     NOT NULL,
    [Message]   NVARCHAR(4000)    NOT NULL
);
GO

-- 2.7 SystemLogsArchive table
CREATE TABLE dbo.SystemLogsArchive
(
    LogId       INT            NOT NULL,
    [Timestamp] DATETIME2(0)   NOT NULL,
    [Level]     NVARCHAR(20)   NOT NULL,
    [Source]    NVARCHAR(100)  NOT NULL,
    [Message]   NVARCHAR(4000) NOT NULL,
    ArchivedDate DATETIME2(0)  NOT NULL
);
GO

-- Drop the table if it already exists
IF OBJECT_ID('dbo.AircraftTransmitterPackets', 'U') IS NOT NULL
    DROP TABLE dbo.AircraftTransmitterPackets;
GO

-- Create the table to store the packet we will recieve from AircraftTransmit
CREATE TABLE dbo.AircraftTransmitterPackets
(
    TelemetryId INT IDENTITY(1,1) PRIMARY KEY,          -- unique row ID
    SampleTimeStamp DATETIME2(0) NOT NULL,              -- timestamp when sample was received
    TailNumber NVARCHAR(50) NOT NULL,                   -- aircraft identifier
    [Checksum] INT NOT NULL,                            -- packet checksum
    Altitude FLOAT NOT NULL,                            -- altitude in feet
    Pitch FLOAT NOT NULL,                               -- pitch angle in degrees
    Bank FLOAT NOT NULL,                                -- bank angle in degrees
    AccelX FLOAT NOT NULL,                              -- X-axis acceleration
    AccelY FLOAT NOT NULL,                              -- Y-axis acceleration
    AccelZ FLOAT NOT NULL                               -- Z-axis acceleration
);
GO

/* ------------------------------------------------------------
   3. Seed data (Channels + sample Aircraft/Flight)
   ------------------------------------------------------------ */

-- 3.1 Channels
INSERT INTO dbo.Channel (ChannelName, ChannelCode, Description)
VALUES
    (N'Pitch / Roll / Yaw', N'ATT', N'Attitude / orientation'),
    (N'Altitude',           N'ALT', N'Barometric altitude'),
    (N'Engine / Airspeed',  N'ENG', N'Engine or airspeed related'),
    (N'Fuel Level',         N'FUEL', N'Fuel quantity monitoring'),
    (N'System Health',      N'SYS',  N'Overall system diagnostics'),
    (N'GPS Position',       N'GPS',  N'Latitude / Longitude tracking'),
    (N'Acceleration',       N'ACC',  N'Linear acceleration data'),
    (N'Weight',             N'WGT',  N'Aircraft gross weight'),
    (N'Temperature',        N'TEMP', N'Environmental or engine temperature'),
    (N'Pressure',           N'PRS',  N'Cabin or system pressure');
GO


-- 3.2 Aircraft (three examples to match your txt files)
INSERT INTO dbo.Aircraft (TailNumber, Model, Manufacturer)
VALUES
    (N'C-FGAX', N'Test Model A', N'FDMS Demo'),
    (N'C-GEFC', N'Test Model B', N'FDMS Demo'),
    (N'C-QWWT', N'Test Model C', N'FDMS Demo'),
    (N'C-HJKL', N'Test Model D', N'FDMS Demo'),
    (N'C-MNOP', N'Test Model E', N'FDMS Demo'),
    (N'C-QRST', N'Test Model F', N'FDMS Demo'),
    (N'C-UVWX', N'Test Model G', N'FDMS Demo'),
    (N'C-YZAB', N'Test Model H', N'FDMS Demo'),
    (N'C-CDEF', N'Test Model I', N'FDMS Demo'),
    (N'C-GHIJ', N'Test Model J', N'FDMS Demo');
GO


-- 3.3 Flight (one flight per aircraft for testing)
-- One flight per aircraft for testing
INSERT INTO dbo.Flight (AircraftId, FlightCode, DepartureTime, ArrivalTime)
SELECT AircraftId, N'TEST-' + TailNumber + N'-1', SYSDATETIME(), NULL
FROM dbo.Aircraft;
GO

-- 3.4 seed sample log into SystemLogs
INSERT INTO dbo.SystemLogs ([Timestamp], [Level], [Source], [Message])
VALUES
    (SYSDATETIME(), N'INFO',  N'Dashboard',   N'Ground terminal UI started.'),
    (SYSDATETIME(), N'INFO',  N'Network',     N'Listening for telemetry on port 5000.'),
    (SYSDATETIME(), N'WARN',  N'Database',    N'Initial connection attempt failed; retrying.'),
    (SYSDATETIME(), N'ERROR', N'PacketParse', N'Checksum mismatch for telemetry packet ID 25.');
GO

/* ------------------------------------------------------------
   4. Basic queries (stored procedures)
   ------------------------------------------------------------ */

-- 4.1 Get latest telemetry for a flight
CREATE PROCEDURE dbo.GetLatestTelemetryForFlight
    @FlightId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        ts.TelemetrySampleId,
        ts.FlightId,
        ts.SampleTime,
        ts.Field1,
        ts.Field2,
        ts.Field3,
        ts.Field4,
        ts.Field5,
        ts.Field6,
        ts.Field7,
        ts.ChannelId
    FROM dbo.TelemetrySample ts
    WHERE ts.FlightId = @FlightId
    ORDER BY ts.SampleTime DESC, ts.TelemetrySampleId DESC;
END;
GO

-- 4.2 Search telemetry by FlightID + time range + channel
CREATE PROCEDURE dbo.SearchTelemetry
    @FlightId    INT,
    @FromTime    DATETIME2(0) = NULL,
    @ToTime      DATETIME2(0) = NULL,
    @ChannelId   INT          = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ts.TelemetrySampleId,
        ts.FlightId,
        ts.SampleTime,
        ts.Field1,
        ts.Field2,
        ts.Field3,
        ts.Field4,
        ts.Field5,
        ts.Field6,
        ts.Field7,
        ts.ChannelId
    FROM dbo.TelemetrySample ts
    WHERE ts.FlightId = @FlightId
      AND (@FromTime IS NULL OR ts.SampleTime >= @FromTime)
      AND (@ToTime   IS NULL OR ts.SampleTime <= @ToTime)
      AND (@ChannelId IS NULL OR ts.ChannelId = @ChannelId)
    ORDER BY ts.SampleTime, ts.TelemetrySampleId;
END;
GO

SELECT * FROM dbo.Aircraft;
SELECT * FROM dbo.Flight;
SELECT * FROM dbo.Channel;
SELECT * FROM dbo.TelemetrySample;
SELECT * FROM dbo.TransmissionErrorLog;

SELECT * FROM dbo.AircraftTransmitterPackets

