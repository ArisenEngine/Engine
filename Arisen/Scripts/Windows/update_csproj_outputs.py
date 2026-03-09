import os
import sys
import xml.etree.ElementTree as ET

def ensure_property_group_with_output(tree, config, path):
    root = tree.getroot()
    ns = {'': root.tag[root.tag.find("{")+1:root.tag.find("}")]} if root.tag.startswith("{") else {}

    found = False
    for pg in root.findall("PropertyGroup", ns):
        cond = pg.get("Condition")
        if cond and f"'$(Configuration)' == '{config}'" in cond:
            op = pg.find("OutputPath", ns)
            if op is not None:
                op.text = path
            else:
                ET.SubElement(pg, "OutputPath").text = path
            found = True
            break

    if not found:
        pg = ET.Element("PropertyGroup", Condition=f"'$(Configuration)' == '{config}'")
        ET.SubElement(pg, "OutputPath").text = path
        root.append(pg)

def ensure_framework_flag(tree):
    root = tree.getroot()
    ns = {'': root.tag[root.tag.find("{")+1:root.tag.find("}")]} if root.tag.startswith("{") else {}
    
    found = False
    for pg in root.findall("PropertyGroup", ns):
        if pg.find("AppendTargetFrameworkToOutputPath", ns) is not None:
            pg.find("AppendTargetFrameworkToOutputPath", ns).text = "false"
            found = True
            break

    if not found:
        pg = root.find("PropertyGroup", ns)
        if pg is None:
            pg = ET.SubElement(root, "PropertyGroup")
        ET.SubElement(pg, "AppendTargetFrameworkToOutputPath").text = "false"

def update_output_path(csproj_path, sln_dir, base_output):
    base_output_abs = os.path.abspath(base_output)
    csproj_dir = os.path.dirname(os.path.abspath(csproj_path))
    csproj_name = os.path.basename(csproj_path)

    # Detect if it's a package and determine its category
    package_category = None
    package_name = None
    
    parts = csproj_path.replace("\\", "/").split("/")
    if "Packages" in parts:
        pkg_idx = parts.index("Packages")
        if pkg_idx + 1 < len(parts):
            # The next part is the package folder name (e.g., com.arisen.generic-renderpipeline)
            package_name = parts[pkg_idx + 1]
            package_category = "" # No category for root packages
            
            # If there's another level (like old Builtin/ForwardRP), handle it
            if pkg_idx + 2 < len(parts) and parts[pkg_idx + 2].lower().endswith(".csproj") == False:
                 package_category = parts[pkg_idx + 1]
                 package_name = parts[pkg_idx + 2]

    tree = ET.parse(csproj_path)

    configs = ["Debug", "Release"]
    for config in configs:
        target_output = os.path.join(base_output_abs, config)
        if package_name:
            if package_category:
                target_output = os.path.join(target_output, "Packages", package_category, package_name)
            else:
                target_output = os.path.join(target_output, "Packages", package_name)
        
        # Output 相对于 csproj 的相对路径
        output_rel = os.path.relpath(target_output, csproj_dir)
        ensure_property_group_with_output(tree, config, output_rel + os.sep)

    ensure_framework_flag(tree)
    tree.write(csproj_path, encoding="utf-8", xml_declaration=True)


def extract_csproj_paths_from_sln(sln_path):
    csproj_paths = []
    with open(sln_path, "r", encoding="utf-8") as f:
        for line in f:
            if line.strip().startswith("Project(") and ".csproj" in line:
                parts = line.split(",")
                if len(parts) >= 2:
                    path = parts[1].strip().strip('"').replace("\\", os.sep)
                    full_path = os.path.normpath(os.path.join(os.path.dirname(sln_path), path))
                    if "3rdparty" not in full_path.replace("\\", "/").lower():
                        csproj_paths.append(full_path)
    return csproj_paths

# Entry point
if len(sys.argv) != 3:
    print("Usage: python update_csproj_output.py path/to/solution.sln Outputs/")
    sys.exit(1)

sln_file = sys.argv[1]
output_base = os.path.abspath(sys.argv[2])
sln_dir = os.path.dirname(os.path.abspath(sln_file))
print("Update csproj Output base:" + output_base)
if not os.path.exists(sln_file):
    print(f"Error: Solution file not found: {sln_file}")
    sys.exit(1)

csproj_files = extract_csproj_paths_from_sln(sln_file)

for csproj in csproj_files:
    if os.path.exists(csproj):
        print(f"Updating {csproj}...")
        update_output_path(csproj, sln_dir, output_base)
    else:
        print(f"Warning: .csproj not found: {csproj}")
        
print("Done.")
