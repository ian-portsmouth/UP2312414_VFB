USE FoodBankDB;
GO

IF COL_LENGTH('dbo.Users','IsActive') IS NULL
BEGIN
    ALTER TABLE dbo.Users
    ADD IsActive BIT NOT NULL
        CONSTRAINT DF_Users_IsActive DEFAULT(1);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_Users_Username'
      AND object_id = OBJECT_ID('dbo.Users')
)
BEGIN
    CREATE UNIQUE INDEX UX_Users_Username ON dbo.Users(Username);
END
GO


DECLARE @AdminUsername NVARCHAR(50) = 'vfbadmin';
DECLARE @AdminPassword VARCHAR(200) = 'Dellsvcs1!';  
DECLARE @AdminEmail NVARCHAR(100) = 'ianyawbenson@gmail.com';

DECLARE @AdminHash VARCHAR(64) =
    LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @AdminPassword), 2));

IF EXISTS (SELECT 1 FROM dbo.Users WHERE Username = @AdminUsername)
BEGIN
    UPDATE dbo.Users
    SET PasswordHash = @AdminHash,
        Role = 'Admin',
        Email = @AdminEmail,
        IsActive = 1
    WHERE Username = @AdminUsername;
END
ELSE
BEGIN
    INSERT INTO dbo.Users (UserID, Username, PasswordHash, Role, Email, IsActive)
    VALUES (NEWID(), @AdminUsername, @AdminHash, 'Admin', @AdminEmail, 1);
END
GO

SELECT Username, Role, Email, IsActive
FROM dbo.Users
ORDER BY Username;
