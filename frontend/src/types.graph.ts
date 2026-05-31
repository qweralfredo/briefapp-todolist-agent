// ── Knowledge Graph Types ─────────────────────────────────────────────────────

export type GraphNodeType =
  | 'task'
  | 'commit'
  | 'file'
  | 'business_rule'
  | 'acceptance_criteria'
  | 'sprint'
  | 'backlog'
  | 'agent'

export type GraphEdgeType =
  | 'implements'
  | 'satisfies'
  | 'produced'
  | 'modifies'
  | 'belongs_to'
  | 'executed_by'
  | 'depends_on'
  | 'references'
  | 'related_to'

export type GraphNode = {
  id: string
  projectId: string
  nodeType: GraphNodeType
  externalId: string
  label: string
  propertiesJson: string
  createdAt: string
  updatedAt: string
}

export type GraphEdge = {
  id: string
  projectId: string
  sourceNodeId: string
  targetNodeId: string
  edgeType: GraphEdgeType
  weight: number
  metadataJson: string
  createdAt: string
}

export type GraphResponse = {
  nodes: GraphNode[]
  edges: GraphEdge[]
}

// ── Node color palette ────────────────────────────────────────────────────────

export const NODE_COLORS: Record<GraphNodeType, string> = {
  task:                 '#6366f1',  // indigo
  commit:               '#f59e0b',  // amber
  file:                 '#10b981',  // emerald
  business_rule:        '#ec4899',  // pink
  acceptance_criteria:  '#8b5cf6',  // violet
  sprint:               '#3b82f6',  // blue
  backlog:              '#06b6d4',  // cyan
  agent:                '#f97316',  // orange
}

export const NODE_ICONS: Record<GraphNodeType, string> = {
  task:                 '✅',
  commit:               '⬡',
  file:                 '📄',
  business_rule:        '⚖️',
  acceptance_criteria:  '🎯',
  sprint:               '🏃',
  backlog:              '📋',
  agent:                '🤖',
}

export const EDGE_COLORS: Record<GraphEdgeType, string> = {
  implements:     '#ec4899',
  satisfies:      '#8b5cf6',
  produced:       '#f59e0b',
  modifies:       '#10b981',
  belongs_to:     '#3b82f6',
  executed_by:    '#f97316',
  depends_on:     '#ef4444',
  references:     '#6b7280',
  related_to:     '#9ca3af',
}
