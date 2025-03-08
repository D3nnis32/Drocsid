-- Script to initialize the database schema for Drocsid registry

-- Connect to the drocsid_registry database
\c drocsid_registry

-- Create the Nodes table
CREATE TABLE IF NOT EXISTS "Nodes" (
    "Id" text NOT NULL,
    "Hostname" text NOT NULL,
    "Endpoint" text NOT NULL,
    "ApiKey" text NULL,
    "TotalStorage" bigint NOT NULL,
    "Region" text NULL,
    "LastSeen" timestamp with time zone NOT NULL,
    "Tags" json NOT NULL,
    "Metadata" json NOT NULL,
    "Status_IsHealthy" boolean NOT NULL,
    "Status_CurrentLoad" double precision NOT NULL,
    "Status_AvailableSpace" bigint NOT NULL,
    "Status_ActiveConnections" integer NOT NULL,
    "Status_LastUpdated" timestamp with time zone NOT NULL,
    "Status_ActiveTransfers" integer NOT NULL,
    "Status_NetworkCapacity" bigint NOT NULL,
    "Status_UsedSpace" bigint NOT NULL,
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
    "LastAccessed" timestamp with time zone NULL,
    "Tags" json NOT NULL,
    "NodeLocations" json NOT NULL,
    "Metadata" json NOT NULL,
    CONSTRAINT "PK_Files" PRIMARY KEY ("Id")
);

-- Create Users table
CREATE TABLE IF NOT EXISTS "Users" (
    "Id" uuid NOT NULL,
    "Username" character varying(50) NOT NULL,
    "Email" character varying(100) NOT NULL,
    "Status" integer NOT NULL,
    "LastSeen" timestamp with time zone NOT NULL,
    "PasswordHash" character varying(100) NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "PreferredRegion" character varying(50) NULL,
    "CurrentNodeId" character varying(50) NULL,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);

-- Create Channels table
CREATE TABLE IF NOT EXISTS "Channels" (
    "Id" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Type" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "MemberIds" json NOT NULL,
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
    "MessageId" uuid NULL,
    CONSTRAINT "PK_Messages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Messages_Messages_MessageId" FOREIGN KEY ("MessageId") REFERENCES "Messages" ("Id") ON DELETE RESTRICT
);

-- Create Attachments table
CREATE TABLE IF NOT EXISTS "Attachments" (
    "Id" uuid NOT NULL,
    "Filename" character varying(255) NOT NULL,
    "ContentType" character varying(100) NOT NULL,
    "Path" character varying(500) NOT NULL,
    "Size" bigint NOT NULL,
    "UploadedAt" timestamp with time zone NOT NULL,
    "MessageId" uuid NULL,
    CONSTRAINT "PK_Attachments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Attachments_Messages_MessageId" FOREIGN KEY ("MessageId") REFERENCES "Messages" ("Id") ON DELETE CASCADE
);

-- Create UserChannels table
CREATE TABLE IF NOT EXISTS "UserChannels" (
    "UserId" uuid NOT NULL,
    "ChannelId" uuid NOT NULL,
    "JoinedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_UserChannels" PRIMARY KEY ("UserId", "ChannelId"),
    CONSTRAINT "FK_UserChannels_Channels_ChannelId" FOREIGN KEY ("ChannelId") REFERENCES "Channels" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_UserChannels_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

-- Create ChannelNodes table
CREATE TABLE IF NOT EXISTS "ChannelNodes" (
    "ChannelId" uuid NOT NULL,
    "NodeId" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ChannelNodes" PRIMARY KEY ("ChannelId", "NodeId"),
    CONSTRAINT "FK_ChannelNodes_Channels_ChannelId" FOREIGN KEY ("ChannelId") REFERENCES "Channels" ("Id") ON DELETE CASCADE
);

-- Create MessageLocations table
CREATE TABLE IF NOT EXISTS "MessageLocations" (
    "MessageId" uuid NOT NULL,
    "NodeId" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MessageLocations" PRIMARY KEY ("MessageId", "NodeId"),
    CONSTRAINT "FK_MessageLocations_Messages_MessageId" FOREIGN KEY ("MessageId") REFERENCES "Messages" ("Id") ON DELETE CASCADE
);

-- Create indices
CREATE INDEX IF NOT EXISTS "IX_Attachments_MessageId" ON "Attachments" ("MessageId");
CREATE INDEX IF NOT EXISTS "IX_Messages_MessageId" ON "Messages" ("MessageId");