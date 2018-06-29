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
-- Table `alarms`
--

DROP TABLE IF EXISTS `alarms`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `alarms` (
  `OrgId` varchar(100) NOT NULL DEFAULT 'default',
  `Controller` int(11) NOT NULL,
  `Time` datetime NOT NULL,
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `AlarmName` varchar(50) NOT NULL,
  `AlarmState` bit(1) NOT NULL,
  PRIMARY KEY (`ID`),
  KEY `IX_Org_Controller_Time` (`OrgId`,`Controller`,`Time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table `audittrail`
--

DROP TABLE IF EXISTS `audittrail`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `audittrail` (
  `OrgId` varchar(100) NOT NULL DEFAULT 'default',
  `Controller` int(11) NOT NULL,
  `Time` datetime NOT NULL,
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `Operator` int(11) NOT NULL DEFAULT '0',
  `VariableName` varchar(50) NOT NULL,
  `Value` double NOT NULL,
  `OldValue` double DEFAULT NULL,
  PRIMARY KEY (`ID`),
  KEY `IX_Org_Controller_Time` (`OrgId`,`Controller`,`Time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table `cycledata`
--

DROP TABLE IF EXISTS `cycledata`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `cycledata` (
  `OrgId` varchar(100) NOT NULL DEFAULT 'default',
  `Controller` int(11) NOT NULL,
  `Time` datetime NOT NULL,
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `Operator` int(11) NOT NULL DEFAULT '0',
  `OpMode` tinyint(3) unsigned DEFAULT NULL,
  `JobMode` tinyint(3) unsigned DEFAULT NULL,
  `JobCard` varchar(100) DEFAULT NULL,
  `Mold` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`ID`),
  KEY `IX_Org_Controller_Time` (`OrgId`,`Controller`,`Time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table `cycledatavalues`
--

DROP TABLE IF EXISTS `cycledatavalues`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `cycledatavalues` (
  `ID` int(11) NOT NULL,
  `VariableName` varchar(50) NOT NULL,
  `Value` double NOT NULL,
  PRIMARY KEY (`ID`,`VariableName`),
  CONSTRAINT `FK_CycleData` FOREIGN KEY (`ID`) REFERENCES `cycledata` (`ID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table `events`
--

DROP TABLE IF EXISTS `events`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `events` (
  `OrgId` varchar(100) NOT NULL DEFAULT 'default',
  `Controller` int(11) NOT NULL,
  `Time` datetime NOT NULL,
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `Operator` int(11) DEFAULT '0',
  `Connected` bit(1) DEFAULT NULL,
  `IP` varchar(25) DEFAULT NULL,
  `GeoLatitude` double DEFAULT NULL,
  `GeoLongitude` double DEFAULT NULL,
  `OpMode` tinyint(3) unsigned DEFAULT NULL,
  `JobMode` tinyint(3) unsigned DEFAULT NULL,
  `JobCard` varchar(100) DEFAULT NULL,
  `Mold` char(38) DEFAULT NULL,
  PRIMARY KEY (`ID`),
  KEY `IX_Org_Controller_Time` (`OrgId`,`Controller`,`Time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

