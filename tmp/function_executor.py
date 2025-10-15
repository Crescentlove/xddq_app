import os
import time
from tkinter import messagebox
from func_steps import func_steps

class TapFailed(Exception):
    pass

class FunctionExecutor:
    def __init__(self, config_mgr, logger=None):
        self.config_mgr = config_mgr
        from logger import NullLogger
        self.logger = logger if logger else NullLogger()

    def adb_tap(self, x, y):
        try:
            cmd = f'adb shell input tap {x} {y}'
            result = os.system(cmd)
            if result != 0:
                self.logger.error("ADB命令执行失败，请检查ADB连接和环境变量。")
                messagebox.showerror("错误", "ADB命令执行失败，请检查ADB连接和环境变量。")
                return False
            self.logger.info(f"点击坐标: ({x}, {y})")
            return True
        except Exception as e:
            self.logger.error(f"adb_tap异常: {e}")
            messagebox.showerror("异常", f"adb_tap异常: {e}")
            return False

    def tap_guard(self, func):
        def wrapper(*args, **kwargs):
            try:
                return func(*args, **kwargs)
            except TapFailed:
                return
        return wrapper

    def run_func(self, func_name, gui=None, finish_callback=None):
        try:
            self.logger.info(f"开始执行功能：{func_name}")
            steps = func_steps.get(func_name)
            if not steps:
                self.logger.warning(f"功能步骤未配置：{func_name}")
                messagebox.showwarning("未配置", f"功能步骤未配置：{func_name}")
                if finish_callback:
                    finish_callback()
                return

            def run_steps(idx=0):
                if idx >= len(steps):
                    # 所有步骤执行完毕，清空status_bar
                    if gui and hasattr(gui, 'status_bar'):
                        gui.status_bar.config(text="")
                    if finish_callback:
                        finish_callback()
                    return
                step = steps[idx]
                section = step.get('section')
                key = step.get('key')
                sleep_time = step.get('sleep', 1)
                pos = None
                if section and key:
                    pos = self.config_mgr.get(section, key)
                elif section:
                    pos = self.config_mgr.get(section)
                if not (isinstance(pos, list) and len(pos) == 2):
                    self.logger.warning(f"步骤坐标未配置或格式错误：{section} - {key}")
                    messagebox.showwarning("未配置", f"步骤坐标未配置或格式错误：{section} - {key}")
                    # 立即清空status_bar
                    if gui and hasattr(gui, 'status_bar'):
                        gui.status_bar.config(text="")
                    if finish_callback:
                        finish_callback()
                    return
                self.adb_tap(*pos)
                # 用after代替sleep，避免阻塞主线程
                if gui and hasattr(gui, 'root'):
                    job = gui.root.after(int(sleep_time * 1000), lambda: run_steps(idx + 1))
                    if hasattr(gui, 'func_jobs'):
                        gui.func_jobs.append(job)
                else:
                    import time
                    time.sleep(sleep_time)
                    run_steps(idx + 1)
            run_steps()
        except Exception as e:
            self.logger.error(f"run_func异常: {e}")
            messagebox.showerror("异常", f"run_func异常: {e}")
            if finish_callback:
                finish_callback()
