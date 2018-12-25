iChen® Server Persistence Library
================================

Framework: .NET Standard 2.0  
Dependencies: [`iChen.OpenProtocol.dll`](https://github.com/chenhsong/OpenProtocol)  
Major Packages: Entity Framework Core

|Directory|Content|
|---------|-------|
|`ConfigDB`|ORM entity (`ConfigDB`) for Entity Framework Core to manage configuration settings for the iChen&reg; Server.  The back-end database can be either SQL Server or SQL Server Compact Edition (sqlce).|
|`Database`|Empty bare-minimum configuration databases.|
|`DataStore`|Simple API wrapper over the Entity Framework Core calls to do CRUD operations on the configuration database.|
|`Caches`|Interface for a shared cache and an in-memory, single-process implementation.|
