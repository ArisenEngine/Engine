import uuid
import re
import sys
from pathlib import Path

def group_sln_projects_incremental(sln_path: str, rules: dict):
    with open(sln_path, "r", encoding="utf-8-sig") as f:
        content = f.read()

    project_pattern = re.compile(r'Project\(".*?"\) = "(.*?)", ".*?", "\{(.*?)\}"', re.MULTILINE)
    existing_projects = project_pattern.findall(content)
    project_guid_map = {name: guid for name, guid in existing_projects}

    # 提取已存在的 Solution Folders（名称 -> GUID）
    folder_pattern = re.compile(r'Project\("\{2150E333-8FDC-42A3-9474-1A3956D46DE8\}"\) = "(.*?)", ".*?", "\{(.*?)\}"')
    existing_folders = {name: guid for name, guid in folder_pattern.findall(content)}

    # 提取现有 NestedProjects（项目 GUID -> 文件夹 GUID）
    nested_projects_pattern = re.compile(r'GlobalSection\(NestedProjects\) = preSolution\n(.*?)EndGlobalSection', re.DOTALL)
    nested_projects_block = nested_projects_pattern.search(content)
    nested_lines = nested_projects_block.group(1).splitlines() if nested_projects_block else []
    nested_map = dict(re.findall(r'\{(.*?)\} = \{(.*?)\}', "\n".join(nested_lines)))

    # 修改或新增分组映射
    updated_nested_lines = {**nested_map}
    folder_guids = {**existing_folders}

    for name, guid in project_guid_map.items():
        for folder, patterns in rules.items():
            if any(name == pat or name.startswith(pat) for pat in patterns):
                if folder not in folder_guids:
                    folder_guids[folder] = str(uuid.uuid4()).upper()
                    # 添加 Solution Folder 项目块（如果不存在）
                    folder_proj = f'Project("{{2150E333-8FDC-42A3-9474-1A3956D46DE8}}") = "{folder}", "{folder}", "{{{folder_guids[folder]}}}"\nEndProject\n'
                    insert_index = content.rfind("EndProject") + len("EndProject\n")
                    content = content[:insert_index] + folder_proj + content[insert_index:]
                updated_nested_lines[guid.upper()] = folder_guids[folder]

    # 重建 NestedProjects 块（合并后）
    nested_text = "GlobalSection(NestedProjects) = preSolution\n"
    for proj_guid, folder_guid in sorted(updated_nested_lines.items()):
        nested_text += f"\t\t{{{proj_guid}}} = {{{folder_guid}}}\n"
    nested_text += "EndGlobalSection\n"

    if nested_projects_block:
        content = re.sub(r'GlobalSection\(NestedProjects\) = preSolution\n(.*?)EndGlobalSection\n',
                         nested_text, content, flags=re.DOTALL)
    else:
        content = content.replace("Global\n", f"Global\n\t{nested_text}", 1)

    with open(sln_path, "w", encoding="utf-8") as f:
        f.write(content)

    print(f"Updated solution file incrementally: {sln_path}")

if __name__ == "__main__":
    rules = {
        "ToolChain": ["Serialization"],
        "Binding": ["BindingGenerator", "AutoBinding"],
        "Editor": [
            "ArisenEditor", "ArisenEditor.Desktop", "ArisenEditorShell",
            "ArisenLauncher", "ArisenLauncher.Desktop",
            "Avalonia.", "Dock.", "ArisenEditorFramework"
        ],
        "Runtime": ["ArisenEngine"],
        "Test": ["EditorTest", "CSharpEngineTest"]
    }

    if len(sys.argv) < 2:
        print("Usage: python group_sln_incremental.py <path/to/solution.sln>")
        sys.exit(1)

    group_sln_projects_incremental(sys.argv[1], rules)