CREATE TABLE Controllers (
  ID int PRIMARY KEY,
  OrgId varchar(100) NOT NULL DEFAULT 'default',
  IsEnabled bit NOT NULL DEFAULT 1,
  Name varchar(100) NOT NULL,
  Type int NOT NULL,
  Version varchar(50) NOT NULL,
  Model varchar(100) NOT NULL,
  IP varchar(25) NOT NULL,
  LockIP varchar(100) DEFAULT NULL,
  GeoLatitude double DEFAULT NULL,
  GeoLongitude double DEFAULT NULL,
  Created datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  Modified datetime DEFAULT NULL
);

CREATE UNIQUE INDEX IX_Controllers_OrgId_ID ON Controllers (OrgId,ID);

CREATE TABLE Molds (
  ID INTEGER PRIMARY KEY AUTOINCREMENT,
  Name varchar(100) NOT NULL,
  ControllerId int DEFAULT NULL,
  IsEnabled bit NOT NULL DEFAULT 1,
  Created datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  Modified datetime DEFAULT NULL,
  GUID char(38) NOT NULL,
  FOREIGN KEY (ControllerId) REFERENCES controllers (ID) ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE UNIQUE INDEX IX_Molds_Controller_Name ON Molds (ControllerId,Name);

CREATE TABLE MoldSettings (
  MoldId int NOT NULL,
  Offset smallint NOT NULL,
  Value smallint NOT NULL,
  Created datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  Modified datetime DEFAULT NULL,
  PRIMARY KEY (MoldId, Offset),
  FOREIGN KEY (MoldId) REFERENCES molds (ID) ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE TextMaps (
  ID INTEGER PRIMARY KEY AUTOINCREMENT,
  Text varchar(255) NOT NULL
);

CREATE UNIQUE INDEX IX_TextMaps_Text ON TextMaps (Text);

CREATE TABLE Users (
  ID INTEGER PRIMARY KEY AUTOINCREMENT,
  OrgId varchar(100) NOT NULL DEFAULT 'default',
  Password varchar(50) NOT NULL,
  Name varchar(50) NOT NULL,
  IsEnabled bit NOT NULL DEFAULT 1,
  Filters int NOT NULL,
  AccessLevel tinyint unsigned DEFAULT 0,
  Created datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  Modified datetime DEFAULT NULL
);

CREATE UNIQUE INDEX IX_Users_Org_Name ON Users (OrgId,Name);

CREATE UNIQUE INDEX IX_Users_Org_Password ON Users (OrgId,Password);

INSERT INTO Users (ID, OrgId, Password, Name, IsEnabled, Filters, AccessLevel)
VALUES (1,'default','chenhsong','admin',1,255,0);


