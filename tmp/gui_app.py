import tkinter as tk
import webbrowser
from logger import NullLogger

class GUIApp:
    def __init__(self, config_mgr, func_exec, logger=None):
        self.config_mgr = config_mgr
        self.func_exec = func_exec
        self.root = None
        self.status_bar = None
        self.screen_size = None
        self.logger = logger if logger else NullLogger()
        self.func_jobs = []  # 记录所有 after 句柄

    def stop_all_jobs(self):
        for job in self.func_jobs:
            try:
                self.root.after_cancel(job)
            except Exception:
                pass
        self.func_jobs.clear()
        self.status_bar.config(text="已停止所有执行")

    def create_title_section(self):
        title_frame = tk.Frame(self.root)
        title_frame.grid(row=0, column=0, columnspan=3, pady=(0, 8), sticky="ew")
        title_container = tk.Frame(title_frame)
        title_container.pack(side=tk.LEFT)
        subtitle_label = tk.Label(title_container, text=f"屏幕分辨率: {self.screen_size}", font=("微软雅黑", 9), fg="#666666")
        subtitle_label.pack(anchor="w")
        github_link = tk.Label(title_frame, text="黑猪 | GitHub | TEL:17602605570", font=("微软雅黑", 8), fg="blue", cursor="hand2")
        github_link.pack(side=tk.RIGHT, padx=6)
        github_link.bind("<Button-1>", lambda e: webbrowser.open("https://github.com/Crescentlove"))

    def create_status_bar(self):
        self.status_bar = tk.Label(self.root, text="", bd=1, relief=tk.SUNKEN, anchor=tk.W)
        self.status_bar.grid(row=5, column=0, columnspan=3, sticky="ew")

    def create_section(self, frame, button_configs):
        """
        创建功能区块的所有按钮和批量操作控件。
        - 每个按钮自动生成，支持批量执行和全选。
        - 自动领桃子按钮后插入全局停止按钮。
        """
        check_vars = {}  # 记录每个按钮的选中状态
        simple_cmds = {}  # 记录每个按钮的执行函数

        # 批量自动执行选中的功能
        def auto_execute():
            tasks = [text for text, var in check_vars.items() if var.get() == 1 and text in simple_cmds]
            def run_next(idx=0):
                if idx >= len(tasks):
                    self.status_bar.config(text="")
                    return
                def on_finish():
                    run_next(idx+1)
                # 修改按钮命令，传入回调
                simple_cmds[tasks[idx]](on_finish)
            run_next()

        # 全选/取消全选逻辑
        select_all_state = {'selected': False}
        def toggle_select_all():
            select_all_state['selected'] = not select_all_state['selected']
            for var in check_vars.values():
                var.set(1 if select_all_state['selected'] else 0)
            select_all_btn.config(text="取消全选" if select_all_state['selected'] else "全选")

        # 顶部批量操作区块
        top_btn_frame = tk.Frame(frame)
        top_btn_frame.pack(fill=tk.X, pady=2)
        tk.Button(top_btn_frame, text="自动执行", command=auto_execute, width=10, height=1, bg="lightblue").pack(side=tk.LEFT, padx=(0, 5))
        select_all_btn = tk.Button(top_btn_frame, text="全选", width=8, command=toggle_select_all)
        select_all_btn.pack(side=tk.LEFT)
        tk.Frame(frame, height=1, bg="gray").pack(fill=tk.X, pady=2)

        # 生成所有功能按钮
        for btn_config in button_configs:
            row_frame = tk.Frame(frame)
            row_frame.pack(fill=tk.X, pady=2)
            var = tk.IntVar()
            check_vars[btn_config['text']] = var
            tk.Checkbutton(row_frame, variable=var).pack(side=tk.LEFT, padx=(0, 5))
            # 修复闭包问题，调试打印
            def make_cmd(btn, cmd, name):
                def inner(on_finish=None):
                    self.status_bar.config(text=f"正在执行：{name}")
                    def do_action():
                        # 支持回调
                        if on_finish:
                            cmd(finish_callback=on_finish)
                        else:
                            cmd()
                    frame.after(120, do_action)
                return inner
            btn = tk.Button(row_frame, text=btn_config['text'], width=10, height=1)
            simple_cmd = make_cmd(btn, btn_config['command'], btn_config['text'])
            btn.config(command=(lambda sc=simple_cmd: sc()))
            btn.pack(side=tk.LEFT, fill=tk.X, expand=True)
            simple_cmds[btn_config['text']] = simple_cmd

        # “其他功能”区块最后插入全局停止按钮（自动领桃子存在时）
        if any(b['text']=='自动领桃子' for b in button_configs):
            stop_all_btn = tk.Button(frame, text="全局停止", width=14, height=1, command=self.stop_all_jobs, bg="#FF6666")
            stop_all_btn.pack(pady=(2, 0), fill=tk.X)

    def create_main_frames(self, sections):
        frames = []
        for idx, sec in enumerate(sections):
            frame = tk.LabelFrame(self.root, text=sec["title"], font=("微软雅黑", 9))
            row, col = divmod(idx, 3)
            frame.grid(row=row+1, column=col, padx=3, pady=3, sticky="nsew")
            self.create_section(frame, sec["buttons"])
            frames.append(frame)
        return frames

    def launch(self):
        self.root = tk.Tk()
        self.screen_size = self.config_mgr.get_screen_size()
        self.root.title("寻道大千辅助工具")
        self.root.geometry("485x700")
        self.root.resizable(True, True)
        self.create_status_bar()
        self.create_title_section()
        # 配置区块
        sections = [
            {"title": "日常任务", "buttons": [
                {'text': '一键砍树', 'command': lambda finish_callback=None: self.func_exec.run_func('砍树功能', gui=self, finish_callback=finish_callback)},
                {'text': '超值礼包', 'command': lambda finish_callback=None: self.func_exec.run_func('超值礼包功能', gui=self, finish_callback=finish_callback)},
                {'text': '仙缘', 'command': lambda finish_callback=None: self.func_exec.run_func('仙缘功能', gui=self, finish_callback=finish_callback)},
                {'text': '邮件领取', 'command': lambda finish_callback=None: self.func_exec.run_func('邮件领取功能', gui=self, finish_callback=finish_callback)},
                {'text': '轮回殿', 'command': lambda finish_callback=None: self.func_exec.run_func('轮回殿功能', gui=self, finish_callback=finish_callback)},
                {'text': '座驾注灵', 'command': lambda finish_callback=None: self.func_exec.run_func('座驾注灵功能', gui=self, finish_callback=finish_callback)},
                {'text': '道友', 'command': lambda finish_callback=None: self.func_exec.run_func('道友功能', gui=self, finish_callback=finish_callback)},
                {'text': '仙树等级', 'command': lambda finish_callback=None: self.func_exec.run_func('等级加速功能', gui=self, finish_callback=finish_callback)},
            ]},
            {"title": "挑战副本", "buttons": [
                {'text': '斗法挑战', 'command': lambda finish_callback=None: self.func_exec.run_func('斗法挑战功能', gui=self, finish_callback=finish_callback)},
                {'text': '妖王挑战', 'command': lambda finish_callback=None: self.func_exec.run_func('妖王挑战功能', gui=self, finish_callback=finish_callback)},
                {'text': '异兽挑战', 'command': lambda finish_callback=None: self.func_exec.run_func('异兽挑战功能', gui=self, finish_callback=finish_callback)},
                {'text': '镇妖塔挑战', 'command': lambda finish_callback=None: self.func_exec.run_func('镇妖塔挑战功能', gui=self, finish_callback=finish_callback)},
                {'text': '星辰挑战', 'command': lambda finish_callback=None: self.func_exec.run_func('星辰挑战功能', gui=self, finish_callback=finish_callback)},
                {'text': '诸天挑战', 'command': lambda finish_callback=None: self.func_exec.run_func('诸天挑战功能', gui=self, finish_callback=finish_callback)},
                {'text': '法则挑战', 'command': lambda finish_callback=None: self.func_exec.run_func('法则挑战功能', gui=self, finish_callback=finish_callback)},
                {'text': '元辰试炼', 'command': lambda finish_callback=None: self.func_exec.run_func('元辰试炼功能', gui=self, finish_callback=finish_callback)},
            ]},
            {"title": "资源收集", "buttons": [
                {'text': '道途试炼', 'command': lambda finish_callback=None: self.func_exec.run_func('道途试炼功能', gui=self, finish_callback=finish_callback)},
                {'text': '宗门任务', 'command': lambda finish_callback=None: self.func_exec.run_func('宗门任务功能', gui=self, finish_callback=finish_callback)},
                {'text': '仙友游历', 'command': lambda finish_callback=None: self.func_exec.run_func('仙友游历功能', gui=self, finish_callback=finish_callback)},
                {'text': '仙宫点赞', 'command': lambda finish_callback=None: self.func_exec.run_func('仙宫点赞功能', gui=self, finish_callback=finish_callback)}
            ]},
            {"title": "功能系统", "buttons": [
                {'text': '内丹凝聚', 'command': lambda finish_callback=None: self.func_exec.run_func('灵兽内丹功能', gui=self, finish_callback=finish_callback)},
                {'text': '神通领取', 'command': lambda finish_callback=None: self.func_exec.run_func('神通领取功能', gui=self, finish_callback=finish_callback)},
                {'text': '法宝寻宝', 'command': lambda finish_callback=None: self.func_exec.run_func('法宝寻宝功能', gui=self, finish_callback=finish_callback)},
                {'text': '法象功能', 'command': lambda finish_callback=None: self.func_exec.run_func('法象功能', gui=self, finish_callback=finish_callback)},
                {'text': '玄诀修炼', 'command': lambda finish_callback=None: self.func_exec.run_func('玄诀修炼功能', gui=self, finish_callback=finish_callback)},
                {'text': '神躯修炼', 'command': lambda finish_callback=None: self.func_exec.run_func('神躯修炼功能', gui=self, finish_callback=finish_callback)}
            ]},
            {"title": "妖盟专区", "buttons": [
                {'text': '妖盟悬赏', 'command': lambda finish_callback=None: self.func_exec.run_func('妖盟悬赏功能', gui=self, finish_callback=finish_callback)},
                {'text': '妖邪挑战', 'command': lambda finish_callback=None: self.func_exec.run_func('妖邪挑战功能', gui=self, finish_callback=finish_callback)},
                {'text': '砍价任务', 'command': lambda finish_callback=None: self.func_exec.run_func('妖盟砍价功能', gui=self, finish_callback=finish_callback)},
                {'text': '妖盟商店', 'command': lambda finish_callback=None: self.func_exec.run_func('妖盟商店功能', gui=self, finish_callback=finish_callback)},
                {'text': '妖盟排行', 'command': lambda finish_callback=None: self.func_exec.run_func('妖盟排行功能', gui=self, finish_callback=finish_callback)}
            ]},
            {"title": "其他功能", "buttons": [
                {'text': '自动领桃子', 'command': lambda finish_callback=None: self.func_exec.run_func('自动领桃子功能', gui=self, finish_callback=finish_callback)}
            ]}
        ]
        self.create_main_frames(sections)
        self.root.mainloop()
