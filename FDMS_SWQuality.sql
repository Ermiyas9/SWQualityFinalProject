/* ============================================================
   FDMS Database Deployment Script
   PURPOSE: Create core tables, relationships, seed data, queries
   TARGET: Azure SQL Database (FDMS_SWQuality)
   AUTHOR: Mher Keshishian
   DATE:   2025-11-25
   ============================================================ */

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

/* ------------------------------------------------------------
   3. Seed data (Channels + sample Aircraft/Flight)
   ------------------------------------------------------------ */

-- 3.1 Channels
INSERT INTO dbo.Channel (ChannelName, ChannelCode, Description)
VALUES
    (N'Pitch / Roll / Yaw', N'ATT', N'Attitude / orientation'),
    (N'Altitude',           N'ALT', N'Barometric altitude'),
    (N'Engine / Airspeed',  N'ENG', N'Engine or airspeed related'),
    (N'Generic',            N'GEN', N'Generic telemetry channel');
GO

-- 3.2 Aircraft (three examples to match your txt files)
INSERT INTO dbo.Aircraft (TailNumber, Model, Manufacturer)
VALUES
    (N'C-FGAX', N'Test Model A', N'FDMS Demo'),
    (N'C-GEFC', N'Test Model B', N'FDMS Demo'),
    (N'C-QWWT', N'Test Model C', N'FDMS Demo');
GO

-- 3.3 Flight (one flight per aircraft for testing)
INSERT INTO dbo.Flight (AircraftId, FlightCode, DepartureTime, ArrivalTime)
SELECT AircraftId, N'TEST-' + TailNumber + N'-1', SYSDATETIME(), NULL
FROM dbo.Aircraft;
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

/* ------------------------------------------------------------
   5. Quick checks
   ------------------------------------------------------------ */

-- List seeded Aircraft
SELECT * FROM dbo.Aircraft;

-- List seeded Flights
SELECT * FROM dbo.Flight;

-- List Channels
SELECT * FROM dbo.Channel;
GO
