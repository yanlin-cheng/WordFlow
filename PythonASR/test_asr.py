#!/usr/bin/env python3
"""
测试ASR功能
"""

import sys
import numpy as np
from pathlib import Path

# 添加父目录到路径
sys.path.insert(0, str(Path(__file__).parent))

from asr_server import ParaformerASR


def test_with_wav(wav_path: str, model_dir: str):
    """测试WAV文件识别"""
    import wave
    
    # 读取WAV文件
    with wave.open(wav_path, 'rb') as wf:
        n_channels = wf.getnchannels()
        sample_rate = wf.getframerate()
        n_frames = wf.getnframes()
        
        audio_data = wf.readframes(n_frames)
        audio = np.frombuffer(audio_data, dtype=np.int16)
        
        # 转单声道
        if n_channels > 1:
            audio = audio.reshape(-1, n_channels).mean(axis=1)
        
        # 转float32
        audio = audio.astype(np.float32) / 32768.0
    
    print(f"音频信息: {len(audio)} samples, {sample_rate}Hz, {n_channels} channels")
    
    # 初始化ASR
    print(f"加载模型: {model_dir}")
    asr = ParaformerASR(model_dir)
    
    # 识别
    print("开始识别...")
    text = asr.recognize(audio, sample_rate)
    
    print(f"识别结果: {text}")
    return text


def test_with_synthetic():
    """用合成数据测试"""
    # 创建2秒的16kHz音频（模拟）
    sample_rate = 16000
    duration = 2
    t = np.linspace(0, duration, sample_rate * duration)
    
    # 生成一个简单的正弦波（模拟语音）
    audio = np.sin(2 * np.pi * 440 * t) * 0.3
    audio += np.sin(2 * np.pi * 880 * t) * 0.2
    
    audio = audio.astype(np.float32)
    
    print(f"合成音频: {len(audio)} samples, {sample_rate}Hz")
    
    # 初始化ASR
    model_dir = r"H:\WordFlow\Models\paraformer-onnx"
    print(f"加载模型: {model_dir}")
    asr = ParaformerASR(model_dir)
    
    # 识别
    print("开始识别...")
    text = asr.recognize(audio, sample_rate)
    
    print(f"识别结果: {text}")
    return text


if __name__ == '__main__':
    import argparse
    
    parser = argparse.ArgumentParser(description='测试ASR功能')
    parser.add_argument('--wav', help='WAV文件路径')
    parser.add_argument('--model', default=r"H:\WordFlow\Models\paraformer-onnx",
                       help='模型目录')
    parser.add_argument('--synthetic', action='store_true',
                       help='使用合成数据测试')
    
    args = parser.parse_args()
    
    if args.wav:
        test_with_wav(args.wav, args.model)
    elif args.synthetic:
        test_with_synthetic()
    else:
        # 使用example目录下的测试音频
        example_dir = Path(args.model) / "example"
        if example_dir.exists():
            wav_files = list(example_dir.glob("*.wav"))
            if wav_files:
                print(f"找到测试音频: {wav_files[0]}")
                test_with_wav(str(wav_files[0]), args.model)
            else:
                print("未找到WAV文件，使用合成数据测试")
                test_with_synthetic()
        else:
            print("未找到example目录，使用合成数据测试")
            test_with_synthetic()
