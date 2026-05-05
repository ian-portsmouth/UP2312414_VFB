USE FoodBankDB;
GO


IF OBJECT_ID('dbo.Users','U') IS NULL
CREATE TABLE dbo.Users (
    UserID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL,
    PasswordHash NVARCHAR(200) NOT NULL,
    Role NVARCHAR(20) NOT NULL,
    Email NVARCHAR(100) NULL
);

IF OBJECT_ID('dbo.Items','U') IS NULL
CREATE TABLE dbo.Items (
    ItemID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    ItemName NVARCHAR(100) NOT NULL,
    Category NVARCHAR(50),
    StockQuantity INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    ImagePath NVARCHAR(260) NULL
);

IF COL_LENGTH('dbo.Items','ImagePath') IS NULL
    ALTER TABLE dbo.Items ADD ImagePath NVARCHAR(260) NULL;

IF OBJECT_ID('dbo.Orders','U') IS NULL
CREATE TABLE dbo.Orders (
    OrderID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    DateCreated DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    OrderCode NVARCHAR(32),
    ContactName NVARCHAR(100)
);

IF OBJECT_ID('dbo.OrderItems','U') IS NULL
CREATE TABLE dbo.OrderItems (
    OrderItemID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    OrderID UNIQUEIDENTIFIER NOT NULL,
    ItemID UNIQUEIDENTIFIER NOT NULL,
    Quantity INT NOT NULL CHECK (Quantity > 0),
    CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderID) REFERENCES dbo.Orders(OrderID),
    CONSTRAINT FK_OrderItems_Items FOREIGN KEY (ItemID) REFERENCES dbo.Items(ItemID)
);


IF EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = 'IX_Orders_OrderCode'
      AND object_id = OBJECT_ID('dbo.Orders')
)
DROP INDEX IX_Orders_OrderCode ON dbo.Orders;
GO

IF COL_LENGTH('dbo.Orders','OrderDate') IS NULL
ALTER TABLE dbo.Orders
ADD OrderDate AS CAST(DateCreated AS date) PERSISTED;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = 'UX_Orders_OrderDate_OrderCode'
      AND object_id = OBJECT_ID('dbo.Orders')
)
CREATE UNIQUE INDEX UX_Orders_OrderDate_OrderCode
ON dbo.Orders (OrderDate, OrderCode);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = 'IX_OrderItems_OrderID'
      AND object_id = OBJECT_ID('dbo.OrderItems')
)
CREATE INDEX IX_OrderItems_OrderID ON dbo.OrderItems(OrderID);
GO


MERGE dbo.Items AS t
USING (VALUES
    ('Milk - carton','Dairy',30,'/images/items/milk.jpg'),
    ('Bread - loaf','Bakery',20,'/images/items/bread.jpg'),
    ('Bottled Water - pack','Drinks',30,'/images/items/water.jpg'),
    ('Rice - bag','Dry Goods',5,'/images/items/rice.png')
) AS s(ItemName,Category,StockQuantity,ImagePath)
ON t.ItemName = s.ItemName
WHEN MATCHED THEN 
    UPDATE SET Category=s.Category, StockQuantity=s.StockQuantity, IsActive=1,
              ImagePath = COALESCE(NULLIF(s.ImagePath,''), t.ImagePath)
WHEN NOT MATCHED THEN
    INSERT (ItemName,Category,StockQuantity,IsActive,ImagePath)
    VALUES (s.ItemName,s.Category,s.StockQuantity,1,s.ImagePath);
GO


USE FoodBankDB;
GO

MERGE dbo.Items AS t
USING (VALUES
    ('Sugar','Dry Goods',20,'/images/items/sugar.png'),
    ('Yoghurt','Dairy',15,'/images/items/yoghurt.png'),
    ('Cereal','Dry Goods',15,'/images/items/cereal.png'),
    ('Butter','Dairy',10,'/images/items/butter.png'),
    ('Juice Box','Drinks',25,'/images/items/juice-box.png'),
    ('Mixed Vegetables','Frozen',12,'/images/items/mixed-vegetables.png')
) AS s(ItemName,Category,StockQuantity,ImagePath)
ON t.ItemName = s.ItemName
WHEN MATCHED THEN
    UPDATE SET Category=s.Category, StockQuantity=s.StockQuantity, IsActive=1,
              ImagePath = COALESCE(NULLIF(s.ImagePath,''), t.ImagePath)
WHEN NOT MATCHED THEN
    INSERT (ItemName,Category,StockQuantity,IsActive,ImagePath)
    VALUES (s.ItemName,s.Category,s.StockQuantity,1,s.ImagePath);
GO

SELECT ItemName, Category, StockQuantity, IsActive
FROM dbo.Items
ORDER BY Category, ItemName;



IF TYPE_ID('dbo.OrderItemType') IS NULL
CREATE TYPE dbo.OrderItemType AS TABLE (
    ItemID UNIQUEIDENTIFIER NOT NULL,
    Quantity INT NOT NULL CHECK (Quantity > 0)
);
GO


IF NOT EXISTS (
    SELECT 1 FROM sys.sequences
    WHERE name='OrderNoSeq'
      AND schema_id=SCHEMA_ID('dbo')
)
CREATE SEQUENCE dbo.OrderNoSeq
    AS tinyint START WITH 1 INCREMENT BY 1
    MINVALUE 1 MAXVALUE 99
    CYCLE CACHE 20;
GO


CREATE OR ALTER PROCEDURE dbo.PlaceOrder_NoAuth
    @ContactName NVARCHAR(100),
    @Items dbo.OrderItemType READONLY,
    @OrderCode NVARCHAR(32) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    ;WITH Req AS (
       SELECT ItemID, SUM(Quantity) AS QtyRequested
       FROM @Items
       GROUP BY ItemID
    )
    SELECT i.ItemName, r.QtyRequested, i.StockQuantity
    INTO #Insufficient
    FROM Req r
    JOIN dbo.Items i ON r.ItemID = i.ItemID
    WHERE r.QtyRequested > i.StockQuantity;

    IF EXISTS (SELECT 1 FROM #Insufficient)
    BEGIN
        SELECT * FROM #Insufficient;
        ROLLBACK;
        RETURN;
    END

    DECLARE @ShortNo tinyint = NEXT VALUE FOR dbo.OrderNoSeq;
    SET @OrderCode = RIGHT('00' + CAST(@ShortNo AS varchar(3)), 2);

    DECLARE @OrderID UNIQUEIDENTIFIER = NEWID();

    INSERT INTO dbo.Orders (OrderID, DateCreated, Status, ContactName, OrderCode)
    VALUES (@OrderID, SYSUTCDATETIME(), 'Pending', @ContactName, @OrderCode);

    INSERT INTO dbo.OrderItems (OrderItemID, OrderID, ItemID, Quantity)
    SELECT NEWID(), @OrderID, ItemID, Quantity
    FROM @Items;

    UPDATE i
    SET StockQuantity = StockQuantity - r.QtyRequested
    FROM dbo.Items i
    JOIN (
       SELECT ItemID, SUM(Quantity) AS QtyRequested
       FROM @Items
       GROUP BY ItemID
    ) r ON i.ItemID = r.ItemID;

    COMMIT;
END;
GO

