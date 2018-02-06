# iChen&reg; Server Analytics Engine

Framework: .NET Standard 2.0  
Dependencies: [`iChen.OpenProtocol.dll`](https://github.com/chenhsong/OpenProtocol)  
Major Packages: Azure Storage, NPOI

This assembly provides an API to an analytical engine that reads stored historical data and returns summarized analysis based on user-defined input criteria.

This engine can access data stored in the following:

1. Azure Storage Table
2. Any compatible database via ODBC

There is also an API to download raw data in a number of formats:

1. Excel spreadsheet
2. CSV text file
3. Tab-delimited text file
4. JSON file
