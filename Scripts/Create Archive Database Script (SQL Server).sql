BEGIN TRANSACTION
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO

/****** Table [Alarms] ******/

CREATE TABLE [Alarms]
(
	[OrgId] [nvarchar](100) NOT NULL CONSTRAINT [DF_Alarms_OrgId] DEFAULT (N'default'),
	[Controller] [int] NOT NULL,
	[Time] [datetime2](2) NOT NULL,
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[AlarmName] [varchar](50) NOT NULL,
	[AlarmState] [bit] NOT NULL,
	CONSTRAINT [PK_Alarms] PRIMARY KEY CLUSTERED 
	(
		[OrgId] ASC,
		[Controller] ASC,
		[Time] ASC,
		[ID] ASC
	)
)
GO

/****** Table [AuditTrail] ******/

CREATE TABLE [AuditTrail]
(
	[OrgId] [nvarchar](100) NOT NULL CONSTRAINT [DF_AuditTrail_OrgId] DEFAULT (N'default'),
	[Controller] [int] NOT NULL,
	[Time] [datetime2](2) NOT NULL,
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[Operator] [int] NOT NULL CONSTRAINT [DF_AuditTrail_Operator] DEFAULT ((0)),
	[VariableName] [varchar](50) NOT NULL,
	[Value] [real] NOT NULL,
	[OldValue] [real] NULL DEFAULT (NULL),
	CONSTRAINT [PK_AuditTrail] PRIMARY KEY CLUSTERED 
	(
		[OrgId] ASC,
		[Controller] ASC,
		[Time] ASC,
		[ID] ASC
	)
)
GO

/****** Table [CycleData] ******/

CREATE TABLE [CycleData]
(
	[OrgId] [nvarchar](100) NOT NULL CONSTRAINT [DF_CycleData_OrgId] DEFAULT (N'default'),
	[Controller] [int] NOT NULL,
	[Time] [datetime2](2) NOT NULL,
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[Operator] [int] NOT NULL CONSTRAINT [DF_CycleData_Operator] DEFAULT ((0)),
	[OpMode] [tinyint] NOT NULL CONSTRAINT [DF_CycleData_OpMode] DEFAULT (NULL),
	[JobMode] [tinyint] NOT NULL CONSTRAINT [DF_CycleData_JobMode] DEFAULT (NULL),
	[JobCard] [nvarchar](100) NULL CONSTRAINT [DF_CycleData_JobCard] DEFAULT (NULL),
	[Mold] [nvarchar](100) NULL CONSTRAINT [DF_CycleData_Mold] DEFAULT (NULL),
	CONSTRAINT [PK_CycleData] PRIMARY KEY NONCLUSTERED ( [ID] ASC )
)
GO

CREATE UNIQUE CLUSTERED INDEX [IX_CycleData] ON [CycleData]
(
	[OrgId] ASC,
	[Controller] ASC,
	[Time] ASC,
	[ID] ASC
)
GO

/****** Table [CycleDataValues] ******/

CREATE TABLE [CycleDataValues]
(
	[ID] [int] NOT NULL,
	[VariableName] [varchar](50) NOT NULL,
	[Value] [real] NOT NULL,
	CONSTRAINT [PK_CycleDataValues] PRIMARY KEY CLUSTERED ( [ID] ASC, [VariableName] ASC )
)
GO

ALTER TABLE [CycleDataValues] WITH CHECK ADD CONSTRAINT [FK_CycleData] FOREIGN KEY([ID])
REFERENCES [CycleData] ([ID])
GO

/****** Table [Events] ******/

CREATE TABLE [Events]
(
	[OrgId] [nvarchar](100) NOT NULL CONSTRAINT [DF_Events_OrgId] DEFAULT (N'default'),
	[Controller] [int] NOT NULL,
	[Time] [datetime2](2) NOT NULL,
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[Operator] [int] NULL CONSTRAINT [DF_Events_Operator] DEFAULT (NULL),
	[Connected] [bit] NULL CONSTRAINT [DF_Events_Connected] DEFAULT (NULL),
	[IP] [varchar](25) NULL CONSTRAINT [DF_Events_IP] DEFAULT (NULL),
	[GeoLatitude] float NULL CONSTRAINT [DF_Events_GeoLatitude] DEFAULT (NULL),
	[GeoLongitude] float NULL CONSTRAINT [DF_Events_GeoLongitude] DEFAULT (NULL),
	[OpMode] [tinyint] NULL CONSTRAINT [DF_Events_OpMode] DEFAULT (NULL),
	[JobMode] [tinyint] NULL CONSTRAINT [DF_Events_JobMode] DEFAULT (NULL),
	[JobCard] [nvarchar](100) NULL CONSTRAINT [DF_Events_JobCard] DEFAULT (NULL),
	[Mold] [uniqueidentifier] NULL CONSTRAINT [DF_Events_Mold] DEFAULT (NULL),
	CONSTRAINT [PK_Events] PRIMARY KEY CLUSTERED 
	(
		[OrgId] ASC,
		[Controller] ASC,
		[Time] ASC,
		[ID] ASC
	)
)
GO

COMMIT TRANSACTION
GO
