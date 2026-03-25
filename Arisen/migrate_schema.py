import os
import json

root_dir = r"E:\Jingwen\ArisenEngine\Engine\Arisen"
for dirpath, dirnames, filenames in os.walk(root_dir):
    if "3rdparty" in dirpath:
        continue
    for f in filenames:
        if f == "package.json":
            filepath = os.path.join(dirpath, f)
            with open(filepath, 'r', encoding='utf-8') as file:
                try:
                    data = json.load(file)
                except:
                    continue
            
            modified = False
            
            # Migration logic
            entry_assembly = None
            entry_class = None
            
            if "entryAssembly" in data:
                entry_assembly = data.pop("entryAssembly")
                modified = True
            if "entryClass" in data:
                entry_class = data.pop("entryClass")
                modified = True
            if "assemblyEntry" in data:
                entry_assembly = data.pop("assemblyEntry")
                modified = True
            if "Entry" in data and isinstance(data["Entry"], dict):
                entry_data = data.pop("Entry")
                if "Assembly" in entry_data:
                    entry_assembly = entry_data["Assembly"]
                if "Class" in entry_data:
                    entry_class = entry_data["Class"]
                modified = True
                
            if modified and (entry_assembly or entry_class):
                data["entry"] = {}
                if entry_assembly:
                    data["entry"]["assembly"] = entry_assembly
                if entry_class:
                    data["entry"]["class"] = entry_class
                    
            if modified:
                with open(filepath, 'w', encoding='utf-8') as file:
                    json.dump(data, file, indent=2)
                print(f"Migrated {filepath}")
