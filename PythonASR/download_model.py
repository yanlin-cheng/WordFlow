#!/usr/bin/env python3
"""
模型下载工具 - 改进版本
支持从 Gitee 下载分卷模型文件，也支持从 ModelScope 下载
"""

import os
import sys
import tarfile
import urllib.request
import urllib.error
from pathlib import Path


def download_file(url: str, dest: Path, desc: str = "") -> bool:
    """下载文件并显示进度"""
    print(f"下载：{desc or url}")
    
    try:
        def progress_hook(block_num, block_size, total_size):
            downloaded = block_num * block_size
            percent = min(downloaded * 100 / total_size, 100) if total_size > 0 else 0
            size_mb = downloaded / 1024 / 1024
            total_mb = total_size / 1024 / 1024 if total_size > 0 else 0
            print(f"\r  进度：{percent:.1f}% ({size_mb:.1f}/{total_mb:.1f} MB)", end="", flush=True)
        
        # 设置 User-Agent，避免被阻止
        opener = urllib.request.build_opener()
        opener.addheaders = [('User-Agent', 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36')]
        urllib.request.install_opener(opener)
        
        urllib.request.urlretrieve(url, dest, progress_hook)
        print()  # 换行
        return True
    except Exception as e:
        print(f"\n下载错误：{e}")
        return False


def check_model_installed(model_path: Path) -> bool:
    """检查模型是否已安装"""
    if not model_path.exists():
        return False
    # 检查是否有有效的模型文件
    has_model = (model_path / "model.onnx").exists() or (model_path / "model.int8.onnx").exists()
    has_tokens = (model_path / "tokens.txt").exists()
    return has_model and has_tokens


def download_model(model_id: str, models_dir: Path = None) -> bool:
    """
    下载指定模型
    
    Args:
        model_id: 模型 ID
        models_dir: 模型存储目录
    """
    from asr_server import SherpaASRService
    
    if models_dir is None:
        models_dir = Path(__file__).parent / "models"
    
    models_dir.mkdir(exist_ok=True)
    
    # 获取模型信息
    if model_id not in SherpaASRService.AVAILABLE_MODELS:
        print(f"错误：未知模型 {model_id}")
        print(f"可用模型：{', '.join(SherpaASRService.AVAILABLE_MODELS.keys())}")
        return False
    
    model_info = SherpaASRService.AVAILABLE_MODELS[model_id]
    print(f"模型：{model_info['name']}")
    print(f"大小：{model_info['size']}")
    print(f"说明：{model_info['description']}")
    print()
    
    # 检查是否已安装
    model_path = models_dir / model_id
    if check_model_installed(model_path):
        print(f"✅ 模型已安装：{model_path}")
        print("   跳过下载")
        return True
    
    # 下载
    url = model_info['url']
    archive_name = Path(url).name
    download_path = models_dir / archive_name
    
    try:
        print(f"下载地址：{url}")
        if not download_file(url, download_path, archive_name):
            print("下载失败")
            return False
        
        # 解压
        print(f"解压 {archive_name}...")
        if archive_name.endswith('.tar.bz2'):
            with tarfile.open(download_path, 'r:bz2') as tar:
                tar.extractall(models_dir)
        
        # 查找解压后的目录（支持多种命名格式）
        extracted_dir = None
        possible_names = [
            model_id,
            model_id.replace('-', '_'),
            f"sherpa-{model_id}",
            f"sherpa-{model_id.replace('-', '_')}"
        ]
        
        for name in possible_names:
            for d in models_dir.iterdir():
                if d.is_dir() and name in d.name:
                    extracted_dir = d
                    break
            if extracted_dir:
                break
        
        if extracted_dir:
            # 重命名为标准名称
            if extracted_dir != model_path:
                print(f"重命名：{extracted_dir.name} -> {model_path.name}")
                extracted_dir.rename(model_path)
        else:
            print("警告：未找到解压后的模型目录")
            # 检查是否已经存在目标目录
            if not check_model_installed(model_path):
                print("错误：模型文件未找到")
                return False
        
        # 删除压缩包
        if download_path.exists():
            download_path.unlink()
        
        # 验证
        if check_model_installed(model_path):
            print(f"\n✅ 模型下载安装成功：{model_path}")
            return True
        else:
            print(f"\n⚠️ 模型文件不完整，请检查：{model_path}")
            return False
        
    except Exception as e:
        print(f"\n❌ 下载失败：{e}")
        import traceback
        traceback.print_exc()
        return False


def main():
    import argparse
    
    parser = argparse.ArgumentParser(description='下载 ASR 模型')
    parser.add_argument('model_id', help='模型 ID')
    parser.add_argument('--models-dir', default='models', help='模型存储目录')
    
    args = parser.parse_args()
    
    # 确保路径是绝对路径
    models_dir = Path(args.models_dir)
    if not models_dir.is_absolute():
        models_dir = Path(__file__).parent / models_dir
    
    print(f"模型目录：{models_dir}")
    
    success = download_model(args.model_id, models_dir)
    
    if success:
        print("\n✓ 完成")
        sys.exit(0)
    else:
        print("\n✗ 失败")
        sys.exit(1)


if __name__ == '__main__':
    main()
