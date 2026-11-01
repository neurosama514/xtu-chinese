import os
import os
WORK = os.environ.get("XTU_WORK") or os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "work"))
base = WORK
apply_dir = os.path.join(base, "apply")

def esc(s):
    return s.replace("\\", "\\\\").replace("\t", "\\t").replace("\r", "\\r").replace("\n", "\\n")

core = {
    "CPU Stress Test": "CPU 压力测试",
    "Places the system's processor under load to test for stability.": "让系统处理器处于负载状态以测试稳定性。",
    "CPU Stress Test with AVX": "CPU 压力测试（AVX）",
    "Places the system's processor under load to test for stability utilizing up to AVX instructions.": "使用最高 AVX 指令让系统处理器处于负载状态以测试稳定性。",
    "CPU Stress Test with AVX2": "CPU 压力测试（AVX2）",
    "Places the system's processor under load to test for stability utilizing up to AVX2 instructions.": "使用最高 AVX2 指令让系统处理器处于负载状态以测试稳定性。",
    "CPU Stress Test with AVX-512": "CPU 压力测试（AVX-512）",
    "Places the system's processor under load to test for stability utilizing up to AVX-512 instructions.": "使用最高 AVX-512 指令让系统处理器处于负载状态以测试稳定性。",
    "Memory Stress Test": "内存压力测试",
    "Places the system's memory under load to test for stability.": "让系统内存处于负载状态以测试稳定性。",
    "CPU Core Temperature: {0}": "CPU 核心温度：{0}",
    "Run Stress Test with {0} Instructions": "使用 {0} 指令运行压力测试",
    "Stress test executable could not be located at: ": "找不到压力测试可执行文件，路径：",
    " Stress test executable could not be located.": " 找不到压力测试可执行文件。",
    " Failed to find the memory stress test binary.": " 未找到内存压力测试程序。",
    "---------------------------Stress test completed.---------------------------": "---------------------------压力测试已完成。---------------------------",
    "---------------------------Stress test canceled.---------------------------": "---------------------------压力测试已取消。---------------------------",
    "---------------------------Stress test failed.---------------------------": "---------------------------压力测试失败。---------------------------",
    "--------------------------Stress test being killed.---------------------------": "--------------------------正在终止压力测试。---------------------------",
    "Creating input data file for linpack test.": "正在为 linpack 测试创建输入数据文件。",
}

common = {
    "Intel XTU Profiles": "Intel XTU 配置文件",
    "Extreme Tuning Utility Profile|*.xtu": "Extreme Tuning Utility 配置文件|*.xtu",
    "Export XTU Profile": "导出 XTU 配置文件",
    "Import XTU Profile": "导入 XTU 配置文件",
    "Failure while attempting to pick files": "选择文件时失败",
    "Yes": "是",
    "No": "否",
}

extra = {
    "Benchmark": "基准分数",
    " Day": " 天",
    " Days": " 天",
    " Hour": " 小时",
    " Hours": " 小时",
    " Minutes": " 分钟",
    "2 Minutes": "2 分钟",
    "3 Minutes": "3 分钟",
    "5 Minutes": "5 分钟",
    "10 Minutes": "10 分钟",
    "30 Minutes": "30 分钟",
    "1 Hour": "1 小时",
}

for name, d in (("strmap_coreclient.tsv", core), ("strmap_ptcommon.tsv", common)):
    with open(os.path.join(apply_dir, name), "w", encoding="utf-8", newline="\n") as f:
        for k, v in d.items():
            f.write(esc(k) + "\t" + esc(v) + "\n")
    print(name, len(d))

with open(os.path.join(apply_dir, "strmap_perftune.tsv"), "a", encoding="utf-8", newline="\n") as f:
    for k, v in extra.items():
        f.write(esc(k) + "\t" + esc(v) + "\n")
print("extra perftune:", len(extra))
