import os
import json
from config_base import base_config
from tkinter import messagebox
from logger import NullLogger

class ConfigManager:
    def __init__(self, config_path=None, base_width=1260, base_height=2800, logger=None):
        if config_path is None:
            config_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "config.json")
        self.config_path = config_path
        self.base_width = base_width
        self.base_height = base_height
        self.config = None
        self.logger = logger if logger else NullLogger()

    def get_screen_size(self):
        result = os.popen('adb shell wm size').read()
        if "Physical size:" in result:
            size = result.strip().split(":")[-1].strip()
            self.logger.info(f"[分辨率] 设备屏幕分辨率: {size}")
            return size
        self.logger.warning("[分辨率] 设备屏幕分辨率: 未知")
        return "未知"

    def scale_config(self, config, size_str=None):
        if size_str is None:
            size_str = self.get_screen_size()
        try:
            current_width, current_height = map(int, size_str.split('x'))
        except Exception as e:
            self.logger.warning(f"无法获取屏幕分辨率，保持默认配置。异常: {e}")
            messagebox.showwarning("分辨率异常", f"无法获取屏幕分辨率，保持默认配置。异常: {e}")
            return config
        scale_x = current_width / self.base_width
        scale_y = current_height / self.base_height
        self.logger.info(f"[缩放比例] scale_x: {scale_x:.4f}, scale_y: {scale_y:.4f} (当前分辨率: {current_width}x{current_height}, 基准: {self.base_width}x{self.base_height})")
        def scale_dict(d):
            out = {}
            for k, v in d.items():
                if isinstance(v, tuple) and len(v) == 2:
                    out[k] = [v[0] * scale_x, v[1] * scale_y]
                elif isinstance(v, dict):
                    out[k] = scale_dict(v)
                else:
                    out[k] = v
            return out
        return scale_dict(config)

    def load(self):
        # 只在config.json不存在时生成，存在则直接加载
        if os.path.exists(self.config_path):
            try:
                with open(self.config_path, 'r', encoding='utf-8') as f:
                    self.config = json.load(f)
                self.logger.info("[配置] 已加载本地 config.json")
                return self.config
            except Exception as e:
                self.logger.warning(f"[配置] 加载 config.json 失败: {e}")
                # 失败时重新生成
        scaled = self.scale_config(base_config)
        with open(self.config_path, 'w', encoding='utf-8') as f:
            json.dump(scaled, f, ensure_ascii=False, indent=2)
        self.config = scaled
        self.logger.info("[配置] 自动生成 config.json")
        return self.config

    def save(self, config=None):
        if config is None:
            config = self.config
        with open(self.config_path, 'w', encoding='utf-8') as f:
            json.dump(config, f, ensure_ascii=False, indent=2)
        self.logger.info("[配置] 已保存 config.json")

    def get(self, section, key=None):
        if self.config is None:
            self.load()
        if key:
            return self.config.get(section, {}).get(key)
        return self.config.get(section)

    def reload(self):
        return self.load()
