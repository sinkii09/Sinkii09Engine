#!/usr/bin/env python3
"""
Logging utilities for Sinkii09 Engine automation
Provides consistent logging across all automation scripts
"""
import sys
from datetime import datetime
from typing import Optional
from enum import Enum

class LogLevel(Enum):
    DEBUG = "DEBUG"
    INFO = "INFO"
    WARNING = "WARNING" 
    ERROR = "ERROR"
    SUCCESS = "SUCCESS"

class Logger:
    """Simple, colorful logger for automation scripts"""
    
    COLORS = {
        LogLevel.DEBUG: '\033[0;37m',      # Gray
        LogLevel.INFO: '\033[0;34m',       # Blue
        LogLevel.WARNING: '\033[1;33m',    # Yellow
        LogLevel.ERROR: '\033[0;31m',      # Red
        LogLevel.SUCCESS: '\033[0;32m',    # Green
    }
    
    EMOJIS = {
        LogLevel.DEBUG: '🔍',
        LogLevel.INFO: 'ℹ️',
        LogLevel.WARNING: '⚠️',
        LogLevel.ERROR: '❌',
        LogLevel.SUCCESS: '✅'
    }
    
    RESET = '\033[0m'
    
    def __init__(self, name: str = "automation", use_colors: bool = True):
        self.name = name
        self.use_colors = use_colors
        self.min_level = LogLevel.INFO
    
    def _format_message(self, level: LogLevel, message: str, prefix: Optional[str] = None) -> str:
        """Format log message with colors and emojis"""
        timestamp = datetime.now().strftime('%H:%M:%S')
        emoji = self.EMOJIS[level]
        
        if prefix:
            formatted = f"{emoji} {prefix} {message}"
        else:
            formatted = f"{emoji} {message}"
        
        if self.use_colors:
            color = self.COLORS[level]
            return f"{color}{formatted}{self.RESET}"
        else:
            return formatted
    
    def debug(self, message: str, prefix: Optional[str] = None):
        """Log debug message"""
        if self.min_level == LogLevel.DEBUG:
            print(self._format_message(LogLevel.DEBUG, message, prefix))
    
    def info(self, message: str, prefix: Optional[str] = None):
        """Log info message"""
        print(self._format_message(LogLevel.INFO, message, prefix))
    
    def warning(self, message: str, prefix: Optional[str] = None):
        """Log warning message"""
        print(self._format_message(LogLevel.WARNING, message, prefix))
    
    def error(self, message: str, prefix: Optional[str] = None, exit_code: Optional[int] = None):
        """Log error message and optionally exit"""
        print(self._format_message(LogLevel.ERROR, message, prefix), file=sys.stderr)
        if exit_code is not None:
            sys.exit(exit_code)
    
    def success(self, message: str, prefix: Optional[str] = None):
        """Log success message"""
        print(self._format_message(LogLevel.SUCCESS, message, prefix))
    
    def step(self, message: str, step_num: Optional[int] = None, total: Optional[int] = None):
        """Log a process step"""
        if step_num and total:
            prefix = f"Step {step_num}/{total}:"
        else:
            prefix = "Step:"
        self.info(message, prefix)
    
    def progress(self, current: int, total: int, message: str = ""):
        """Log progress"""
        percentage = int((current / total) * 100)
        bar_length = 20
        filled = int((current / total) * bar_length)
        bar = "█" * filled + "░" * (bar_length - filled)
        
        progress_msg = f"[{bar}] {percentage}% ({current}/{total})"
        if message:
            progress_msg += f" - {message}"
        
        # Use carriage return to overwrite previous progress
        print(f"\r🔄 {progress_msg}", end="", flush=True)
        
        # Print newline when complete
        if current == total:
            print()
    
    def section(self, title: str):
        """Log a section header"""
        separator = "=" * len(title)
        print(f"\n{separator}")
        print(f"🎯 {title}")
        print(f"{separator}")
    
    def subsection(self, title: str):
        """Log a subsection header"""
        print(f"\n📋 {title}")
        print("-" * (len(title) + 4))

# Global logger instance
logger = Logger()