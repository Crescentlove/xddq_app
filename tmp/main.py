from config_manager import ConfigManager
from function_executor import FunctionExecutor
from gui_app import GUIApp
from logger import Logger
import os
import time
import sys

# 是否记录日志到文件，可在此切换
if getattr(sys, 'frozen', False):
    base_dir = os.path.dirname(sys.executable)
else:
    base_dir = os.path.dirname(os.path.abspath(__file__))

log_to_file = True
log_file_path = os.path.join(base_dir, "app.log")

def main():
    os.system('adb start-server')
    logger = Logger(log_file_path if log_to_file else None)
    logger.info("程序启动，ADB server已启动")
    config_mgr = ConfigManager(os.path.join(base_dir, "config.json"), logger=logger)
    config_mgr.load()
    func_exec = FunctionExecutor(config_mgr, logger=logger)
    app = GUIApp(config_mgr, func_exec, logger=logger)
    app.launch()

if __name__ == "__main__":
    main()
