CREATE TABLE "GraphNodes" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "NodeType" character varying(50) NOT NULL,
    "ExternalId" character varying(500) NOT NULL,
    "Label" character varying(500) NOT NULL,
    "PropertiesJson" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_GraphNodes" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_GraphNodes_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects"("Id") ON DELETE CASCADE
);

CREATE TABLE "GraphEdges" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "SourceNodeId" uuid NOT NULL,
    "TargetNodeId" uuid NOT NULL,
    "EdgeType" character varying(50) NOT NULL,
    "Weight" double precision NOT NULL,
    "MetadataJson" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_GraphEdges" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_GraphEdges_GraphNodes_SourceNodeId" FOREIGN KEY ("SourceNodeId") REFERENCES "GraphNodes"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_GraphEdges_GraphNodes_TargetNodeId" FOREIGN KEY ("TargetNodeId") REFERENCES "GraphNodes"("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_GraphEdges_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects"("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_GraphEdges_ProjectId_SourceNodeId_TargetNodeId_EdgeType" ON "GraphEdges" ("ProjectId", "SourceNodeId", "TargetNodeId", "EdgeType");
CREATE INDEX "IX_GraphEdges_SourceNodeId" ON "GraphEdges" ("SourceNodeId");
CREATE INDEX "IX_GraphEdges_TargetNodeId" ON "GraphEdges" ("TargetNodeId");
CREATE UNIQUE INDEX "IX_GraphNodes_ProjectId_NodeType_ExternalId" ON "GraphNodes" ("ProjectId", "NodeType", "ExternalId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260502014826_AddKnowledgeGraph', '9.0.4');
