

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

--  here are the tables I am thinking of we need , feel free to modify them 

-- first we need to drop all the tables in case if they exist before also 
-- make sure to keep this order as it matters when we drop tables that are linked with forignkey
IF OBJECT_ID('TelemetrySample', 'U') IS NOT NULL DROP TABLE TelemetrySample;
IF OBJECT_ID('TransmissionErrorLog', 'U') IS NOT NULL DROP TABLE TransmissionErrorLog;
IF OBJECT_ID('Flight', 'U') IS NOT NULL DROP TABLE Flight;
IF OBJECT_ID('Aircraft', 'U') IS NOT NULL DROP TABLE Aircraft;
IF OBJECT_ID('Channel', 'U') IS NOT NULL DROP TABLE Channel;
IF OBJECT_ID('PacketFormat', 'U') IS NOT NULL DROP TABLE PacketFormat;
IF OBJECT_ID('AppUser', 'U') IS NOT NULL DROP TABLE AppUser;
IF OBJECT_ID('Role', 'U') IS NOT NULL DROP TABLE Role;
GO


-- here are the tables 

-- 1st table

CREATE TABLE Aircraft (
    Aircraft_ID INT PRIMARY KEY,
    Tail_Number NVARCHAR(50) UNIQUE NOT NULL,
    Model_Number NVARCHAR(50),
    Manufacturer_ NVARCHAR(100),
    Year_Of_Make INT
);
GO

-- second table
CREATE TABLE Flight (
    Flight_ID INT PRIMARY KEY,
    Aircraft_ID INT FOREIGN KEY REFERENCES Aircraft(Aircraft_ID),
    Flight_Number NVARCHAR(50),
    Departure_Airport NVARCHAR(100),
    Arrival_Airport NVARCHAR(100),
    Scheduled_Departure DATETIME,
    Scheduled_Arrival DATETIME,
    Actual_Departure DATETIME,
    Actual_Arrival DATETIME,
    Status_ NVARCHAR(50) DEFAULT 'Scheduled',
    Created_At_Utc DATETIME
);
GO

-- 3rd table
CREATE TABLE Channel (
    Channel_ID INT PRIMARY KEY,
    NameOfChanel NVARCHAR(100),
    DescriptionOfChannel NVARCHAR(255)
);
GO

-- 4th table
CREATE TABLE TelemetrySample (
    Sample_ID INT PRIMARY KEY,
    Flight_ID INT FOREIGN KEY REFERENCES Flight(Flight_ID),
    TimeInLocalTimeZone DATETIME,
    Message_Type NVARCHAR(50),
    Channel_ID INT FOREIGN KEY REFERENCES Channel(Channel_ID),
    SeqenceNo INT,
    Payload_Json NVARCHAR(MAX),
    Checksum_Valid BIT,
    Received_At_Utc DATETIME,
    Source_IP_Address NVARCHAR(50)
);
GO

-- 5th table
CREATE TABLE TransmissionErrorLog (
    ErrorLogID INT PRIMARY KEY,
    Flight_ID INT FOREIGN KEY REFERENCES Flight(Flight_ID),
    Occurred_At_Utc DATETIME,
    Error_Code NVARCHAR(50),
    Reason_ NVARCHAR(255),
    Raw_Packet NVARCHAR(MAX),
    Source_IP NVARCHAR(50)
);
GO

-- 6th table
CREATE TABLE PacketFormat (
    MessageFormatID INT PRIMARY KEY,
    Version_Num NVARCHAR(50) UNIQUE,
    JsonMessage NVARCHAR(MAX)
);
GO

-- 7th table
CREATE TABLE Role (
    appUserRole_ID INT PRIMARY KEY,
    Name NVARCHAR(50)
);
GO

-- 8th table
CREATE TABLE AppUser (
    User_ID INT PRIMARY KEY,
    User_name NVARCHAR(100),
    Password_ NVARCHAR(255),
    Is_Active BIT,
    created_Date DATETIME,
    Updated_Date DATETIME,
    appUserRole_ID INT FOREIGN KEY REFERENCES Role(appUserRole_ID)
);
GO

-- List seeded Aircraft
SELECT * FROM dbo.Aircraft;

-- List seeded Flights
SELECT * FROM dbo.Flight;

-- List Channels
SELECT * FROM dbo.Channel;

-- List Telemetry Samples
SELECT * FROM dbo.TelemetrySample;

-- List Transmission Error Logs
SELECT * FROM dbo.TransmissionErrorLog;

-- List Packet Formats
SELECT * FROM dbo.PacketFormat;

-- List Roles
SELECT * FROM dbo.Role;

-- List App Users
SELECT * FROM dbo.AppUser;
GO
