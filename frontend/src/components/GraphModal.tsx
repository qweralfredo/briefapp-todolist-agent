import { useEffect, useState, useCallback } from 'react'
import GraphViewer from './GraphViewer'
import type { GraphResponse, GraphNode } from '../types.graph'
import { NODE_COLORS, NODE_ICONS } from '../types.graph'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8480'

function getAuthHeaders(): Record<string, string> {
  const token = localStorage.getItem('authToken')
  if (token) return { 'Authorization': `Bearer ${token}` }
  if (localStorage.getItem('isGuest') === 'true') return { 'X-Briefapp-Api-Key': 'dev-api-key' }
  return {}
}

interface GraphModalProps {
  projectId: string
  /** Se workItemId for passado, exibe grafo de task. Se backlogItemId, grafo macro. */
  workItemId?: string
  backlogItemId?: string
  title: string
  onClose: () => void
}

export default function GraphModal({
  projectId,
  workItemId,
  backlogItemId,
  title,
  onClose,
}: GraphModalProps) {
  const [graph, setGraph] = useState<GraphResponse>({ nodes: [], edges: [] })
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selectedNode, setSelectedNode] = useState<GraphNode | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)

  const fetchGraph = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const endpoint = workItemId
        ? `/api/projects/${projectId}/graph/task/${workItemId}`
        : `/api/projects/${projectId}/graph/backlog/${backlogItemId}`

      const res = await fetch(`${API_BASE}${endpoint}`, {
        headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
      })
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      const data: GraphResponse = await res.json()
      setGraph(data)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao carregar grafo')
    } finally {
      setLoading(false)
    }
  }, [projectId, workItemId, backlogItemId])

  useEffect(() => {
    fetchGraph()
  }, [fetchGraph])

  // Open detail panel when a node is selected
  const handleNodeClick = useCallback((node: GraphNode) => {
    setSelectedNode(node)
    setDetailOpen(true)
  }, [])

  // Fechar com Escape
  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [onClose])

  const stats = {
    nodes: graph.nodes.length,
    edges: graph.edges.length,
    byType: graph.nodes.reduce<Record<string, number>>((acc, n) => {
      acc[n.nodeType] = (acc[n.nodeType] ?? 0) + 1
      return acc
    }, {}),
  }

  return (
    <div
      className="graph-modal-overlay"
      onClick={(e) => { if (e.target === e.currentTarget) onClose() }}
    >
      <div className="graph-modal-container">
        {/* ── Header ──────────────────────────────────────────────────── */}
        <div className="graph-modal-header">
          <div className="graph-modal-header-left">
            <span style={{ fontSize: 22, flexShrink: 0 }}>🕸️</span>
            <div style={{ minWidth: 0 }}>
              <div className="graph-modal-title">
                {backlogItemId ? 'Grafo Macro — ' : 'Rastreabilidade — '}
                <span style={{ color: '#818cf8' }}>{title}</span>
              </div>
              <div className="graph-modal-subtitle">
                {stats.nodes} nós · {stats.edges} arestas
              </div>
            </div>
          </div>

          {/* Legend chips — hidden on very small screens */}
          <div className="graph-modal-legend">
            {Object.entries(stats.byType).map(([type, count]) => (
              <span key={type} className="graph-modal-chip" style={{
                borderColor: `${NODE_COLORS[type as keyof typeof NODE_COLORS]}40`,
                color: NODE_COLORS[type as keyof typeof NODE_COLORS],
              }}>
                {NODE_ICONS[type as keyof typeof NODE_ICONS]} {type} ({count})
              </span>
            ))}
          </div>

          <button className="graph-modal-close-btn" onClick={onClose}>
            ✕
          </button>
        </div>

        {/* ── Body ────────────────────────────────────────────────────── */}
        <div className="graph-modal-body">
          {/* Graph canvas */}
          <div className="graph-modal-canvas">
            {loading ? (
              <div className="graph-modal-state">
                <div className="graph-modal-spinner" />
                <span>Carregando grafo...</span>
              </div>
            ) : error ? (
              <div className="graph-modal-state" style={{ color: '#ef4444' }}>
                <span style={{ fontSize: 32 }}>⚠️</span>
                <span>{error}</span>
                <button onClick={fetchGraph} className="graph-modal-retry-btn">
                  Tentar novamente
                </button>
              </div>
            ) : (
              <GraphViewer
                data={graph}
                onNodeClick={handleNodeClick}
              />
            )}
          </div>

          {/* Node Detail Panel — slide-in on mobile */}
          {selectedNode && detailOpen && (
            <div className="graph-modal-detail">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
                <span style={{
                  fontSize: 11, fontWeight: 700, letterSpacing: 1,
                  color: NODE_COLORS[selectedNode.nodeType], textTransform: 'uppercase',
                }}>
                  {NODE_ICONS[selectedNode.nodeType]} {selectedNode.nodeType}
                </span>
                <button onClick={() => { setDetailOpen(false); setSelectedNode(null) }} style={{
                  background: 'none', border: 'none', color: '#6b7280', cursor: 'pointer', fontSize: 16,
                }}>✕</button>
              </div>

              <div style={{ color: '#e2e8f0', fontWeight: 600, fontSize: 14, marginBottom: 12, lineHeight: 1.4 }}>
                {selectedNode.label}
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                <NodeDetail label="External ID" value={selectedNode.externalId} mono />
                <NodeDetail label="Tipo" value={selectedNode.nodeType} />
                <NodeDetail label="Criado em" value={new Date(selectedNode.createdAt).toLocaleString('pt-BR')} />
                <NodeDetail label="Atualizado em" value={new Date(selectedNode.updatedAt).toLocaleString('pt-BR')} />
              </div>

              {selectedNode.propertiesJson !== '{}' && (
                <div style={{ marginTop: 16 }}>
                  <div style={{ color: '#6b7280', fontSize: 11, marginBottom: 6, textTransform: 'uppercase' }}>
                    Propriedades
                  </div>
                  <pre style={{
                    background: 'rgba(0,0,0,0.4)', borderRadius: 8, padding: 10,
                    fontSize: 10, color: '#a5b4fc', overflowX: 'auto', margin: 0,
                    lineHeight: 1.6, whiteSpace: 'pre-wrap', wordBreak: 'break-word',
                  }}>
                    {JSON.stringify(JSON.parse(selectedNode.propertiesJson), null, 2)}
                  </pre>
                </div>
              )}

              {/* Arestas conectadas */}
              <div style={{ marginTop: 16 }}>
                <div style={{ color: '#6b7280', fontSize: 11, marginBottom: 8, textTransform: 'uppercase' }}>
                  Conexões
                </div>
                {graph.edges
                  .filter(e => e.sourceNodeId === selectedNode.id || e.targetNodeId === selectedNode.id)
                  .map(e => {
                    const isOut = e.sourceNodeId === selectedNode.id
                    const otherId = isOut ? e.targetNodeId : e.sourceNodeId
                    const other = graph.nodes.find(n => n.id === otherId)
                    return (
                      <div key={e.id} style={{
                        display: 'flex', alignItems: 'center', gap: 6,
                        padding: '4px 0', borderBottom: '1px solid rgba(255,255,255,0.04)',
                        fontSize: 11, color: '#9ca3af',
                      }}>
                        <span style={{ color: isOut ? '#6366f1' : '#10b981', flexShrink: 0 }}>
                          {isOut ? '→' : '←'}
                        </span>
                        <span style={{ color: '#e2e8f0', fontWeight: 500, flexShrink: 0 }}>{e.edgeType}</span>
                        <span style={{ color: '#6b7280', flex: 1, textAlign: 'right', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                          {other?.label ?? otherId.slice(0, 8) + '…'}
                        </span>
                      </div>
                    )
                  })
                }
              </div>
            </div>
          )}
        </div>
      </div>

      <style>{`
        @keyframes spin { to { transform: rotate(360deg) } }

        .graph-modal-overlay {
          position: fixed; inset: 0; z-index: 1000;
          background: rgba(0,0,0,0.75);
          backdrop-filter: blur(6px);
          display: flex; align-items: center; justify-content: center;
          padding: 12px;
        }

        .graph-modal-container {
          width: 95vw; max-width: 1100px;
          height: 90vh; max-height: 90vh;
          background: linear-gradient(135deg, #0f172a 0%, #1e1b4b 100%);
          border-radius: 16px;
          border: 1px solid rgba(99,102,241,0.3);
          box-shadow: 0 25px 80px rgba(0,0,0,0.8);
          display: flex; flex-direction: column;
          overflow: hidden;
        }

        /* ── Header ───────────────────────────────────────────────── */
        .graph-modal-header {
          display: flex; align-items: center; gap: 12;
          padding: 14px 18px;
          border-bottom: 1px solid rgba(255,255,255,0.08);
          background: rgba(255,255,255,0.03);
          flex-shrink: 0;
          flex-wrap: wrap;
        }
        .graph-modal-header-left {
          display: flex; align-items: center; gap: 10;
          min-width: 0; flex: 1 1 auto;
        }
        .graph-modal-title {
          color: #e2e8f0; font-weight: 700; font-size: 15;
          white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
        }
        .graph-modal-subtitle {
          color: #6b7280; font-size: 11; margin-top: 1px;
        }
        .graph-modal-legend {
          display: flex; gap: 6; flex-wrap: wrap;
          max-width: 360px; flex-shrink: 1;
        }
        .graph-modal-chip {
          display: flex; align-items: center; gap: 4;
          background: rgba(255,255,255,0.05);
          border: 1px solid; border-radius: 20;
          padding: 2px 8px; font-size: 10;
          white-space: nowrap;
        }
        .graph-modal-close-btn {
          background: rgba(255,255,255,0.05);
          border: 1px solid rgba(255,255,255,0.1);
          border-radius: 8; color: #9ca3af;
          padding: 6px 12px; cursor: pointer;
          font-size: 13; transition: all 0.2s;
          flex-shrink: 0;
        }
        .graph-modal-close-btn:hover {
          background: rgba(255,255,255,0.1);
          color: #fff;
        }

        /* ── Body ─────────────────────────────────────────────────── */
        .graph-modal-body {
          display: flex; flex: 1;
          min-height: 0; /* crucial for flex children to shrink */
          overflow: hidden;
          position: relative;
        }
        .graph-modal-canvas {
          flex: 1; min-width: 0; min-height: 0;
          display: flex; position: relative;
          overflow: hidden;
        }
        .graph-modal-state {
          display: flex; align-items: center; justify-content: center;
          width: 100%; height: 100%;
          color: #6b7280; flex-direction: column; gap: 12;
        }
        .graph-modal-spinner {
          width: 36px; height: 36px;
          border: 3px solid #6366f1; border-top-color: transparent;
          border-radius: 50%; animation: spin 0.8s linear infinite;
        }
        .graph-modal-retry-btn {
          background: #6366f1; color: #fff; border: none;
          border-radius: 8; padding: 6px 16px; cursor: pointer;
          font-size: 13;
        }

        /* ── Detail Panel ─────────────────────────────────────────── */
        .graph-modal-detail {
          width: 280px; flex-shrink: 0;
          border-left: 1px solid rgba(255,255,255,0.08);
          padding: 16px; overflow-y: auto;
          background: rgba(0,0,0,0.3);
        }

        /* ── Responsive breakpoints ──────────────────────────────── */
        @media (max-width: 900px) {
          .graph-modal-container {
            width: 100vw; height: 100vh;
            max-width: 100vw; max-height: 100vh;
            border-radius: 0;
          }
          .graph-modal-overlay { padding: 0; }
          .graph-modal-legend { display: none; }
          .graph-modal-title { font-size: 13px; }
        }

        @media (max-width: 700px) {
          .graph-modal-body { flex-direction: column; }
          .graph-modal-canvas { flex: 1; min-height: 200px; }
          .graph-modal-detail {
            width: 100%; max-height: 40vh;
            border-left: none;
            border-top: 1px solid rgba(255,255,255,0.08);
          }
          .graph-modal-header { padding: 10px 14px; }
        }
      `}</style>
    </div>
  )
}

function NodeDetail({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div>
      <div style={{ color: '#6b7280', fontSize: 10, textTransform: 'uppercase', marginBottom: 2 }}>{label}</div>
      <div style={{
        color: '#e2e8f0', fontSize: 12,
        fontFamily: mono ? 'monospace' : 'inherit',
        wordBreak: 'break-all',
      }}>{value}</div>
    </div>
  )
}
