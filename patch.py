#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
补丁脚本：字符串替换 + r.json 映射替换。
Config.cs AST 修补已移除。
"""

import json
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

    # DeviceRegion 常量
    'MainlandChinaGeoId = 45': 'MainlandChinaGeoId = 74',

    # EndsWith 检查
    'EndsWith("-CN"': 'EndsWith("-BV"',
}


def patch_file(filepath: str) -> bool:
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
        print(f"[!] 没有找到需要替换的字符串（跳过）: {path}")
        return True

    path.write_text(patched, encoding="utf-8")
    print(f"[✓] 完成: {path}")
    return True


def apply_mapping(mapping_file: str = "r.json") -> bool:
    """
    读取映射文件（JSON），将源文件内容做替换后写入目标路径。

    映射格式：
        { "源文件名": "目标路径" }
    """
    path = Path(mapping_file)
    if not path.exists():
        print(f"[!] 映射文件不存在（跳过）: {path}")
        return True

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
            print(f"[!] 源文件不存在（跳过）: {src_path}")
            all_ok = False
            continue

        src_text = src_path.read_text(encoding="utf-8")
        replaced = src_text
        for old, new in REPLACEMENTS.items():
            replaced = replaced.replace(old, new)

        dst_path.parent.mkdir(parents=True, exist_ok=True)
        dst_path.write_text(replaced, encoding="utf-8")
        print(f"[✓] 映射替换: {src_path} -> {dst_path}")

    return all_ok


def run_defaults():
    """
    无参数模式：RegionPolicy + r.json 映射。
    """
    results = []

    # RegionPolicy.cs
    default_region = Path("PCL.Core/App/Region/RegionPolicy.cs")
    if default_region.exists():
        print("[+] 执行 RegionPolicy 修补")
        results.append(patch_file(str(default_region)))
    else:
        print(f"[!] 跳过 RegionPolicy（文件不存在）: {default_region}")
        results.append(True)

    # r.json 映射
    results.append(apply_mapping("r.json"))

    return all(results)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("[+] 未传入参数，执行默认修补（RegionPolicy + r.json）")
        sys.exit(0 if run_defaults() else 1)

    success = patch_file(sys.argv[1])
    sys.exit(0 if success else 1)