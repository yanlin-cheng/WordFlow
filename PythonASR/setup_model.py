#!/usr/bin/env python3
"""
模型下载辅助工具
由于自动下载经常失败，提供手动下载指引
"""

import os
import sys
from pathlib import Path

MODELS = {
    "paraformer-zh": {
        "name": "Paraformer-中文",
        "size": "200MB",
        "desc": "中文语音识别，准确率最高，推荐",
        # 多个备选下载源
        "urls": [
            "https://hf-mirror.com/pkufool/sherpa-onnx-paraformer-zh-2024-04-25/resolve/main/sherpa-onnx-paraformer-zh-2024-04-25.tar.bz2",
            "https://www.modelscope.cn/models/pkufool/sherpa-onnx-paraformer-zh-2024-04-25/resolve/master/sherpa-onnx-paraformer-zh-2024-04-25.tar.bz2",
        ]
    },
    "sensevoice-small": {
        "name": "SenseVoice-Small", 
        "size": "100MB",
        "desc": "多语种小模型，支持中英日韩粤",
        "urls": [
            "https://hf-mirror.com/pkufool/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17/resolve/main/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17.tar.bz2",
        ]
    }
}

def print_help():
    """打印帮助信息"""
    print("="*60)
    print("WordFlow 模型下载指引")
    print("="*60)
    print()
    print("由于网络原因，自动下载经常失败。请手动下载：")
    print()
    
    for model_id, info in MODELS.items():
        print(f"【{info['name']}】- {info['size']}")
        print(f"  说明: {info['desc']}")
        print(f"  下载地址:")
        for i, url in enumerate(info['urls'], 1):
            print(f"    {i}. {url}")
        print()
    
    print("="*60)
    print("下载步骤:")
    print("1. 复制上面的下载链接，用浏览器或迅雷下载")
    print("2. 将下载的 .tar.bz2 文件放到 PythonASR/models/ 目录")
    print("3. 解压文件（可用7-Zip或Bandizip）")
    print("4. 解压后的文件夹重命名为模型ID（如 paraformer-zh）")
    print()
    print("目录结构示例:")
    print("  PythonASR/models/")
    print("  └── paraformer-zh/")
    print("      ├── model.onnx")
    print("      └── tokens.txt")
    print("="*60)

def check_and_extract():
    """检查并解压已下载的模型文件"""
    models_dir = Path(__file__).parent / "models"
    models_dir.mkdir(exist_ok=True)
    
    # 查找下载的压缩包
    archives = list(models_dir.glob("*.tar.bz2")) + list(models_dir.glob("*.zip"))
    
    if not archives:
        print("未找到模型压缩包")
        return False
    
    import tarfile
    
    for archive in archives:
        print(f"\n发现压缩包: {archive.name}")
        
        # 尝试识别模型类型
        model_id = None
        for mid in MODELS.keys():
            if mid.replace("-", "_") in archive.name or mid in archive.name:
                model_id = mid
                break
        
        if not model_id:
            print(f"  无法识别模型类型，跳过")
            continue
        
        model_dir = models_dir / model_id
        if model_dir.exists():
            print(f"  模型目录已存在: {model_dir}")
            continue
        
        # 解压
        print(f"  正在解压到: {model_dir}")
        try:
            if archive.name.endswith('.tar.bz2'):
                with tarfile.open(archive, 'r:bz2') as tar:
                    # 解压到临时目录
                    temp_dir = models_dir / f"_temp_{model_id}"
                    tar.extractall(temp_dir)
                    
                    # 查找解压后的目录
                    subdirs = [d for d in temp_dir.iterdir() if d.is_dir()]
                    if subdirs:
                        subdirs[0].rename(model_dir)
                    temp_dir.rmdir()
                    
                print(f"  ✓ 解压完成: {model_id}")
                # 删除压缩包
                archive.unlink()
        except Exception as e:
            print(f"  ✗ 解压失败: {e}")
    
    return True

if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "--extract":
        check_and_extract()
    else:
        print_help()
    
    input("\n按回车键退出...")
