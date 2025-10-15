import datetime

class NullLogger:
    def info(self, msg): pass
    def warning(self, msg): pass
    def error(self, msg): pass
    def debug(self, msg): pass

class Logger:
    def __init__(self, log_file=None):
        self.log_file = log_file
        self.console = False  # 控制是否输出到控制台

    def _write(self, level, msg):
        timestamp = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')
        log_msg = f"[{timestamp}] [{level}] {msg}"
        if self.console:
            try:
                print(log_msg)
            except Exception:
                pass
        if self.log_file:
            with open(self.log_file, 'a', encoding='utf-8') as f:
                f.write(log_msg + '\n')

    def info(self, msg):
        self._write('INFO', msg)

    def warning(self, msg):
        self._write('WARNING', msg)

    def error(self, msg):
        self._write('ERROR', msg)

    def debug(self, msg):
        self._write('DEBUG', msg)
