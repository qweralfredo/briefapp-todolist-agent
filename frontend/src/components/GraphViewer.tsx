import { useEffect, useRef, useCallback, useState } from 'react'
import * as d3 from 'd3'
import type { GraphResponse, GraphNode, GraphEdge } from '../types.graph'
import { NODE_COLORS, NODE_ICONS, EDGE_COLORS } from '../types.graph'

// ── D3 simulation types ────────────────────────────────────────────────────────

interface SimNode extends d3.SimulationNodeDatum, GraphNode {
  x?: number
  y?: number
}

interface SimEdge extends d3.SimulationLinkDatum<SimNode> {
  edge: GraphEdge
}

// ── Props ──────────────────────────────────────────────────────────────────────

interface GraphViewerProps {
  data: GraphResponse
  onNodeClick?: (node: GraphNode) => void
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function GraphViewer({
  data,
  onNodeClick,
}: GraphViewerProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const svgRef = useRef<SVGSVGElement>(null)
  const [dims, setDims] = useState({ width: 800, height: 500 })

  // ── ResizeObserver to track container size ─────────────────────────────────
  useEffect(() => {
    const el = containerRef.current
    if (!el) return

    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const { width, height } = entry.contentRect
        if (width > 0 && height > 0) {
          setDims({ width: Math.round(width), height: Math.round(height) })
        }
      }
    })
    observer.observe(el)
    return () => observer.disconnect()
  }, [])

  const render = useCallback(() => {
    if (!svgRef.current || data.nodes.length === 0) return

    const { width, height } = dims

    const svg = d3.select(svgRef.current)
    svg.selectAll('*').remove()

    // ── Setup ──────────────────────────────────────────────────────────────────
    const defs = svg.append('defs')

    // Arrowhead marker
    defs.append('marker')
      .attr('id', 'arrow')
      .attr('viewBox', '0 -4 8 8')
      .attr('refX', 18)
      .attr('refY', 0)
      .attr('markerWidth', 6)
      .attr('markerHeight', 6)
      .attr('orient', 'auto')
      .append('path')
      .attr('d', 'M0,-4L8,0L0,4')
      .attr('fill', '#6b7280')
      .attr('opacity', 0.6)

    // Radial gradient for glass effect on nodes
    defs.selectAll('radialGradient')
      .data(Object.entries(NODE_COLORS))
      .enter()
      .append('radialGradient')
      .attr('id', ([type]) => `grad-${type}`)
      .attr('cx', '35%').attr('cy', '35%').attr('r', '65%')
      .each(function ([, color]) {
        d3.select(this).append('stop')
          .attr('offset', '0%').attr('stop-color', d3.color(color)?.brighter(0.5)?.toString() ?? color)
        d3.select(this).append('stop')
          .attr('offset', '100%').attr('stop-color', color)
      })

    // ── Simulation ─────────────────────────────────────────────────────────────
    const simNodes: SimNode[] = data.nodes.map(n => ({ ...n }))
    const nodeById = new Map(simNodes.map(n => [n.id, n]))

    const simEdges: SimEdge[] = data.edges
      .filter(e => nodeById.has(e.sourceNodeId) && nodeById.has(e.targetNodeId))
      .map(e => ({
        source: nodeById.get(e.sourceNodeId)!,
        target: nodeById.get(e.targetNodeId)!,
        edge: e,
      }))

    const simulation = d3.forceSimulation<SimNode>(simNodes)
      .force('link', d3.forceLink<SimNode, SimEdge>(simEdges)
        .id(d => d.id)
        .distance(110)
        .strength(0.5))
      .force('charge', d3.forceManyBody().strength(-320))
      .force('center', d3.forceCenter(width / 2, height / 2))
      .force('collision', d3.forceCollide(36))

    // ── Canvas group (zoomable) ────────────────────────────────────────────────
    const g = svg.append('g')

    svg.call(
      d3.zoom<SVGSVGElement, unknown>()
        .scaleExtent([0.2, 3])
        .on('zoom', (event) => g.attr('transform', event.transform))
    )

    // ── Edges ──────────────────────────────────────────────────────────────────
    const edgeGroup = g.append('g').attr('class', 'edges')

    const edgeLines = edgeGroup.selectAll<SVGLineElement, SimEdge>('line')
      .data(simEdges)
      .enter()
      .append('line')
      .attr('stroke', d => EDGE_COLORS[d.edge.edgeType] ?? '#9ca3af')
      .attr('stroke-width', d => Math.max(1, d.edge.weight * 1.5))
      .attr('stroke-opacity', 0.55)
      .attr('marker-end', 'url(#arrow)')

    const edgeLabels = edgeGroup.selectAll<SVGTextElement, SimEdge>('text')
      .data(simEdges)
      .enter()
      .append('text')
      .attr('font-size', 8)
      .attr('fill', '#9ca3af')
      .attr('text-anchor', 'middle')
      .attr('dy', -3)
      .text(d => d.edge.edgeType)

    // ── Nodes ──────────────────────────────────────────────────────────────────
    const nodeGroup = g.append('g').attr('class', 'nodes')

    const nodeGs = nodeGroup.selectAll<SVGGElement, SimNode>('g')
      .data(simNodes)
      .enter()
      .append('g')
      .attr('class', 'node')
      .style('cursor', 'pointer')
      .call(
        d3.drag<SVGGElement, SimNode>()
          .on('start', (event, d) => {
            if (!event.active) simulation.alphaTarget(0.3).restart()
            d.fx = d.x; d.fy = d.y
          })
          .on('drag', (event, d) => { d.fx = event.x; d.fy = event.y })
          .on('end', (event, d) => {
            if (!event.active) simulation.alphaTarget(0)
            d.fx = null; d.fy = null
          })
      )
      .on('click', (_event, d) => onNodeClick?.(d))

    // Glow filter
    const filter = defs.append('filter').attr('id', 'glow')
    filter.append('feGaussianBlur').attr('stdDeviation', '3').attr('result', 'coloredBlur')
    const feMerge = filter.append('feMerge')
    feMerge.append('feMergeNode').attr('in', 'coloredBlur')
    feMerge.append('feMergeNode').attr('in', 'SourceGraphic')

    // Circle
    nodeGs.append('circle')
      .attr('r', 20)
      .attr('fill', d => `url(#grad-${d.nodeType})`)
      .attr('stroke', d => NODE_COLORS[d.nodeType] ?? '#6366f1')
      .attr('stroke-width', 1.5)
      .attr('filter', 'url(#glow)')

    // Icon
    nodeGs.append('text')
      .attr('text-anchor', 'middle')
      .attr('dominant-baseline', 'central')
      .attr('font-size', 13)
      .text(d => NODE_ICONS[d.nodeType] ?? '●')

    // Label
    nodeGs.append('text')
      .attr('text-anchor', 'middle')
      .attr('dy', 32)
      .attr('font-size', 9)
      .attr('fill', '#e2e8f0')
      .attr('font-family', 'Inter, sans-serif')
      .text(d => d.label.length > 22 ? d.label.slice(0, 20) + '…' : d.label)

    // Tooltip
    nodeGs.append('title').text(d =>
      `[${d.nodeType}] ${d.label}\nID: ${d.externalId}`)

    // ── Tick ──────────────────────────────────────────────────────────────────
    simulation.on('tick', () => {
      edgeLines
        .attr('x1', d => (d.source as SimNode).x ?? 0)
        .attr('y1', d => (d.source as SimNode).y ?? 0)
        .attr('x2', d => (d.target as SimNode).x ?? 0)
        .attr('y2', d => (d.target as SimNode).y ?? 0)

      edgeLabels
        .attr('x', d => (((d.source as SimNode).x ?? 0) + ((d.target as SimNode).x ?? 0)) / 2)
        .attr('y', d => (((d.source as SimNode).y ?? 0) + ((d.target as SimNode).y ?? 0)) / 2)

      nodeGs.attr('transform', d => `translate(${d.x ?? 0},${d.y ?? 0})`)
    })

    return () => { simulation.stop() }
  }, [data, dims, onNodeClick])

  useEffect(() => {
    const cleanup = render()
    return cleanup
  }, [render])

  if (data.nodes.length === 0) {
    return (
      <div
        ref={containerRef}
        style={{
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          width: '100%', height: '100%', minHeight: 200,
          color: '#6b7280', fontSize: 14, flexDirection: 'column', gap: 8,
        }}
      >
        <span style={{ fontSize: 32 }}>🕸️</span>
        <span>Nenhum dado de rastreabilidade ainda.</span>
        <span style={{ fontSize: 12, color: '#4b5563' }}>
          Os dados aparecerão após o skill-graph ser acionado.
        </span>
      </div>
    )
  }

  return (
    <div
      ref={containerRef}
      style={{
        width: '100%',
        height: '100%',
        minHeight: 200,
        overflow: 'hidden',
      }}
    >
      <svg
        ref={svgRef}
        width={dims.width}
        height={dims.height}
        viewBox={`0 0 ${dims.width} ${dims.height}`}
        style={{
          background: 'radial-gradient(ellipse at center, #1e1b4b 0%, #0f0f1a 100%)',
          display: 'block',
        }}
      />
    </div>
  )
}
