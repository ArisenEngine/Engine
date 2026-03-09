import os
import sys
import xml.etree.ElementTree as ET
import xml.dom.minidom as minidom


def prettify(elem):
    rough_string = ET.tostring(elem, 'utf-8')
    reparsed = minidom.parseString(rough_string)
    # Remove the extra <?xml version="1.0" ?> header from minidom if it's redundant
    # but for .user files it's fine.
    return reparsed.toprettyxml(indent="  ")

def update_output_path(csproj_path, sln_name, base_output):
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
    
    if os.path.exists(user_file):
        try:
            tree = ET.parse(user_file)
            root = tree.getroot()
        except:
            root = ET.Element("Project")
    else:
        root = ET.Element("Project")

    # Helper to find existing PropertyGroup by condition
    def find_pg(root, condition):
        for pg in root.findall("PropertyGroup"):
            if pg.get("Condition") == condition:
                return pg
        return None

    # Helper to remove PGs for the current solution or legacy unqualified ones
    def clear_relevant_pgs(root, sln_name):
        to_remove = []
        for pg in root.findall("PropertyGroup"):
            cond = pg.get("Condition")
            if not cond:
                continue
            
            # Remove if it belongs to this solution
            if f"$(SolutionName)' == '{sln_name}'" in cond:
                to_remove.append(pg)
            # Remove if it's a legacy unqualified configuration PG (only has Configuration but no SolutionName)
            elif "Condition=\"'$(Configuration)' == '" in ET.tostring(pg, encoding="unicode") or \
                 (cond.startswith("'$(Configuration)' == '") and "SolutionName" not in cond):
                 # Check if it actually contains OutputPath to avoid deleting unrelated user settings
                 if pg.find("OutputPath") is not None:
                     to_remove.append(pg)

        for pg in to_remove:
            root.remove(pg)

    clear_relevant_pgs(root, sln_name)

    # Ensure common PropertyGroup for AppendTargetFrameworkToOutputPath if not present
    # We apply this globally for now, or we could also make it solution specific.
    # But usually this is safer globally in .user if we want to bypass the SDK default.
    common_pg = find_pg(root, None)
    if common_pg is None:
        common_pg = ET.SubElement(root, "PropertyGroup")
    
    if common_pg.find("AppendTargetFrameworkToOutputPath") is None:
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
        
        # New condition: SolutionName AND Configuration
        condition = f"('$(SolutionName)' == '{sln_name}') AND ('$(Configuration)' == '{config}')"
        pg = ET.SubElement(root, "PropertyGroup", Condition=condition)
        ET.SubElement(pg, "OutputPath").text = output_rel + os.sep
        ET.SubElement(pg, "AppendTargetFrameworkToOutputPath").text = "false"

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
sln_name = os.path.splitext(os.path.basename(sln_file))[0]
print(f"Update csproj Output base: {output_base} for Solution: {sln_name}")

csproj_files = extract_csproj_paths_from_sln(sln_file)
for csproj in csproj_files:
    if os.path.exists(csproj):
        print(f"Updating {csproj}.user ...")
        update_output_path(csproj, sln_name, output_base)
    else:
        print(f"Warning: .csproj not found: {csproj}")

print("Done.")
