#!/usr/bin/env python3
"""快速测试FunASR服务"""

import requests
import base64
import json
from pathlib import Path

def test_with_file(wav_path: str):
    """使用音频文件测试"""
    print(f"\n🎵 测试文件: {Path(wav_path).name}")
    
    # 读取WAV文件
    with open(wav_path, 'rb') as f:
        audio_bytes = f.read()
    
    print(f"   文件大小: {len(audio_bytes)} bytes")
    
    # Base64编码
    audio_b64 = base64.b64encode(audio_bytes).decode('utf-8')
    
    # 发送请求
    print(f"   发送识别请求...")
    try:
        response = requests.post(
            'http://127.0.0.1:5000/recognize',
            json={'audio': audio_b64, 'sample_rate': 16000},
            timeout=30
        )
        
        if response.status_code == 200:
            result = response.json()
            if result.get('success'):
                print(f"\n✅ 识别成功!")
                print(f"📝 结果: {result['text']}")
            else:
                print(f"❌ 识别失败: {result.get('error')}")
        else:
            print(f"❌ HTTP错误: {response.status_code}")
            print(response.text)
    
    except Exception as e:
        print(f"❌ 请求失败: {e}")


def main():
    print("=" * 50)
    print("WordFlow FunASR 测试")
    print("=" * 50)
    
    # 检查服务状态
    print("\n🔍 检查服务状态...")
    try:
        response = requests.get('http://127.0.0.1:5000/health', timeout=5)
        if response.status_code == 200:
            status = response.json()
            print(f"✅ 服务运行中: {status.get('service', 'Unknown')}")
        else:
            print(f"❌ 服务异常: {response.status_code}")
    except Exception as e:
        print(f"❌ 无法连接服务: {e}")
        print("   请先运行 start_server.bat 启动服务")
        return
    
    # 查找测试音频
    model_dir = Path(r"H:\WordFlow\Models\paraformer-onnx")
    example_dir = model_dir / "example"
    
    if example_dir.exists():
        wav_files = list(example_dir.glob("*.wav"))
        if wav_files:
            for wav_file in wav_files[:3]:  # 测试前3个
                test_with_file(str(wav_file))
        else:
            print("\n⚠️ 未找到测试音频")
    else:
        print("\n⚠️ 未找到example目录")
    
    print("\n" + "=" * 50)


if __name__ == '__main__':
    main()
    input("\n按回车键退出...")
