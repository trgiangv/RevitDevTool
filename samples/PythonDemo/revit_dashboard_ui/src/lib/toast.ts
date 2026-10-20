import { Toast } from "@douyinfe/semi-ui"

function show(kind: "success" | "error" | "info", content: string, description?: string) {
  Toast[kind]({
    content: description ? `${content} — ${description}` : content,
    theme: "light",
  })
}

export const toast = {
  success(content: string, options?: { description?: string }) {
    show("success", content, options?.description)
  },
  error(content: string, options?: { description?: string }) {
    show("error", content, options?.description)
  },
  info(content: string, options?: { description?: string }) {
    show("info", content, options?.description)
  },
}
