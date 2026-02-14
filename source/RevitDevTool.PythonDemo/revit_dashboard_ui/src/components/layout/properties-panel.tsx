/**
 * Properties Panel — Revit-style Properties Pane.
 * Shows grouped, collapsible parameter sections fetched via getElementParameters.
 * Features: read-only indicator, copy button, status tags, search.
 */

import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import {
  Button,
  Collapse,
  Input,
  Spin,
  Tag,
  Tooltip,
  Typography,
} from "@douyinfe/semi-ui"
import {
  IconClose,
  IconCopy,
  IconLock,
  IconSearch,
} from "@douyinfe/semi-icons"
import { toast } from "sonner"
import { useBridge } from "@/providers/bridge-provider"
import type { ElementRow } from "@/types"

const { Title, Text } = Typography

/** Shape of a single parameter returned by the bridge */
interface ParameterInfo {
  name: string
  value: string
  is_readonly: boolean
  storage_type: string
}

type GroupedParameters = Record<string, ParameterInfo[]>

interface PropertiesPanelProps {
  element: ElementRow | null
  visible: boolean
  onClose: () => void
}

/** Preferred group ordering (Revit-like) */
const GROUP_ORDER = [
  "Identity Data",
  "Constraints",
  "Dimensions",
  "Construction",
  "Graphics",
  "Materials And Finishes",
  "Structural",
  "Analytical Model",
  "Energy Analysis",
  "Phasing",
  "Other",
]

function groupSortKey(name: string): number {
  const idx = GROUP_ORDER.findIndex((g) => name.toLowerCase().includes(g.toLowerCase()))
  return idx >= 0 ? idx : GROUP_ORDER.length
}

export function PropertiesPanel({ element, visible, onClose }: PropertiesPanelProps) {
  const bridge = useBridge()
  const [params, setParams] = useState<GroupedParameters | null>(null)
  const [loading, setLoading] = useState(false)
  const [search, setSearch] = useState("")
  const [expandedKeys, setExpandedKeys] = useState<string[]>([])
  const loadingRef = useRef(false)

  // Fetch parameters when element changes
  useEffect(() => {
    if (!element || !visible) {
      return
    }

    let cancelled = false

    // Set loading state via callback to avoid setState in effect warning
    queueMicrotask(() => {
      if (!cancelled) {
        loadingRef.current = true
        setLoading(true)
      }
    })

    bridge
      .getElementParameters(element.element_id)
      .then((result) => {
        if (!cancelled) {
          setParams(result.parameters)
          // Auto-expand all groups
          setExpandedKeys(Object.keys(result.parameters))
        }
      })
      .catch((err) => {
        console.error("[PropertiesPanel] Failed to fetch parameters:", err)
        if (!cancelled) setParams(null)
      })
      .finally(() => {
        if (!cancelled) {
          loadingRef.current = false
          setLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [element, visible, bridge])

  // Copy value to clipboard
  const handleCopy = useCallback((value: string) => {
    navigator.clipboard.writeText(value).then(
      () => toast.success("Copied to clipboard"),
      () => toast.error("Failed to copy"),
    )
  }, [])

  // Sorted group names
  const sortedGroups = useMemo(() => {
    if (!params) return []
    return Object.keys(params).sort((a, b) => groupSortKey(a) - groupSortKey(b))
  }, [params])

  // Filtered groups based on search
  const filteredGroups = useMemo(() => {
    if (!params) return []
    if (!search.trim()) return sortedGroups

    const needle = search.toLowerCase()
    return sortedGroups.filter((group) =>
      params[group].some(
        (p) =>
          p.name.toLowerCase().includes(needle) ||
          p.value.toLowerCase().includes(needle),
      ),
    )
  }, [params, sortedGroups, search])

  if (!visible || !element) return null

  const totalParams = params
    ? Object.values(params).reduce((sum, list) => sum + list.length, 0)
    : 0

  return (
    <div
      style={{
        position: "fixed",
        top: 0,
        right: 0,
        bottom: 0,
        width: 360,
        backgroundColor: "var(--semi-color-bg-1)",
        borderLeft: "1px solid var(--semi-color-border)",
        boxShadow: "-4px 0 16px rgba(0,0,0,0.1)",
        zIndex: 1000,
        display: "flex",
        flexDirection: "column",
        transform: visible ? "translateX(0)" : "translateX(100%)",
        transition: "transform 0.25s ease",
      }}
    >
      {/* Header */}
      <div
        style={{
          padding: "10px 14px",
          borderBottom: "1px solid var(--semi-color-border)",
          flexShrink: 0,
        }}
      >
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 6 }}>
          <Title heading={6} style={{ margin: 0 }}>Properties</Title>
          <Button theme="borderless" icon={<IconClose />} size="small" onClick={onClose} />
        </div>

        {/* Element identity header */}
        <div style={{ marginBottom: 6 }}>
          <Text strong ellipsis={{ showTooltip: true }} style={{ display: "block", fontSize: 13 }}>
            {element.name}
          </Text>
          <Text type="tertiary" size="small">
            ID: {element.element_id}
          </Text>
        </div>

        {/* Status tags */}
        <div style={{ display: "flex", gap: 4, flexWrap: "wrap", marginBottom: 8 }}>
          <Tag size="small" color="blue">{element.category}</Tag>
          <Tag size="small" color="green">{element.family}</Tag>
          <Tag size="small" color="violet">{element.type}</Tag>
          {element.is_pinned && <Tag color="orange" size="small">Pinned</Tag>}
          {element.is_view_specific && <Tag color="cyan" size="small">View Specific</Tag>}
        </div>

        {/* Search */}
        <Input
          prefix={<IconSearch />}
          value={search}
          onChange={(val) => setSearch(String(val ?? ""))}
          placeholder="Search parameters..."
          showClear
          size="small"
        />

        {totalParams > 0 && (
          <Text type="tertiary" size="small" style={{ marginTop: 4, display: "block" }}>
            {totalParams} parameters in {sortedGroups.length} groups
          </Text>
        )}
      </div>

      {/* Content */}
      <div style={{ flex: 1, overflow: "auto" }}>
        {loading && (
          <div style={{ padding: 32, textAlign: "center" }}>
            <Spin size="middle" tip="Loading parameters..." />
          </div>
        )}

        {!loading && !params && (
          <div style={{ padding: 32, textAlign: "center" }}>
            <Text type="tertiary">No parameter data available</Text>
          </div>
        )}

        {!loading && params && filteredGroups.length === 0 && search && (
          <div style={{ padding: 32, textAlign: "center" }}>
            <Text type="tertiary">No parameters match "{search}"</Text>
          </div>
        )}

        {!loading && params && filteredGroups.length > 0 && (
          <Collapse
            activeKey={expandedKeys}
            onChange={(keys) => setExpandedKeys(keys as string[])}
            style={{ borderRadius: 0 }}
          >
            {filteredGroups.map((groupName) => {
              const groupParams = params[groupName]
              const needle = search.toLowerCase()
              const filtered = search.trim()
                ? groupParams.filter(
                    (p) =>
                      p.name.toLowerCase().includes(needle) ||
                      p.value.toLowerCase().includes(needle),
                  )
                : groupParams

              return (
                <Collapse.Panel
                  key={groupName}
                  itemKey={groupName}
                  header={
                    <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                      <Text strong size="small">{groupName}</Text>
                      <Tag size="small" color="grey" style={{ minWidth: 18, textAlign: "center" }}>
                        {filtered.length}
                      </Tag>
                    </div>
                  }
                >
                  <div style={{ display: "flex", flexDirection: "column" }}>
                    {filtered.map((param) => (
                      <div
                        key={`${groupName}-${param.name}`}
                        style={{
                          display: "grid",
                          gridTemplateColumns: "1fr 1fr 28px",
                          gap: 4,
                          padding: "4px 8px",
                          borderBottom: "1px solid var(--semi-color-fill-0)",
                          alignItems: "center",
                          minHeight: 30,
                        }}
                      >
                        {/* Parameter name */}
                        <div style={{ display: "flex", alignItems: "center", gap: 4, overflow: "hidden" }}>
                          {param.is_readonly && (
                            <Tooltip content="Read-only">
                              <IconLock
                                size="extra-small"
                                style={{ color: "var(--semi-color-text-3)", flexShrink: 0 }}
                              />
                            </Tooltip>
                          )}
                          <Text
                            size="small"
                            type="tertiary"
                            ellipsis={{ showTooltip: true }}
                            style={{ lineHeight: 1.4 }}
                          >
                            {param.name}
                          </Text>
                        </div>

                        {/* Parameter value */}
                        <Text
                          size="small"
                          ellipsis={{ showTooltip: true }}
                          style={{
                            lineHeight: 1.4,
                            fontWeight: param.value ? 500 : 400,
                            color: param.value
                              ? "var(--semi-color-text-0)"
                              : "var(--semi-color-text-3)",
                          }}
                        >
                          {param.value || "\u2014"}
                        </Text>

                        {/* Copy button */}
                        <Tooltip content="Copy value">
                          <Button
                            theme="borderless"
                            icon={<IconCopy size="small" />}
                            size="small"
                            style={{
                              width: 24,
                              height: 24,
                              padding: 0,
                              opacity: 0.5,
                            }}
                            onClick={() => handleCopy(param.value || param.name)}
                          />
                        </Tooltip>
                      </div>
                    ))}
                  </div>
                </Collapse.Panel>
              )
            })}
          </Collapse>
        )}
      </div>
    </div>
  )
}
