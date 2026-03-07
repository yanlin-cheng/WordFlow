#!/usr/bin/env python3
"""
WordFlow Python ASR 服务端 - Sherpa-ONNX版本
轻量级、无需PyTorch，支持模型热切换
"""

import json
import logging
import base64
import struct
import os
import sys
from pathlib import Path
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import parse_qs, urlparse

import numpy as np

# 配置日志
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)


class SherpaASRService:
    """Sherpa-ONNX语音识别服务"""
    
    # 支持的模型配置（下载地址等）
    # 使用 ModelScope 国内镜像，下载更快
    AVAILABLE_MODELS = {
        "sensevoice-small-onnx": {
            "name": "SenseVoice 官方 ONNX",
            "size": "238MB",
            "description": "通义实验室官方 ONNX 版本，带标点，支持多语种",
            "files": {
                "model": "model.int8.onnx",
                "tokens": "tokens.txt"
            },
            "type": "sensevoice",
            "localOnly": True
        }
    }
    
    def __init__(self, models_dir: str = None):
        """
        初始化服务
        
        Args:
            models_dir: 模型存储目录，默认使用程序目录下的models文件夹
        """
        if models_dir is None:
            # 默认路径：程序目录/models
            self.models_dir = Path(__file__).parent / "models"
        else:
            self.models_dir = Path(models_dir)
        
        self.models_dir.mkdir(exist_ok=True)
        
        # 当前加载的模型
        self.current_model_id = None
        self.recognizer = None
        
        # 检查已安装的模型
        self.installed_models = self._scan_installed_models()
        
    def _scan_installed_models(self) -> list:
        """扫描已安装的模型"""
        installed = []
        for model_id in self.AVAILABLE_MODELS.keys():
            model_path = self.models_dir / model_id
            if model_path.exists():
                # 支持 model.onnx 或 model.int8.onnx
                if (model_path / "model.onnx").exists() or (model_path / "model.int8.onnx").exists():
                    installed.append(model_id)
        return installed
    
    def get_available_models(self) -> dict:
        """获取可用模型列表（包括安装状态）"""
        # 重新扫描，确保获取最新状态
        self.installed_models = self._scan_installed_models()
        
        result = {}
        for model_id, info in self.AVAILABLE_MODELS.items():
            result[model_id] = {
                **info,
                "installed": model_id in self.installed_models
            }
        return result
    
    def load_model(self, model_id: str) -> bool:
        """
        加载指定模型
        
        Args:
            model_id: 模型 ID
            
        Returns:
            是否加载成功
        """
        try:
            from sherpa_onnx import OfflineRecognizer
            
            if model_id not in self.AVAILABLE_MODELS:
                logger.error(f"未知模型：{model_id}")
                return False
            
            model_path = self.models_dir / model_id
            if not model_path.exists():
                logger.error(f"模型未安装：{model_id}")
                return False
            
            # 释放旧模型
            if self.recognizer:
                self.recognizer = None
            
            logger.info(f"正在加载模型：{model_id}")
            
            # 自动检测模型文件（优先使用 int8 量化版）
            model_file = model_path / "model.int8.onnx"
            if not model_file.exists():
                model_file = model_path / "model.onnx"
            
            if not model_file.exists():
                logger.error(f"模型文件不存在：{model_path}")
                return False
            
            logger.info(f"使用模型文件：{model_file.name}")
            
            # 获取模型类型
            model_info = self.AVAILABLE_MODELS.get(model_id, {})
            model_type = model_info.get("type", "paraformer")
            
            # 根据模型类型使用不同的加载方法
            if model_type == "sensevoice":
                logger.info(f"使用 SenseVoice 方式加载模型")
                
                # 使用 SenseVoice 专用工厂方法
                # use_itn=True: 启用逆文本正则化，自动输出带标点符号的文本
                self.recognizer = OfflineRecognizer.from_sense_voice(
                    model=str(model_file),
                    tokens=str(model_path / "tokens.txt"),
                    num_threads=4,
                    use_itn=True
                )
            else:
                logger.info(f"使用 Paraformer 方式加载模型")
                # 使用 Paraformer 专用工厂方法
                self.recognizer = OfflineRecognizer.from_paraformer(
                    paraformer=str(model_file),
                    tokens=str(model_path / "tokens.txt"),
                    num_threads=4
                )
            
            self.current_model_id = model_id
            
            logger.info(f"模型加载完成：{model_id}")
            logger.info(f" recognizer={self.recognizer is not None}, current_model_id={self.current_model_id}")
            return True
            
        except Exception as e:
            logger.error(f"加载模型失败：{e}")
            import traceback
            logger.error(traceback.format_exc())
            return False
    
    def recognize(self, audio_bytes: bytes, sample_rate: int = 16000) -> dict:
        """
        语音识别
        
        Args:
            audio_bytes: 音频字节数据（WAV或PCM）
            sample_rate: 采样率
            
        Returns:
            {"text": str}
        """
        if self.recognizer is None:
            return {"text": "", "error": "模型未加载"}
        
        try:
            # 解析音频
            audio_array = self._parse_audio(audio_bytes, sample_rate)
            
            # 创建识别流
            stream = self.recognizer.create_stream()
            stream.accept_waveform(sample_rate, audio_array)
            
            # 解码
            self.recognizer.decode_stream(stream)
            
            text = stream.result.text
            logger.info(f"识别结果: {text}")
            
            return {"text": text}
            
        except Exception as e:
            logger.error(f"识别失败: {e}")
            return {"text": "", "error": str(e)}
    
    def _parse_audio(self, audio_bytes: bytes, sample_rate: int) -> np.ndarray:
        """解析音频数据"""
        # 检查WAV格式
        if len(audio_bytes) > 44 and audio_bytes[:4] == b'RIFF' and audio_bytes[8:12] == b'WAVE':
            # 查找data chunk
            idx = audio_bytes.find(b'data')
            if idx != -1:
                data_size = struct.unpack('<I', audio_bytes[idx+4:idx+8])[0]
                pcm_data = audio_bytes[idx+8:idx+8+data_size]
                audio = np.frombuffer(pcm_data, dtype=np.int16).astype(np.float32) / 32768.0
                return audio
            else:
                audio = np.frombuffer(audio_bytes[44:], dtype=np.int16).astype(np.float32) / 32768.0
                return audio
        else:
            # 纯PCM
            audio = np.frombuffer(audio_bytes, dtype=np.int16).astype(np.float32) / 32768.0
            return audio


class ASRHandler(BaseHTTPRequestHandler):
    """HTTP请求处理器"""
    
    asr_service = None
    
    def log_message(self, format, *args):
        logger.info(f"{self.address_string()} - {format % args}")
    
    def do_GET(self):
        """处理GET请求"""
        parsed = urlparse(self.path)
        path = parsed.path
        
        if path == '/' or path == '/health':
            # 健康检查
            logger.info(f"[DEBUG] Health check - asr_service={self.asr_service}, id={id(self.asr_service) if self.asr_service else None}")
            if self.asr_service:
                logger.info(f"[DEBUG] current_model_id={self.asr_service.current_model_id}, installed={self.asr_service.installed_models}")
            status = {
                'status': 'ok',
                'service': 'WordFlow ASR (Sherpa-ONNX)',
                'current_model': self.asr_service.current_model_id if self.asr_service else None,
                'installed_models': self.asr_service.installed_models if self.asr_service else []
            }
            self._send_json(status)
            
        elif path == '/models':
            # 获取可用模型列表
            models = self.asr_service.get_available_models()
            self._send_json({'models': models})
            
        else:
            self._send_error(404, "Not Found")
    
    def do_POST(self):
        """处理POST请求"""
        parsed = urlparse(self.path)
        path = parsed.path
        
        try:
            content_length = int(self.headers['Content-Length'])
            post_data = self.rfile.read(content_length)
            data = json.loads(post_data.decode('utf-8'))
            
            if path == '/recognize':
                # 语音识别
                audio_b64 = data.get('audio')
                if not audio_b64:
                    self._send_error(400, "Missing audio data")
                    return
                
                audio_bytes = base64.b64decode(audio_b64)
                sample_rate = data.get('sample_rate', 16000)
                
                result = self.asr_service.recognize(audio_bytes, sample_rate)
                self._send_json({
                    'text': result.get('text', ''),
                    'success': 'error' not in result
                })
                
            elif path == '/load_model':
                # 切换模型
                model_id = data.get('model_id')
                logger.info(f"收到切换模型请求: model_id={model_id}")
                
                if not model_id:
                    logger.error("切换模型失败: 缺少 model_id")
                    self._send_error(400, "Missing model_id")
                    return
                
                logger.info(f"开始加载模型: {model_id}")
                success = self.asr_service.load_model(model_id)
                logger.info(f"模型加载结果: success={success}")
                
                self._send_json({
                    'success': success,
                    'model_id': model_id if success else None
                })
                
            else:
                self._send_error(404, "Not Found")
                
        except Exception as e:
            logger.error(f"处理请求失败: {e}", exc_info=True)
            self._send_error(500, str(e))
    
    def _send_json(self, data: dict):
        self.send_response(200)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Access-Control-Allow-Origin', '*')
        self.end_headers()
        self.wfile.write(json.dumps(data, ensure_ascii=False).encode('utf-8'))
    
    def _send_error(self, code: int, message: str):
        self.send_response(code)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Access-Control-Allow-Origin', '*')
        self.end_headers()
        self.wfile.write(json.dumps({'error': message, 'success': False}).encode('utf-8'))


def main():
    import argparse
    
    parser = argparse.ArgumentParser(description='WordFlow ASR Service')
    parser.add_argument('--models-dir', default='models', help='模型存储目录')
    parser.add_argument('--port', type=int, default=5000, help='服务端口')
    parser.add_argument('--model', default=None, help='默认加载的模型ID')
    
    args = parser.parse_args()
    
    logger.info("=" * 50)
    logger.info("WordFlow ASR Service (Sherpa-ONNX)")
    logger.info("=" * 50)
    
    # 初始化服务
    service = SherpaASRService(args.models_dir)
    ASRHandler.asr_service = service
    
    # 显示模型状态
    logger.info(f"模型目录: {service.models_dir}")
    logger.info(f"已安装模型: {service.installed_models}")
    
    # 如果有默认模型，尝试加载
    if args.model and args.model in service.installed_models:
        logger.info(f"启动时加载默认模型: {args.model}")
        success = service.load_model(args.model)
        logger.info(f"启动时加载结果: success={success}, current_model_id={service.current_model_id}")
    
    # 启动HTTP服务
    host = '127.0.0.1'
    port = args.port
    
    server = HTTPServer((host, port), ASRHandler)
    logger.info(f"服务启动: http://{host}:{port}")
    logger.info("API:")
    logger.info("  GET  /health      - 健康检查")
    logger.info("  GET  /models      - 获取可用模型")
    logger.info("  POST /recognize   - 语音识别")
    logger.info("  POST /load_model  - 切换模型")
    logger.info("按 Ctrl+C 停止服务")
    logger.info("=" * 50)
    
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        logger.info("停止服务")


if __name__ == '__main__':
    main()
