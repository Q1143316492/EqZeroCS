#!/usr/bin/env python3
"""
终端平铺管理器
通过输入参数搜索并平铺相关终端窗口
支持Windows平台，默认优先竖向平铺
"""

import argparse
import sys
import re
import time
from typing import List, Tuple, Optional
import subprocess

# Windows API相关导入
try:
    import win32gui
    import win32con
    import win32api
    HAS_WIN32 = True
except ImportError:
    HAS_WIN32 = False
    print("警告: 未安装pywin32，某些功能可能不可用")
    print("请运行: pip install pywin32")

class TerminalTiler:
    """终端平铺管理器"""
    
    def __init__(self):
        self.screen_width = 1920
        self.screen_height = 1080
        if HAS_WIN32:
            self.screen_width = win32api.GetSystemMetrics(win32con.SM_CXSCREEN)
            self.screen_height = win32api.GetSystemMetrics(win32con.SM_CYSCREEN)
    
    def _calculate_optimal_grid(self, num_windows: int, prefer_vertical: bool = True) -> Tuple[int, List[int]]:
        """
        计算最优的网格布局
        
        Args:
            num_windows: 窗口总数
            prefer_vertical: 是否优先垂直排列
            
        Returns:
            (列数, 每列的行数列表)
        """
        if num_windows <= 0:
            return (0, [])
        
        if num_windows == 1:
            return (1, [1])
        
        if num_windows == 2:
            return (1, [2]) if prefer_vertical else (2, [1, 1])
        
        # 对于3个或更多窗口，使用智能布局
        if prefer_vertical:
            # 垂直优先：尽量少列，但避免单列过挤
            if num_windows <= 2:
                return (1, [num_windows])  # 2个以内单列
            elif num_windows == 3:
                # 3个窗口：优先2列布局，避免单列过长
                return (2, [2, 1])  # 第一列2个，第二列1个
            elif num_windows <= 6:
                # 4-6个窗口用2列
                col1_count = (num_windows + 1) // 2  # 向上取整
                col2_count = num_windows - col1_count
                return (2, [col1_count, col2_count])
            elif num_windows <= 9:
                # 7-9个窗口用3列
                base_count = num_windows // 3
                remainder = num_windows % 3
                cols = [base_count] * 3
                # 将余数分配给前面的列
                for i in range(remainder):
                    cols[i] += 1
                return (3, cols)
            else:
                # 更多窗口，计算合适的列数
                cols = min(4, (num_windows + 2) // 3)  # 最多4列，每列大约3个
                base_count = num_windows // cols
                remainder = num_windows % cols
                col_counts = [base_count] * cols
                for i in range(remainder):
                    col_counts[i] += 1
                return (cols, col_counts)
        else:
            # 水平优先：尽量多列，少行
            import math
            cols = min(num_windows, int(math.ceil(math.sqrt(num_windows))))
            base_count = num_windows // cols
            remainder = num_windows % cols
            col_counts = [base_count] * cols
            for i in range(remainder):
                col_counts[i] += 1
            return (cols, col_counts)
    
    def find_terminal_windows(self, keyword: str) -> List[Tuple[int, str]]:
        """
        查找包含关键字的终端窗口
        返回: [(窗口句柄, 窗口标题), ...]
        """
        if not HAS_WIN32:
            print(f"模拟查找包含 '{keyword}' 的终端窗口...")
            return [(12345, f"模拟终端 - {keyword}1"), (12346, f"模拟终端 - {keyword}2")]
        
        windows = []
        
        def enum_window_callback(hwnd, windows):
            if win32gui.IsWindowVisible(hwnd):
                window_title = win32gui.GetWindowText(hwnd)
                class_name = win32gui.GetClassName(hwnd)
                
                # 检查是否为终端窗口类型
                terminal_classes = [
                    'ConsoleWindowClass',  # 传统命令提示符
                    'CASCADIA_HOSTING_WINDOW_CLASS',  # Windows Terminal
                    'PseudoConsoleWindow',  # PowerShell
                    'WindowsTerminal',
                    'VirtualConsoleClass'
                ]
                
                is_terminal = any(term_class in class_name for term_class in terminal_classes)
                has_keyword = keyword.lower() in window_title.lower()
                
                if is_terminal and has_keyword:
                    windows.append((hwnd, window_title))
                    print(f"找到终端: {window_title} (类名: {class_name})")
            
            return True
        
        try:
            win32gui.EnumWindows(enum_window_callback, windows)
        except Exception as e:
            print(f"枚举窗口时出错: {e}")
        
        return windows
    
    def tile_windows_vertical(self, windows: List[Tuple[int, str]], gap: int = 5):
        """
        智能垂直平铺窗口
        当窗口数量较多时自动使用多列布局，并限制窗口高度避免过长
        """
        if not windows:
            print("没有找到要平铺的窗口")
            return
        
        num_windows = len(windows)
        print(f"开始智能垂直平铺 {num_windows} 个窗口...")
        
        # 智能计算列数和行数
        cols, rows_per_col = self._calculate_optimal_grid(num_windows, prefer_vertical=True)
        
        # 设置最小和最大窗口高度（用于查看日志的合理高度）
        min_window_height = 200  # 最小高度
        max_window_height = 600  # 最大高度，避免窗口过长
        
        # 计算实际窗口尺寸
        available_width = self.screen_width - gap * (cols + 1)
        window_width = available_width // cols
        
        available_height = self.screen_height - gap * (max(rows_per_col) + 1)
        window_height = min(max_window_height, max(min_window_height, available_height // max(rows_per_col)))
        
        print(f"布局方案: {cols}列，每列最多{max(rows_per_col)}行")
        print(f"窗口尺寸: 宽度={window_width}, 高度={window_height}")
        
        # 按列排列窗口
        current_window = 0
        for col in range(cols):
            if current_window >= num_windows:
                break
                
            # 计算这一列要放置的窗口数量
            windows_in_this_col = rows_per_col[col] if col < len(rows_per_col) else 0
            if windows_in_this_col == 0:
                continue
                
            # 计算列的X位置
            col_x = gap + col * (window_width + gap)
            
            # 为这一列的窗口分配位置
            for row in range(windows_in_this_col):
                if current_window >= num_windows:
                    break
                    
                hwnd, title = windows[current_window]
                
                # 计算窗口位置
                x = col_x
                y = gap + row * (window_height + gap)
                
                if HAS_WIN32:
                    try:
                        # 确保窗口处于正常状态
                        win32gui.ShowWindow(hwnd, win32con.SW_RESTORE)
                        # 移动和调整窗口大小
                        win32gui.SetWindowPos(
                            hwnd, 
                            win32con.HWND_TOP, 
                            x, y, window_width, window_height,
                            win32con.SWP_SHOWWINDOW
                        )
                        print(f"已平铺窗口 [第{col+1}列第{row+1}行]: {title}")
                        print(f"  -> 位置({x}, {y}) 大小({window_width}, {window_height})")
                    except Exception as e:
                        print(f"平铺窗口 '{title}' 时出错: {e}")
                else:
                    print(f"模拟平铺窗口 [第{col+1}列第{row+1}行]: {title}")
                    print(f"  -> 位置({x}, {y}) 大小({window_width}, {window_height})")
                
                current_window += 1
                # 短暂延迟，让窗口有时间响应
                time.sleep(0.1)
    
    def tile_windows_horizontal(self, windows: List[Tuple[int, str]], gap: int = 5):
        """
        智能水平平铺窗口
        当窗口数量较多时自动使用多行布局，保持合理的窗口宽度
        """
        if not windows:
            print("没有找到要平铺的窗口")
            return
        
        num_windows = len(windows)
        print(f"开始智能水平平铺 {num_windows} 个窗口...")
        
        # 智能计算列数和行数（水平优先）
        cols, rows_per_col = self._calculate_optimal_grid(num_windows, prefer_vertical=False)
        
        # 设置最小和最大窗口宽度
        min_window_width = 300   # 最小宽度，保证终端可读性
        max_window_width = 800   # 最大宽度，避免窗口过宽
        
        # 计算实际窗口尺寸
        available_width = self.screen_width - gap * (cols + 1)
        window_width = min(max_window_width, max(min_window_width, available_width // cols))
        
        # 对于水平布局，行数就是最大的列高度
        max_rows = max(rows_per_col) if rows_per_col else 1
        available_height = self.screen_height - gap * (max_rows + 1)
        window_height = available_height // max_rows
        
        print(f"布局方案: {cols}列{max_rows}行网格")
        print(f"窗口尺寸: 宽度={window_width}, 高度={window_height}")
        
        # 按行优先排列窗口（水平平铺特色）
        current_window = 0
        
        # 重新组织窗口分配逻辑 - 按行优先填充
        window_positions = []  # [(窗口索引, 行, 列), ...]
        
        # 计算每行应该放多少个窗口
        for row in range(max_rows):
            for col in range(cols):
                if current_window >= num_windows:
                    break
                # 检查这一列在当前行是否应该有窗口
                if row < rows_per_col[col]:
                    window_positions.append((current_window, row, col))
                    current_window += 1
        
        # 根据计算好的位置放置窗口
        for window_idx, row, col in window_positions:
            hwnd, title = windows[window_idx]
            
            # 计算窗口位置
            x = gap + col * (window_width + gap)
            y = gap + row * (window_height + gap)
            
            if HAS_WIN32:
                try:
                    # 确保窗口处于正常状态
                    win32gui.ShowWindow(hwnd, win32con.SW_RESTORE)
                    # 移动和调整窗口大小
                    win32gui.SetWindowPos(
                        hwnd, 
                        win32con.HWND_TOP, 
                        x, y, window_width, window_height,
                        win32con.SWP_SHOWWINDOW
                    )
                    print(f"已平铺窗口 [第{row+1}行第{col+1}列]: {title}")
                    print(f"  -> 位置({x}, {y}) 大小({window_width}, {window_height})")
                except Exception as e:
                    print(f"平铺窗口 '{title}' 时出错: {e}")
            else:
                print(f"模拟平铺窗口 [第{row+1}行第{col+1}列]: {title}")
                print(f"  -> 位置({x}, {y}) 大小({window_width}, {window_height})")
            
            # 短暂延迟，让窗口有时间响应
            time.sleep(0.1)
    
    def list_all_terminals(self):
        """
        列出所有终端窗口
        """
        if not HAS_WIN32:
            print("模拟模式：列出所有终端窗口")
            terminals = [
                "gas1 - PowerShell",
                "gas2 - PowerShell", 
                "gcc1 - Command Prompt",
                "gcc2 - Command Prompt",
                "gds - Windows Terminal"
            ]
            for terminal in terminals:
                print(f"  - {terminal}")
            return
        
        print("搜索所有终端窗口...")
        all_windows = []
        
        def enum_all_callback(hwnd, windows):
            if win32gui.IsWindowVisible(hwnd):
                window_title = win32gui.GetWindowText(hwnd)
                class_name = win32gui.GetClassName(hwnd)
                
                terminal_classes = [
                    'ConsoleWindowClass',
                    'CASCADIA_HOSTING_WINDOW_CLASS',
                    'PseudoConsoleWindow',
                    'WindowsTerminal',
                    'VirtualConsoleClass'
                ]
                
                if any(term_class in class_name for term_class in terminal_classes) and window_title:
                    windows.append((hwnd, window_title, class_name))
            
            return True
        
        win32gui.EnumWindows(enum_all_callback, all_windows)
        
        if all_windows:
            print(f"找到 {len(all_windows)} 个终端窗口:")
            for hwnd, title, class_name in all_windows:
                print(f"  - {title} ({class_name})")
        else:
            print("未找到任何终端窗口")

def main():
    parser = argparse.ArgumentParser(
        description="终端平铺管理器 - 搜索并平铺相关终端窗口",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
使用示例:
  python terminal_tiler.py gas                    # 垂直平铺所有包含'gas'的终端
  python terminal_tiler.py gas --horizontal       # 水平平铺所有包含'gas'的终端
  python terminal_tiler.py --list                 # 列出所有终端窗口
  python terminal_tiler.py g --hide               # 最小化所有相关终端窗口
  python terminal_tiler.py gcc --gap 10           # 平铺gcc相关终端，窗口间距为10像素
        """
    )
    
    parser.add_argument(
        'keyword', 
        nargs='?',
        help='要搜索的终端关键字 (如: gas, gcc, gds)'
    )
    
    parser.add_argument(
        '--horizontal', 
        action='store_true',
        help='水平平铺窗口 (默认为垂直平铺)'
    )
    
    parser.add_argument(
        '--gap', 
        type=int, 
        default=5,
        help='窗口间距 (像素，默认5)'
    )
    
    parser.add_argument(
        '--list', 
        action='store_true',
        help='列出所有终端窗口'
    )
    
    parser.add_argument(
        '--hide',
        action='store_true',
        help='隐藏所有扫描到的终端窗口'
    )
    args = parser.parse_args()
    
    tiler = TerminalTiler()
    
    if args.list:
        tiler.list_all_terminals()
        return
    
    if not args.keyword:
        print("错误: 请提供要搜索的关键字，或使用 --list 查看所有终端")
        parser.print_help()
        return
    
    print(f"搜索包含关键字 '{args.keyword}' 的终端窗口...")
    windows = tiler.find_terminal_windows(args.keyword)
    
    if not windows:
        print(f"未找到包含 '{args.keyword}' 的终端窗口")
        print("使用 --list 参数查看所有可用的终端窗口")
        return
    
    print(f"找到 {len(windows)} 个匹配的终端窗口:")
    for hwnd, title in windows:
        print(f"  - {title}")

    if args.hide:
        # 最小化所有扫描到的终端窗口
        if not HAS_WIN32:
            print("模拟最小化所有终端窗口")
            for hwnd, title in windows:
                print(f"  - 已最小化: {title}")
        else:
            for hwnd, title in windows:
                try:
                    win32gui.ShowWindow(hwnd, win32con.SW_MINIMIZE)
                    print(f"已最小化: {title}")
                except Exception as e:
                    print(f"最小化窗口 '{title}' 时出错: {e}")
        print("终端窗口最小化完成!")
        return
    
    # 平铺窗口
    if args.horizontal:
        tiler.tile_windows_horizontal(windows, args.gap)
    else:
        tiler.tile_windows_vertical(windows, args.gap)
    
    print("终端平铺完成!")

if __name__ == "__main__":
    main()
