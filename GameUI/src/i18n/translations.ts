export type TranslationKey =
  | "app.title"
  | "app.open"
  | "summary.none"
  | "summary.selected"
  | "section.tools"
  | "section.parcels"
  | "section.selected"
  | "section.move"
  | "section.vertices"
  | "section.appearance"
  | "tool.mapOn"
  | "tool.mapOff"
  | "tool.newRectangle"
  | "tooltip.mapTool"
  | "tooltip.newRectangle"
  | "tooltip.selectParcel"
  | "tooltip.mergeTarget"
  | "tooltip.selectVertex"
  | "state.active"
  | "state.locked"
  | "parcel.defaultName"
  | "parcel.selected"
  | "parcel.target"
  | "parcel.merge"
  | "selected.rename"
  | "action.prev"
  | "action.next"
  | "action.delete"
  | "action.insertVertex"
  | "action.deleteVertex"
  | "action.confirm"
  | "action.cancel"
  | "move.step"
  | "move.north"
  | "move.south"
  | "move.west"
  | "move.east"
  | "vertices.empty"
  | "merge.message"
  | "appearance.showVanilla"
  | "appearance.color"
  | "appearance.red"
  | "appearance.green"
  | "appearance.blue"
  | "appearance.borderOpacity"
  | "appearance.fillOpacity"
  | "appearance.width";

export const translations: Record<"en" | "zhHans", Record<TranslationKey, string>> = {
  en: {
    "app.title": "Custom Land Parcel",
    "app.open": "Open Custom Land Parcel panel",
    "summary.none": "no selection",
    "summary.selected": "{count} parcels / {points} pts / vertex {vertex}",
    "section.tools": "Tools",
    "section.parcels": "Parcels",
    "section.selected": "Selected",
    "section.move": "Move",
    "section.vertices": "Vertices",
    "section.appearance": "Appearance",
    "tool.mapOn": "Map Tool On",
    "tool.mapOff": "Map Tool Off",
    "tool.newRectangle": "New Rectangle",
    "tooltip.mapTool": "Toggle map parcel edit tool",
    "tooltip.newRectangle": "Add a rectangular parcel",
    "tooltip.selectParcel": "Select parcel",
    "tooltip.mergeTarget": "Use as merge target",
    "tooltip.selectVertex": "Select vertex",
    "state.active": "Active",
    "state.locked": "Locked",
    "parcel.defaultName": "Parcel",
    "parcel.selected": "Selected",
    "parcel.target": "Target",
    "parcel.merge": "Merge",
    "selected.rename": "Rename selected parcel",
    "action.prev": "Prev",
    "action.next": "Next",
    "action.delete": "Delete",
    "action.insertVertex": "Insert Vertex",
    "action.deleteVertex": "Delete Vertex",
    "action.confirm": "OK",
    "action.cancel": "Cancel",
    "move.step": "Step",
    "move.north": "N",
    "move.south": "S",
    "move.west": "W",
    "move.east": "E",
    "vertices.empty": "No selected parcel.",
    "merge.message": "Merge {selected} with {target}",
    "appearance.showVanilla": "Vanilla border",
    "appearance.color": "Color",
    "appearance.red": "R",
    "appearance.green": "G",
    "appearance.blue": "B",
    "appearance.borderOpacity": "Line opacity",
    "appearance.fillOpacity": "Fill opacity",
    "appearance.width": "Line width",
  },
  zhHans: {
    "app.title": "自定义地块",
    "app.open": "打开自定义地块面板",
    "summary.none": "未选择",
    "summary.selected": "{count} 个地块 / {points} 个节点 / 节点 {vertex}",
    "section.tools": "工具",
    "section.parcels": "地块",
    "section.selected": "当前地块",
    "section.move": "移动",
    "section.vertices": "节点",
    "section.appearance": "外观",
    "tool.mapOn": "地图工具 开",
    "tool.mapOff": "地图工具 关",
    "tool.newRectangle": "新建矩形",
    "tooltip.mapTool": "切换地图地块编辑工具",
    "tooltip.newRectangle": "添加一个矩形地块",
    "tooltip.selectParcel": "选择地块",
    "tooltip.mergeTarget": "设为合并目标",
    "tooltip.selectVertex": "选择节点",
    "state.active": "启用",
    "state.locked": "锁定",
    "parcel.defaultName": "地块",
    "parcel.selected": "已选",
    "parcel.target": "目标",
    "parcel.merge": "合并",
    "selected.rename": "重命名当前地块",
    "action.prev": "上一个",
    "action.next": "下一个",
    "action.delete": "删除",
    "action.insertVertex": "插入节点",
    "action.deleteVertex": "删除节点",
    "action.confirm": "确定",
    "action.cancel": "取消",
    "move.step": "步长",
    "move.north": "北",
    "move.south": "南",
    "move.west": "西",
    "move.east": "东",
    "vertices.empty": "没有选中的地块。",
    "merge.message": "合并 {selected} 和 {target}",
    "appearance.showVanilla": "显示原版虚线",
    "appearance.color": "颜色",
    "appearance.red": "红",
    "appearance.green": "绿",
    "appearance.blue": "蓝",
    "appearance.borderOpacity": "线透明度",
    "appearance.fillOpacity": "填充透明度",
    "appearance.width": "线宽",
  },
};
