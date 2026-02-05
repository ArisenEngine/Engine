import re
import os

CORE_RHI_DIR = r"e:\Jingwen\ArisenEngine\Engine\Arisen\Core\Core.RHI\RHI"
EXPORTS_DIR = r"e:\Jingwen\ArisenEngine\Engine\Arisen\Engine\NativeEngine\RHI"

TYPE_MAP = {
    "RHIBufferHandle": "RHI_BufferHandle", "RHIImageHandle": "RHI_ImageHandle",
    "RHIImageViewHandle": "RHI_ImageViewHandle", "RHISamplerHandle": "RHI_SamplerHandle",
    "RHIShaderHandle": "RHI_ShaderHandle", "RHIPipelineHandle": "RHI_PipelineHandle",
    "RHICommandBufferHandle": "RHI_CommandBufferHandle", "RHIDescriptorSetHandle": "RHI_DescriptorSetHandle",
    "RHIFenceHandle": "RHI_FenceHandle", "RHISemaphoreHandle": "RHI_SemaphoreHandle",
    "RHIRenderPassHandle": "RHI_RenderPassHandle", "RHIFrameBufferHandle": "RHI_FrameBufferHandle",
    "RHICommandBufferPoolHandle": "RHI_CommandBufferPoolHandle", "RHIShaderProgramHandle": "RHI_GPUProgramHandle",
    "UInt32": "unsigned int", "SInt32": "int", "UInt64": "unsigned long long",
    "Float32": "float", "bool": "bool", "void": "void"
}

OPAQUE_HANDLES = {
    "RHIInstance": "RHI_InstanceHandle", "RHIDevice": "RHI_DeviceHandle",
    "RHISurface": "RHI_SurfaceHandle", "RHISwapChain": "RHI_SwapChainHandle",
    "RHICommandBuffer": "RHI_CommandBufferHandle", "RHIDescriptorPool": "RHI_DescriptorPoolHandle",
    "RHIPipelineManager": "RHI_PipelineManagerHandle", "RHIFactory": "RHI_FactoryHandle",
}

def map_single_type(mid):
    mid = mid.strip()
    if mid in TYPE_MAP: return TYPE_MAP[mid]
    if mid in OPAQUE_HANDLES: return OPAQUE_HANDLES[mid]
    if mid.startswith("E") or "::E" in mid:
        if "::" not in mid: return f"ArisenEngine::RHI::{mid}"
    if mid in ["RenderPassBeginDesc", "RHIRenderingInfo", "RHIDescriptorImageInfo", "RHIBufferImageCopy", "RHIMemoryBarrier", "RHIImageMemoryBarrier", "RHIBufferMemoryBarrier", "RHIDescriptorUpdateEntry", "RHIDescriptorSet"]:
        return f"ArisenEngine::RHI::{mid}"
    return mid

def map_type(t):
    t = t.replace("inline ", "").replace("virtual ", "").strip()
    suf = ""
    if t.endswith("&&"): suf = "&&"; t = t[:-2].strip()
    elif t.endswith("&"): suf = "&"; t = t[:-1].strip()
    elif t.endswith("*"): suf = "*"; t = t[:-1].strip()
    pre = ""
    if t.startswith("const "): pre = "const "; t = t[6:].strip()

    if "Vector<" in t:
        inner = t[t.find("<")+1:t.rfind(">")].strip()
        if "shared_ptr<" in inner:
            si = inner[inner.find("<")+1:inner.rfind(">")].strip()
            itt = f"std::shared_ptr<{map_single_type(si)}>"
        else: itt = map_single_type(inner)
        return f"{pre}ArisenEngine::Containers::Vector<{itt}>{suf}".strip()
    
    if "shared_ptr<" in t:
        inner = t[t.find("<")+1:t.rfind(">")].strip()
        return f"{pre}std::shared_ptr<{map_single_type(inner)}>{suf}".strip()
        
    return f"{pre}{map_single_type(t)}{suf}".strip()

class MethodInfo:
    def __init__(self, rt, name, params, ic):
        self.rt, self.name, self.params, self.ic = rt, name, params, ic

def parse_header(file_path):
    if not os.path.exists(file_path): return None, []
    with open(file_path, 'r', encoding='utf-8') as f: content = f.read()
    cm = re.search(r'class\s+(?:ENGINE_DLL\s+)?(\w+)\s*(?::\s*[\w\s,:]+)?\s*\{', content)
    if not cm: return None, []
    cn = cm.group(1)
    st, pb, pu, d = content.find(cm.group(0)), "", False, 0
    for l in content[st:].split('\n'):
        if '{' in l: d += l.count('{')
        if '}' in l: 
            d -= l.count('}')
            if d <= 0: break
        if 'public:' in l: pu = True
        elif 'protected:' in l or 'private:' in l: pu = False
        if pu and d == 1: pb += l + "\n"
    pb = re.sub(r'//.*', '', pb); pb = re.sub(r'/\*.*?\*/', '', pb, flags=re.DOTALL)
    methods = []
    for dec in pb.split(';'):
        dec = dec.strip()
        if not dec or '{' in dec or '}' in dec: continue
        m = re.match(r'(?:virtual\s+)?([\w:<>*& ]+)\s+(\w+)\s*\((.*?)\)\s*(const)?\s*(?:=\s*0)?$', dec, re.DOTALL)
        if not m: continue
        rt, name, ps_str, ic = m.group(1).strip(), m.group(2).strip(), m.group(3).strip(), m.group(4) is not None
        if name in [cn, f"~{cn}", "NO_COPY_NO_MOVE_NO_DEFAULT"] or "friend" in rt: continue
        params = []
        if ps_str:
            p_list, cur, dep = [], "", 0
            for c in ps_str:
                if c == '<': dep += 1
                elif c == '>': dep -= 1
                if c == ',' and dep == 0:
                    p_list.append(cur.strip())
                    cur = ""
                else: cur += c
            if cur: p_list.append(cur.strip())
            for p in p_list:
                pm = re.match(r'(.+?)\s+(\w+)(?:\[(\d+)\])?(?:\s*=\s*(.*))?$', p)
                if pm: params.append({"type": pm.group(1).strip(), "name": pm.group(2).strip(), "arr": pm.group(3)})
                else: params.append({"type": p, "name": f"p{len(params)}", "arr": None})
        methods.append(MethodInfo(rt, name, params, ic))
    return cn, methods

def generate_exports(cn, methods, prf, out_prf):
    h_f, c_f = os.path.join(EXPORTS_DIR, out_prf + "Exports.gen.h"), os.path.join(EXPORTS_DIR, out_prf + "Exports.gen.cpp")
    h_c = ["#pragma once", '#include "RHITypesExports.h"', '#include "EngineCommon.h"', "", 'extern "C" {']
    c_c = ['#include "' + out_prf + 'Exports.gen.h"', '#include "RHI/Commands/RHICommandBuffer.h"', '#include "RHI/Core/RHIDevice.h"', '#include "RHI/Core/RHIFactory.h"', '#include "RHI/Descriptors/RHIDescriptorUpdateInfo.h"', "using namespace ArisenEngine;", "", 'extern "C" {']
    for m in methods:
        c_ret = map_type(m.rt)
        if m.rt in OPAQUE_HANDLES: c_ret = OPAQUE_HANDLES[m.rt]
        c_name = f"RHI_{prf}_{m.name}"; c_params = [f"{OPAQUE_HANDLES[cn]} handle"] if cn in OPAQUE_HANDLES else []
        for p in m.params:
            rmt = map_type(p["type"])
            if "Vector" in p["type"]: rmt = rmt.replace("&&", "").replace("&", "").strip() + "*"
            c_params.append(f"{rmt} {p['name']}[{p['arr']}]" if p["arr"] else f"{rmt} {p['name']}")
        h_c.append(f'ENGINE_DLL {c_ret} {c_name}({", ".join(c_params)});')
        c_c.append(f'{c_ret} {c_name}({", ".join(c_params)})\n{{')
        if cn in OPAQUE_HANDLES:
            c_c.append(f'    auto* obj = reinterpret_cast<RHI::{cn}*>(handle);')
            c_c.append(f'    if (!obj) return{" 0" if c_ret != "void" else ""};')
            cp = []
            for p in m.params:
                pt, pn = p["type"], p["name"]
                if "Vector" in pt and "&&" in pt:
                    inr = pt[pt.find("<")+1:pt.rfind(">")].strip()
                    if "shared_ptr" in inr:
                        si = inr[inr.find("<")+1:inr.rfind(">")].strip()
                        itt = f"std::shared_ptr<{map_single_type(si)}>"
                    else: itt = map_single_type(inr)
                    c_c.append(f'    ArisenEngine::Containers::Vector<{itt}> local_{pn};')
                    c_c.append(f'    if ({pn}) local_{pn} = std::move(*{pn});')
                    cp.append(f"std::move(local_{pn})")
                elif "Vector" in pt: cp.append(f"*{pn}")
                elif "Handle" in pt:
                    clp = pt.replace("const ", "").replace("&", "").strip()
                    if clp in TYPE_MAP: cp.append(f"*reinterpret_cast<RHI::{clp}*>(&{pn})")
                    else: cp.append(pn)
                else: cp.append(pn)
            call = f'obj->{m.name}({", ".join(cp)})'
            if c_ret == "void": c_c.append(f'    {call};')
            else:
                c_c.append(f'    auto result = {call};')
                clr = m.rt.replace("const ", "").replace("*", "").replace("&", "").strip()
                if "Handle" in clr: c_c.append(f'    return *reinterpret_cast<{c_ret}*>(&result);')
                elif clr in OPAQUE_HANDLES: c_c.append(f'    return reinterpret_cast<{c_ret}>(result);')
                else: c_c.append('    return result;')
        c_c.append("}")
    h_c.append("}\n"); c_c.append("}\n")
    with open(h_f, 'w', encoding='utf-8') as f: f.write("\n".join(h_c))
    with open(c_f, 'w', encoding='utf-8') as f: f.write("\n".join(c_c))

def main():
    conf = [{"f": "Commands/RHICommandBuffer.h", "p": "Cmd", "o": "CommandBuffer"},
            {"f": "Core/RHIDevice.h", "p": "Device", "o": "Device"},
            {"f": "Core/RHIFactory.h", "p": "Factory", "o": "Factory"}]
    for c in conf:
        cn, ms = parse_header(os.path.join(CORE_RHI_DIR, c["f"]))
        if ms: generate_exports(cn, ms, c["p"], c["o"])

if __name__ == "__main__": main()
