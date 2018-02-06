# iChen&reg; Server Core Persistence Library

Framework: .NET Standard 2.0  
Dependencies: [`iChen.OpenProtocol.dll`](https://github.com/chenhsong/OpenProtocol)  
Major Packages: Entity Framework Core

`ConfigDB` provides an ORM entity (`ConfigDB`) for Entity Framework Core to manage configuration settings for the iChen&reg; Server.  The back-end database can be either SQL Server or SQL Server Compact Edition (sqlce).

`Database` contains an empty bare-minimum SQLCE configuration database.

`DataStore` provides a simple API wrapper over the Entity Framework Core calls to do CRUD operations on the configuration database.

`Caches` defines an interface for a shared cache and provides an in-memory, single-process implementation.
