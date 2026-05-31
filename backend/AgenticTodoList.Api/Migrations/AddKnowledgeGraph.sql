-- Migration: Knowledge Graph Tables
-- Cria tabelas para armazenar o grafo de rastreabilidade em PostgreSQL
-- Nós e arestas com índices otimizados para queries de grafo

-- ── Nodes ─────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "GraphNodes" (
    "Id"             UUID         NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    "ProjectId"      UUID         NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,
    "NodeType"       VARCHAR(50)  NOT NULL,
    "ExternalId"     VARCHAR(500) NOT NULL,
    "Label"          VARCHAR(500) NOT NULL,
    "PropertiesJson" TEXT         NOT NULL DEFAULT '{}',
    "CreatedAt"      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- Unique constraint: um nó por projeto/tipo/external_id
CREATE UNIQUE INDEX IF NOT EXISTS "IX_GraphNodes_ProjectId_NodeType_ExternalId"
    ON "GraphNodes" ("ProjectId", "NodeType", "ExternalId");

-- Index para queries por projeto e tipo
CREATE INDEX IF NOT EXISTS "IX_GraphNodes_ProjectId_NodeType"
    ON "GraphNodes" ("ProjectId", "NodeType");

-- ── Edges ─────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "GraphEdges" (
    "Id"           UUID         NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    "ProjectId"    UUID         NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,
    "SourceNodeId" UUID         NOT NULL REFERENCES "GraphNodes"("Id") ON DELETE CASCADE,
    "TargetNodeId" UUID         NOT NULL REFERENCES "GraphNodes"("Id") ON DELETE RESTRICT,
    "EdgeType"     VARCHAR(50)  NOT NULL,
    "Weight"       DOUBLE PRECISION NOT NULL DEFAULT 1.0,
    "MetadataJson" TEXT         NOT NULL DEFAULT '{}',
    "CreatedAt"    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- Unique constraint: uma aresta por (projeto, source, target, tipo)
CREATE UNIQUE INDEX IF NOT EXISTS "IX_GraphEdges_Unique"
    ON "GraphEdges" ("ProjectId", "SourceNodeId", "TargetNodeId", "EdgeType");

-- Index para traversal: arestas de saída de um nó
CREATE INDEX IF NOT EXISTS "IX_GraphEdges_SourceNodeId"
    ON "GraphEdges" ("SourceNodeId");

-- Index para traversal: arestas de entrada em um nó
CREATE INDEX IF NOT EXISTS "IX_GraphEdges_TargetNodeId"
    ON "GraphEdges" ("TargetNodeId");

-- Index para queries por projeto e tipo de aresta
CREATE INDEX IF NOT EXISTS "IX_GraphEdges_ProjectId_EdgeType"
    ON "GraphEdges" ("ProjectId", "EdgeType");
