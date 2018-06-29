CREATE DATABASE  IF NOT EXISTS `ichenconfigdb` /*!40100 DEFAULT CHARACTER SET utf8mb4 */;
USE `ichenconfigdb`;

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table `controllers`
--

DROP TABLE IF EXISTS `controllers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `controllers` (
  `ID` int(11) NOT NULL,
  `OrgId` varchar(100) CHARACTER SET utf8 NOT NULL DEFAULT 'default',
  `IsEnabled` bit(1) NOT NULL DEFAULT b'1',
  `Name` varchar(100) CHARACTER SET utf8 NOT NULL,
  `Type` int(11) NOT NULL,
  `Version` varchar(50) CHARACTER SET utf8 NOT NULL,
  `Model` varchar(100) CHARACTER SET utf8 NOT NULL,
  `IP` varchar(25) CHARACTER SET utf8 NOT NULL,
  `LockIP` varchar(100) CHARACTER SET utf8 DEFAULT NULL,
  `GeoLatitude` double DEFAULT NULL,
  `GeoLongitude` double DEFAULT NULL,
  `Created` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `Modified` datetime DEFAULT NULL,
  PRIMARY KEY (`ID`),
  UNIQUE KEY `IX_Controllers_OrgId_ID` (`OrgId`,`ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table `molds`
--

DROP TABLE IF EXISTS `molds`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `molds` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `Name` varchar(100) CHARACTER SET utf8 NOT NULL,
  `ControllerId` int(11) DEFAULT NULL,
  `IsEnabled` bit(1) NOT NULL DEFAULT b'1',
  `Created` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `Modified` datetime DEFAULT NULL,
  `GUID` char(38) NOT NULL,
  PRIMARY KEY (`ID`),
  UNIQUE KEY `IX_Molds_Controller_Name` (`ControllerId`,`Name`),
  CONSTRAINT `FK_Molds_Controllers` FOREIGN KEY (`ControllerId`) REFERENCES `controllers` (`ID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table `moldsettings`
--

DROP TABLE IF EXISTS `moldsettings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `moldsettings` (
  `MoldId` int(11) NOT NULL,
  `Offset` smallint(6) NOT NULL,
  `Value` smallint(6) NOT NULL,
  `Created` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `Modified` datetime DEFAULT NULL,
  PRIMARY KEY (`MoldId`,`Offset`),
  CONSTRAINT `FK_MoldSettings_Molds` FOREIGN KEY (`MoldId`) REFERENCES `molds` (`ID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table `textmaps`
--

DROP TABLE IF EXISTS `textmaps`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `textmaps` (
  `ID` int(10) NOT NULL AUTO_INCREMENT,
  `Text` varchar(255) CHARACTER SET utf8 NOT NULL,
  PRIMARY KEY (`ID`),
  UNIQUE KEY `IX_TextMaps_Text` (`Text`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table `users`
--

DROP TABLE IF EXISTS `users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `users` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `OrgId` varchar(100) CHARACTER SET utf8 NOT NULL DEFAULT 'default',
  `Password` varchar(50) CHARACTER SET utf8 NOT NULL,
  `Name` varchar(50) CHARACTER SET utf8 NOT NULL,
  `IsEnabled` bit(1) NOT NULL DEFAULT b'1',
  `Filters` int(11) NOT NULL,
  `AccessLevel` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `Created` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `Modified` datetime DEFAULT NULL,
  PRIMARY KEY (`ID`),
  UNIQUE KEY `IX_Users_Org_Name` (`OrgId`,`Name`),
  UNIQUE KEY `IX_Users_Org_Password` (`OrgId`,`Password`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table `users` data
--

LOCK TABLES `users` WRITE;
/*!40000 ALTER TABLE `users` DISABLE KEYS */;
INSERT INTO `users` VALUES (1,'default','chenhsong','admin','',255,0,'2016-06-05 15:51:28',NULL);
/*!40000 ALTER TABLE `users` ENABLE KEYS */;
UNLOCK TABLES;

/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;
