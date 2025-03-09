-- Registry database for the central registry service 
CREATE DATABASE drocsid_registry; 
 
-- Shared database for API nodes 
CREATE DATABASE drocsid;

\connect drocsid_registry;

-- Create Nodes table
CREATE TABLE IF NOT EXISTS "Nodes" (
    "Id" text NOT NULL,
    "Hostname" text NOT NULL,
    "Endpoint" text NOT NULL,
    "TotalStorage" bigint NOT NULL,
    "Region" text NULL,
    "LastSeen" timestamp with time zone NOT NULL,
    "Tags" text NOT NULL DEFAULT '[]',
    "Metadata" text NOT NULL DEFAULT '{}',
    "Status_IsHealthy" boolean NOT NULL,
    "Status_CurrentLoad" double precision NOT NULL,
    "Status_AvailableSpace" bigint NOT NULL,
    "Status_ActiveConnections" integer NOT NULL,
    "Status_LastUpdated" timestamp with time zone NOT NULL,
    "Status_ActiveTransfers" integer NOT NULL DEFAULT 0,
    "Status_NetworkCapacity" bigint NOT NULL DEFAULT 1000,
    "Status_UsedSpace" bigint NOT NULL DEFAULT 0,
    CONSTRAINT "PK_Nodes" PRIMARY KEY ("Id")
);

-- Create Files table
CREATE TABLE IF NOT EXISTS "Files" (
    "Id" text NOT NULL,
    "Filename" text NOT NULL,
    "Size" bigint NOT NULL,
    "ContentType" text NOT NULL,
    "Checksum" text NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ModifiedAt" timestamp with time zone NOT NULL,
    "OwnerId" text NOT NULL,
    "Tags" text NOT NULL DEFAULT '[]',
    "LastAccessed" timestamp with time zone NULL,
    "Metadata" text NOT NULL DEFAULT '{}',
    "NodeLocations" text NOT NULL DEFAULT '[]',
    CONSTRAINT "PK_Files" PRIMARY KEY ("Id")
);

-- Create Users table
CREATE TABLE IF NOT EXISTS "Users" (
    "Id" uuid NOT NULL,
    "Username" varchar(50) NOT NULL,
    "Email" varchar(100) NOT NULL,
    "Status" integer NOT NULL,
    "LastSeen" timestamp with time zone NOT NULL,
    "PasswordHash" varchar(100) NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "PreferredRegion" varchar(50) NULL,
    "CurrentNodeId" varchar(50) NULL,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);

-- Create Channels table
CREATE TABLE IF NOT EXISTS "Channels" (
    "Id" uuid NOT NULL,
    "Name" varchar(100) NOT NULL,
    "Type" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "MemberIds" text NOT NULL DEFAULT '[]',
    CONSTRAINT "PK_Channels" PRIMARY KEY ("Id")
);

-- Create Messages table
CREATE TABLE IF NOT EXISTS "Messages" (
    "Id" uuid NOT NULL,
    "ChannelId" uuid NOT NULL,
    "SenderId" uuid NOT NULL,
    "Content" text NOT NULL,
    "SentAt" timestamp with time zone NOT NULL,
    "EditedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_Messages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Messages_Channels_ChannelId" FOREIGN KEY ("ChannelId") REFERENCES "Channels" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Messages_Users_SenderId" FOREIGN KEY ("SenderId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

-- Create Attachments table
CREATE TABLE IF NOT EXISTS "Attachments" (
    "Id" uuid NOT NULL,
    "Filename" varchar(255) NOT NULL,
    "ContentType" varchar(100) NOT NULL,
    "Path" varchar(500) NOT NULL,
    "Size" bigint NOT NULL,
    "UploadedAt" timestamp with time zone NOT NULL,
    "MessageId" uuid NULL,
    CONSTRAINT "PK_Attachments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Attachments_Messages_MessageId" FOREIGN KEY ("MessageId") REFERENCES "Messages" ("Id") ON DELETE CASCADE
);

-- Create UserChannels table (many-to-many relationship between Users and Channels)
CREATE TABLE IF NOT EXISTS "UserChannels" (
    "UserId" uuid NOT NULL,
    "ChannelId" uuid NOT NULL,
    "JoinedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_UserChannels" PRIMARY KEY ("UserId", "ChannelId"),
    CONSTRAINT "FK_UserChannels_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_UserChannels_Channels_ChannelId" FOREIGN KEY ("ChannelId") REFERENCES "Channels" ("Id") ON DELETE CASCADE
);

-- Create ChannelNodes table (many-to-many relationship between Channels and Nodes)
CREATE TABLE IF NOT EXISTS "ChannelNodes" (
    "ChannelId" uuid NOT NULL,
    "NodeId" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ChannelNodes" PRIMARY KEY ("ChannelId", "NodeId"),
    CONSTRAINT "FK_ChannelNodes_Channels_ChannelId" FOREIGN KEY ("ChannelId") REFERENCES "Channels" ("Id") ON DELETE CASCADE
    -- Note: No foreign key to Nodes as NodeId is a string and Nodes uses string primary keys
);

-- Create MessageLocations table (tracks which nodes have which messages)
CREATE TABLE IF NOT EXISTS "MessageLocations" (
    "MessageId" uuid NOT NULL,
    "NodeId" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MessageLocations" PRIMARY KEY ("MessageId", "NodeId"),
    CONSTRAINT "FK_MessageLocations_Messages_MessageId" FOREIGN KEY ("MessageId") REFERENCES "Messages" ("Id") ON DELETE CASCADE
    -- Note: No foreign key to Nodes as NodeId is a string
);

-- Create indexes for better query performance
CREATE INDEX "IX_Users_Username" ON "Users" ("Username");
CREATE INDEX "IX_Users_Email" ON "Users" ("Email");
CREATE INDEX "IX_Users_Status" ON "Users" ("Status");

CREATE INDEX "IX_Messages_ChannelId" ON "Messages" ("ChannelId");
CREATE INDEX "IX_Messages_SenderId" ON "Messages" ("SenderId");
CREATE INDEX "IX_Messages_SentAt" ON "Messages" ("SentAt");

CREATE INDEX "IX_Attachments_MessageId" ON "Attachments" ("MessageId");

CREATE INDEX "IX_UserChannels_ChannelId" ON "UserChannels" ("ChannelId");
CREATE INDEX "IX_ChannelNodes_NodeId" ON "ChannelNodes" ("NodeId");
CREATE INDEX "IX_MessageLocations_NodeId" ON "MessageLocations" ("NodeId");

-- Add unique constraint for username
ALTER TABLE "Users" ADD CONSTRAINT "UQ_Users_Username" UNIQUE ("Username");

\connect drocsid;

-- Create Users table
CREATE TABLE IF NOT EXISTS "Users" (
    "Id" uuid NOT NULL,
    "Username" varchar(50) NOT NULL,
    "Email" varchar(100) NOT NULL,
    "Status" integer NOT NULL,
    "LastSeen" timestamp with time zone NOT NULL,
    "PasswordHash" varchar(100) NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "PreferredRegion" varchar(50) NULL,
    "CurrentNodeId" varchar(50) NULL,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);

-- Create Channels table
CREATE TABLE IF NOT EXISTS "Channels" (
    "Id" uuid NOT NULL,
    "Name" varchar(100) NOT NULL,
    "Type" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "MemberIds" text NOT NULL DEFAULT '[]',
    CONSTRAINT "PK_Channels" PRIMARY KEY ("Id")
);

-- Create Messages table
CREATE TABLE IF NOT EXISTS "Messages" (
    "Id" uuid NOT NULL,
    "ChannelId" uuid NOT NULL,
    "SenderId" uuid NOT NULL,
    "Content" text NOT NULL,
    "SentAt" timestamp with time zone NOT NULL,
    "EditedAt" timestamp with time zone NULL,
    "SenderName" varchar(100) NULL,
    "Timestamp" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "PK_Messages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Messages_Channels_ChannelId" FOREIGN KEY ("ChannelId") REFERENCES "Channels" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Messages_Users_SenderId" FOREIGN KEY ("SenderId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

-- Create Attachments table
CREATE TABLE IF NOT EXISTS "Attachments" (
    "Id" uuid NOT NULL,
    "Filename" varchar(255) NOT NULL,
    "ContentType" varchar(100) NOT NULL,
    "Path" varchar(500) NOT NULL,
    "Size" bigint NOT NULL,
    "UploadedAt" timestamp with time zone NOT NULL,
    "StoragePath" varchar(500) NULL,
    "MessageId" uuid NULL,
    CONSTRAINT "PK_Attachments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Attachments_Messages_MessageId" FOREIGN KEY ("MessageId") REFERENCES "Messages" ("Id") ON DELETE CASCADE
);

-- Create indexes for better query performance
CREATE INDEX IF NOT EXISTS "IX_Users_Username" ON "Users" ("Username");
CREATE INDEX IF NOT EXISTS "IX_Users_Email" ON "Users" ("Email");
CREATE INDEX IF NOT EXISTS "IX_Users_Status" ON "Users" ("Status");

CREATE INDEX IF NOT EXISTS "IX_Messages_ChannelId" ON "Messages" ("ChannelId");
CREATE INDEX IF NOT EXISTS "IX_Messages_SenderId" ON "Messages" ("SenderId");
CREATE INDEX IF NOT EXISTS "IX_Messages_SentAt" ON "Messages" ("SentAt");

CREATE INDEX IF NOT EXISTS "IX_Attachments_MessageId" ON "Attachments" ("MessageId");

-- Add unique constraint for username
ALTER TABLE "Users" ADD CONSTRAINT "UQ_Users_Username" UNIQUE ("Username");