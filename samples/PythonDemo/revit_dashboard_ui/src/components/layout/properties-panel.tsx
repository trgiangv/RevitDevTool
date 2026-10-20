/**
 * Properties Panel — Revit-style Properties Pane.
 * Shows grouped, collapsible parameter sections fetched via getElementParameters.
 * Features: read-only indicator, copy button, status tags, search.
 */

import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import {
  Button,
  Collapse,
  Empty,
  Input,
  Resizable,
  SideSheet,
  Space,
  Spin,
  Tag,
  Tooltip,
  Typography,
} from "@douyinfe/semi-ui"
import { IconSearch } from "@douyinfe/semi-icons"
import { Copy, Lock } from "lucide-react"
import { toast } from "@/lib/toast"
import { useBridge } from "@/providers/bridge-provider"
import type { ElementRow } from "@/types"

const { Text } = Typography

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

const NAME_RESIZE = {
  top: false,
  right: true,
  bottom: false,
  left: false,
  topLeft: false,
  topRight: false,
  bottomLeft: false,
  bottomRight: false,
}

function ParameterRow({
  param,
  nameWidth,
  onNameWidth,
  onCopy,
}: Readonly<{
  param: ParameterInfo
  nameWidth: string
  onNameWidth: (width: string) => void
  onCopy: (value: string) => void
}>) {
  return (
    <div style={{ display: "flex", alignItems: "stretch", width: "100%", minWidth: 0 }}>
      <Resizable
        size={{ width: nameWidth, height: "auto" }}
        minWidth={72}
        maxWidth="70%"
        enable={NAME_RESIZE}
        onChange={(size) => {
          if (typeof size.width === "string") onNameWidth(size.width)
        }}
        style={{ overflow: "hidden" }}
        handleNode={{
          right: (
            <span
              style={{
                display: "block",
                width: 1,
                height: "100%",
                margin: "0 auto",
                backgroundColor: "var(--semi-color-border)",
              }}
            />
          ),
        }}
      >
        <span
          style={{
            display: "flex",
            alignItems: "center",
            gap: 4,
            height: "100%",
            minWidth: 0,
          }}
        >
          {param.is_readonly ? (
            <Tooltip content="Read-only">
              <Lock size={12} />
            </Tooltip>
          ) : null}
          <Text
            size="small"
            type="tertiary"
            ellipsis={{ showTooltip: true }}
            style={{ flex: 1, minWidth: 0 }}
          >
            {param.name}
          </Text>
        </span>
      </Resizable>
      <Space
        spacing="tight"
        style={{ flex: 1, minWidth: 0, justifyContent: "space-between", marginLeft: 8 }}
      >
        <Text
          size="small"
          type={param.value ? "primary" : "tertiary"}
          ellipsis={{ showTooltip: true }}
          style={{ flex: 1, minWidth: 0 }}
        >
          {param.value || "\u2014"}
        </Text>
        <Tooltip content="Copy value">
          <Button
            theme="borderless"
            type="tertiary"
            icon={<Copy size={12} />}
            size="small"
            aria-label={`Copy ${param.name}`}
            onClick={() => onCopy(param.value || param.name)}
          />
        </Tooltip>
      </Space>
    </div>
  )
}

function groupSortKey(name: string): number {
  const idx = GROUP_ORDER.findIndex((g) => name.toLowerCase().includes(g.toLowerCase()))
  return idx >= 0 ? idx : GROUP_ORDER.length
}

export function PropertiesPanel({ element, visible, onClose }: Readonly<PropertiesPanelProps>) {
  const bridge = useBridge()
  const [params, setParams] = useState<GroupedParameters | null>(null)
  const [loading, setLoading] = useState(false)
  const [search, setSearch] = useState("")
  const [expandedKeys, setExpandedKeys] = useState<string[]>([])
  const [nameWidth, setNameWidth] = useState("46%")
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
        (p) => p.name.toLowerCase().includes(needle) || p.value.toLowerCase().includes(needle),
      ),
    )
  }, [params, sortedGroups, search])

  const totalParams = params ? Object.values(params).reduce((sum, list) => sum + list.length, 0) : 0

  return (
    <SideSheet
      title={element?.name ?? "Properties"}
      visible={visible && element != null}
      onCancel={onClose}
      width={400}
      mask={false}
      disableScroll={false}
      closeOnEsc
      bodyStyle={{
        padding: "8px 12px",
        display: "flex",
        flexDirection: "column",
        height: "100%",
        boxSizing: "border-box",
      }}
    >
      {element ? (
        <Space
          vertical
          spacing="medium"
          style={{ width: "100%", height: "100%", alignItems: "stretch" }}
        >
          <Text type="tertiary" size="small">
            ID: {element.element_id}
          </Text>
          <Space wrap style={{ width: "100%" }}>
            <Tag size="small" color="blue">
              {element.category}
            </Tag>
            <Tag size="small" color="green">
              {element.family}
            </Tag>
            <Tag
              size="small"
              color="violet"
              style={{ height: "auto", maxWidth: "100%", whiteSpace: "normal" }}
            >
              {element.type}
            </Tag>
            {element.is_pinned ? (
              <Tag color="orange" size="small">
                Pinned
              </Tag>
            ) : null}
            {element.is_view_specific ? (
              <Tag color="cyan" size="small">
                View Specific
              </Tag>
            ) : null}
          </Space>
          <Input
            prefix={<IconSearch />}
            value={search}
            onChange={(val) => setSearch(String(val ?? ""))}
            placeholder="Search parameters"
            showClear
          />
          <Spin
            spinning={loading}
            tip="Loading parameters..."
            style={{ width: "100%", flex: 1, minHeight: 0 }}
          >
            {loading ? (
              <div style={{ height: "100%", minHeight: 160 }} />
            ) : (
              <>
                {totalParams > 0 ? (
                  <Text type="tertiary" size="small">
                    {totalParams} parameters in {sortedGroups.length} groups
                  </Text>
                ) : null}
                {!params ? <Empty description="No parameter data available" /> : null}
                {params && filteredGroups.length === 0 && search ? (
                  <Empty description={`No parameters match "${search}"`} />
                ) : null}
                {params && filteredGroups.length > 0 ? (
                  <Collapse
                    activeKey={expandedKeys}
                    onChange={(keys) => setExpandedKeys(keys as string[])}
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
                            <Space>
                              <Text strong size="small">
                                {groupName}
                              </Text>
                              <Tag size="small" color="grey">
                                {filtered.length}
                              </Tag>
                            </Space>
                          }
                        >
                          <Space
                            vertical
                            spacing={2}
                            style={{ width: "100%", alignItems: "stretch" }}
                          >
                            {filtered.map((param) => (
                              <ParameterRow
                                key={param.name}
                                param={param}
                                nameWidth={nameWidth}
                                onNameWidth={setNameWidth}
                                onCopy={handleCopy}
                              />
                            ))}
                          </Space>
                        </Collapse.Panel>
                      )
                    })}
                  </Collapse>
                ) : null}
              </>
            )}
          </Spin>
        </Space>
      ) : null}
    </SideSheet>
  )
}
