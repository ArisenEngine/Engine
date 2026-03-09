import os
import sys
import xml.etree.ElementTree as ET
import xml.dom.minidom as minidom

def ensure_property_group_with_output(root, config, path):
    # Find or create a PropertyGroup for the given configuration
    pg = ET.SubElement(root, "PropertyGroup", Condition=f"'$(Configuration)' == '{config}'")
    ET.SubElement(pg, "OutputPath").text = path
    # Also add the flag to each configured PropertyGroup to be safe
    ET.SubElement(pg, "AppendTargetFrameworkToOutputPath").text = "false"

def prettify(elem):
    rough_string = ET.tostring(elem, 'utf-8')
    reparsed = minidom.parseString(rough_string)
    # Remove the extra <?xml version="1.0" ?> header from minidom if it's redundant
    # but for .user files it's fine.
    return reparsed.toprettyxml(indent="  ")

def update_output_path(csproj_path, sln_dir, base_output):
    base_output_abs = os.path.abspath(base_output)
    csproj_dir = os.path.dirname(os.path.abspath(csproj_path))
    
    # Detect if it's a package and determine its category
    package_category = None
    package_name = None
    
    parts = csproj_path.replace("\\", "/").split("/")
    if "Packages" in parts:
        pkg_idx = parts.index("Packages")
        if pkg_idx + 1 < len(parts):
            package_name = parts[pkg_idx + 1]
            package_category = ""
            if pkg_idx + 2 < len(parts) and parts[pkg_idx + 2].lower().endswith(".csproj") == False:
                 package_category = parts[pkg_idx + 1]
                 package_name = parts[pkg_idx + 2]

    user_file = csproj_path + ".user"
    root = ET.Element("Project")
    
    # Global flag in a common PropertyGroup
    common_pg = ET.SubElement(root, "PropertyGroup")
    ET.SubElement(common_pg, "AppendTargetFrameworkToOutputPath").text = "false"

    configs = ["Debug", "Release"]
    for config in configs:
        target_output = os.path.join(base_output_abs, config)
        if package_name:
            if package_category:
                target_output = os.path.join(target_output, "Packages", package_category, package_name)
            else:
                target_output = os.path.join(target_output, "Packages", package_name)
        
        output_rel = os.path.relpath(target_output, csproj_dir)
        ensure_property_group_with_output(root, config, output_rel + os.sep)

    xml_str = prettify(root)
    with open(user_file, "w", encoding="utf-8") as f:
        f.write(xml_str)

def extract_csproj_paths_from_sln(sln_path):
    csproj_paths = []
    if not os.path.exists(sln_path):
        return csproj_paths
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

if len(sys.argv) != 3:
    print("Usage: python update_csproj_output.py path/to/solution.sln Outputs/")
    sys.exit(1)

sln_file = sys.argv[1]
output_base = os.path.abspath(sys.argv[2])
sln_dir = os.path.dirname(os.path.abspath(sln_file))
print("Update csproj Output base:" + output_base)

csproj_files = extract_csproj_paths_from_sln(sln_file)
for csproj in csproj_files:
    if os.path.exists(csproj):
        print(f"Updating {csproj}.user ...")
        update_output_path(csproj, sln_dir, output_base)
    else:
        print(f"Warning: .csproj not found: {csproj}")

print("Done.")
