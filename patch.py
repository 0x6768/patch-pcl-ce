#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
增强版补丁：同时处理 DeviceRegion 和 EndsWith("-CN")
"""

import json
import re
import sys
from pathlib import Path

REPLACEMENTS = {
    # 国家代码（ISO 3166-1 alpha-2）
    '"CN"': '"BV"',
    '"cn"': '"bv"',
    
    # 国家代码（ISO 3166-1 alpha-3）
    '"CHN"': '"BVT"',
    '"chn"': '"bvt"',
    
    # 数字代码
    '"156"': '"074"',
    
    # 语言代码
    '"zh-CN"': '"nb-BV"',
    '"zh-Hans-CN"': '"nb-BV"',
    '"zh-Hans"': '"nb"',
    
    # 时区
    '"China Standard Time"': '"Central European Time"',
    '"Asia/Shanghai"': '"Europe/Oslo"',
    
    # 地理名称
    '"geo-cn"': '"geo-bv"',
    '"geo-CN"': '"geo-BV"',
    
    # 国家名称
    '"China"': '"Bouvet Island"',
    '"中华人民共和国"': '"布韦岛"',
    '"中国"': '"布韦岛"',
    
    # 新增：DeviceRegion 常量
    'MainlandChinaGeoId = 45': 'MainlandChinaGeoId = 74',
    
    # 新增：EndsWith 检查
    'EndsWith("-CN"': 'EndsWith("-BV"',
}

def patch_file(filepath: str):
    path = Path(filepath)
    if not path.exists():
        print(f"[!] 文件不存在: {path}")
        return False
    
    original = path.read_text(encoding="utf-8")
    patched = original
    
    for old, new in REPLACEMENTS.items():
        if old in patched:
            count = patched.count(old)
            patched = patched.replace(old, new)
            print(f"[+] {old} -> {new} ({count} 处)")
    
    if patched == original:
        print("[!] 没有找到任何需要替换的字符串")
        return False

    path.write_text(patched, encoding="utf-8")
    print(f"[✓] 完成: {path}")
    return True


def apply_mapping(mapping_file: str = "r.json"):
    """
    读取映射文件（JSON），将源文件替换到目标路径。

    映射格式（键为源文件，值为目标路径，均支持包含空格）：
        { "PageSetupUpdate.xaml.cs": "Plain Craft Launcher 2/Pages/PageSetup/PageSetupUpdate.xaml.cs" }
    """
    path = Path(mapping_file)
    if not path.exists():
        print(f"[!] 映射文件不存在: {path}")
        return False

    try:
        mapping = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as ex:
        print(f"[!] 映射文件解析失败: {path} ({ex})")
        return False

    if not isinstance(mapping, dict):
        print(f"[!] 映射文件格式应为 JSON 对象: {path}")
        return False

    all_ok = True
    for src, dst in mapping.items():
        src_path = Path(str(src))
        dst_path = Path(str(dst))
        if not src_path.exists():
            print(f"[!] 源文件不存在: {src_path}")
            all_ok = False
            continue

        src_text = src_path.read_text(encoding="utf-8")
        replaced = src_text
        for old, new in REPLACEMENTS.items():
            replaced = replaced.replace(old, new)

        dst_path.parent.mkdir(parents=True, exist_ok=True)
        dst_path.write_text(replaced, encoding="utf-8")
        print(f"[✓] 替换: {src_path} -> {dst_path}")

    return all_ok


# Config.cs 中 UpdateMode 属性要改成的目标（AST Patch 针对的属性节点）
UPDATE_MODE_ATTRIBUTE = (
    '[ConfigItem<LauncherAutoUpdateBehavior>("SystemSystemUpdate", '
    'LauncherAutoUpdateBehavior.Disable, ConfigSource.Local)]'
)
UPDATE_MODE_DECLARATION = "public partial LauncherAutoUpdateBehavior UpdateMode { get; set; }"


def patch_config(config_path: str):
    """
    对 PCL.Core/App/Config.cs 进行 AST 修补：
    定位 UpdateMode 属性节点（含其前的 ConfigItem 特性行），整体改为指定声明。
    """
    path = Path(config_path)
    if not path.exists():
        print(f"[!] 配置文件不存在: {path}")
        return False

    text = path.read_text(encoding="utf-8")

    # 匹配整条 UpdateMode 属性节点：可选的前置特性行 + 属性声明
    pattern = re.compile(
        r'(?m)^(?P<indent>[ \t]*)(?:\[[^\]\r\n]*\][ \t]*\r?\n[ \t]*)?'
        r'public partial LauncherAutoUpdateBehavior UpdateMode[ \t]*'
        r'\{[ \t]*get;[ \t]*set;[ \t]*\}'
    )

    changed = [0]

    def repl(m):
        changed[0] += 1
        indent = m.group("indent")
        return indent + UPDATE_MODE_ATTRIBUTE + "\n" + indent + UPDATE_MODE_DECLARATION

    patched = pattern.sub(repl, text)

    if changed[0] == 0:
        print(f"[!] 未找到 UpdateMode 属性，AST 修补未生效: {path}")
        return False

    path.write_text(patched, encoding="utf-8")
    print(f"[✓] AST 修补完成（{changed[0]} 处）: {path}")
    return True


def run_defaults():
    """
    无参数模式：同时执行 r.json 映射、RegionPolicy 修补、Config.cs 的 AST 修补。
    不要求任何命令行参数，也绝不生成 .bak。
    """
    results = []

    default_region_path = Path("PCL.Core/App/Region/RegionPolicy.cs")
    if default_region_path.exists():
        print("[+] 执行 RegionPolicy 修补")
        results.append(patch_file(str(default_region_path)))
    else:
        print(f"[!] 跳过 RegionPolicy（文件不存在）: {default_region_path}")
        results.append(False)

    mapping_path = Path("r.json")
    if mapping_path.exists():
        print("[+] 执行 r.json 映射替换")
        results.append(apply_mapping("r.json"))
    else:
        print(f"[!] 跳过 r.json（文件不存在）: {mapping_path}")
        results.append(False)

    config_path = Path("PCL.Core/App/Config.cs")
    if config_path.exists():
        print("[+] 执行 Config.cs AST 修补")
        results.append(patch_config(str(config_path)))
    else:
        print(f"[!] 跳过 Config.cs（文件不存在）: {config_path}")
        results.append(False)

    # 只要存在一个可处理项（无论是否找到替换内容）即视为本次执行成功
    return all(results)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        # 无参数模式：r.json / RegionPolicy / Config.cs 同时执行
        print("[+] 未传入参数，执行默认全部修补（r.json + RegionPolicy + Config.cs）")
        sys.exit(0 if run_defaults() else 1)

    success = patch_file(sys.argv[1])
    sys.exit(0 if success else 1)

    # PCL.Core/App/Region/RegionPolicy.cs