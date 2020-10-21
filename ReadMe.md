# Synchronizing changes in SqlServer Table to Couchbase by using Change Tracking (CT)
In Sql Server, Insert, Update, Delete, ie DML events can be followed up on a table with the CDT option.

Sql Server provides two features in this regard: Change Tracking (CT) and Change Data Capturing (CDC).

> You should review the [details](https://docs.microsoft.com/en-us/sql/relational-databases/track-changes/track-data-changes-sql-server?view=sql-server-ver15) of both features. Usage within this project may not match the way your system works and you may not be able to transfer changes correctly.

## Warning
SQL Server strongly recommends using **Snapshot Isolation** to accurately track DML changes in a tag with CT. Check [here](https://docs.microsoft.com/en-us/sql/relational-databases/track-changes/work-with-change-tracking-sql-server?view=sql-server-ver15). The following assumptions were made for the scenario in this project and **Snapshot Isolation** was not used.

- Sql Server regularly deletes change records. Change records are read before deletion and synchronized to Couchbase. *Examine the CHANGE_RETENTION setting in Sql Server.*
- It is ensured that the stored *cdtLastTrackingVersion* value is not less than the *CHANGE_TRACKING_MIN_VALID_VERSION()* value. As a result of some administrative activities, the *CHANGE_TRACKING_MIN_VALID_VERSION()* value may become less than the *cdtLastTrackingVersion* value stored in Couchbase. The value *cdtLastTrackingVersion* stored in Couchbase should be deleted after any administrative activities that will lead to this. Examine the SQL Server documents and determine the activities that may lead to this.

After the following preparations, the application is ready to work. After completing these steps, run the application.

## External Packages Used
Add packages used in the project from *nuget*.

    dotnet add package CouchbaseNetClient
    dotnet add package System.Data.SqlClient
    dotnet add Package Dapper

## Running a Couchbase Instance with Docker

    docker run -d -p 8091-8094:8091-8094 -p 11210:11210 couchbase

## Running a Sql Server Instance with Docker
> If there is a SQL server running in the development environment, the **1433** port may be in use. Stop the SQL server running in the development environment.

    docker run -e 'ACCEPT_EULA=Y' -e 'SA_PASSWORD=yourStrong(!)Password' -p 1433:1433 -d mcr.microsoft.com/mssql/server:2019-latest

You can connect to the newly run Sql Server instance using **Managment Studio** to make the installations. Server: localhost, Username: **sa**, Password: **yourStrong(!)Password**

## Creating Sample Tables and Data in SQL Server
Create the sample environment with the script below.

    CREATE DATABASE SQLToCouchbaseSample;
    GO
    USE SQLToCouchbaseSample;
    GO
    ALTER DATABASE SQLToCouchbaseSample SET CHANGE_TRACKING = ON (CHANGE_RETENTION = 3 DAYS, AUTO_CLEANUP = ON);
    GO
    CREATE TABLE Product (
        ProductId INT PRIMARY KEY IDENTITY (1, 1),
        ProductName NVARCHAR (255) NOT NULL,
        BrandId INT NOT NULL,
        CategoryId INT NOT NULL,
        ModelYear INT NOT NULL,
        ListPrice DECIMAL (10, 2) NOT NULL
    );
    GO
    ALTER TABLE Product ENABLE CHANGE_TRACKING WITH (TRACK_COLUMNS_UPDATED = OFF);
    GO
    DECLARE @Index INT = 1;
    WHILE @Index <= 1000 BEGIN
        INSERT INTO Product (ProductName, BrandId, CategoryId, ModelYear, ListPrice)
        SELECT 
            'Product :' + CAST(@Index AS VARCHAR(100))
            ,FLOOR(RAND()*(100-50+1)+50)
            ,FLOOR(RAND()*(10-5+1)+5)
            ,FLOOR(RAND()*(2000-2020+1)+2020)
            ,FLOOR(RAND()*(100-1+1)+1) + RAND();
        SET @Index = @Index + 1;
    END;
    GO

## Creating Cluster and Bucket in Couchbase
Go to localhost: 8091 with your browser. Select the **Setup New Cluster** step and follow the steps to create a new cluster in Couchbase. Set the username as **Administrator** and password as **111111**. Choose a name of your choice as the cluster name. After accepting the user agreement, complete the setup with **Finish With Defaults**.

Create a new bucket with the **ADD BUCKET** option from the **Buckets** menu. Assign *documents* as the name and leave the other options as they are.

After these preparations, the application can be run. Open a terminal and run the following command in the project root directory.

    dotnet run

After seeing the messages about synchronization, you can examine the documents in the bucket named *documents* from the Couchbase panel. You can access the Couchbase panel via localhost:8091.

Run the following sentence with the help of **Management Studio**.

    UPDATE Product SET ProductName = N'1 numaralı ürünün adı değişti' WHERE ProductId = 1;

Look at the *ProductName* property of the document *Product-1* from the Couchbase panel. See it updated to *1 numaralı ürünün adı değişti*.