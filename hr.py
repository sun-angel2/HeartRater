import customtkinter as ctk
import asyncio
import threading
import struct
import sys
import socket
import logging
from flask import Flask, jsonify, render_template_string
from bleak import BleakClient, BleakScanner

# --- 1. 设置日志级别 ---
log = logging.getLogger('werkzeug')
log.setLevel(logging.ERROR)

# --- 2. BLE 常量 ---
HR_SERVICE_UUID = "0000180d-0000-1000-8000-00805f9b34fb"
HR_CHAR_UUID = "00002a37-0000-1000-8000-00805f9b34fb"

# --- 3. 全局状态 ---
hr_value = 0
current_device_address = None
ble_client = None
is_scanning = False
is_connected = False

# --- 4. Web Server 配置 ---
flask_app = Flask(__name__)
WEB_PORT = 8088

# HTML 模板
HTML_TEMPLATE = """
<!DOCTYPE html>
<html lang="zh">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>心率监控 (Web版)</title>
    <style>
        body { background-color: #000; color: #fff; font-family: 'Segoe UI', sans-serif; display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100vh; margin: 0; overflow: hidden; }
        .heart-icon { font-size: 100px; color: #ff3b30; margin-bottom: 20px; display: inline-block; transition: transform 0.1s ease; }
        .hr-value { font-size: 80px; font-weight: bold; color: #2ECC71; }
        .label { font-size: 20px; color: #888; margin-top: -10px;}
        .beat { animation: heartbeat 0.4s ease-in-out; }
        @keyframes heartbeat { 0% { transform: scale(1); } 25% { transform: scale(1.3); } 50% { transform: scale(1); } 75% { transform: scale(1.15); } 100% { transform: scale(1); } }
        .footer { position: fixed; bottom: 10px; font-size: 12px; color: #444; }
    </style>
</head>
<body>
    <div style="text-align: center;">
        <div id="heart" class="heart-icon">❤️</div>
        <div id="hr-display" class="hr-value">--</div>
        <div class="label">BPM</div>
    </div>
    <div class="footer">Real-time Heart Rate</div>
    <script>
        function updateHeartRate() {
            fetch('/api/hr').then(r => r.json()).then(data => {
                const hrDisplay = document.getElementById('hr-display');
                const heart = document.getElementById('heart');
                if (data.hr > 0) {
                    hrDisplay.innerText = data.hr;
                    hrDisplay.style.color = "#2ECC71";
                    heart.classList.remove('beat'); void heart.offsetWidth; heart.classList.add('beat');
                } else {
                    hrDisplay.innerText = "--"; hrDisplay.style.color = "grey";
                }
            }).catch(e => console.error(e));
        }
        setInterval(updateHeartRate, 1000);
    </script>
</body>
</html>
"""

@flask_app.route('/')
def index(): return render_template_string(HTML_TEMPLATE)

@flask_app.route('/api/hr')
def get_hr(): global hr_value; return jsonify({"hr": hr_value})

def run_flask():
    print(f"Web Server 正在启动... 监听端口 {WEB_PORT}")
    try:
        flask_app.run(host='::', port=WEB_PORT, debug=False, use_reloader=False, threaded=True)
    except Exception as e:
        print(f"Web Server 启动失败 (IPv6): {e}")
        try:
            flask_app.run(host='0.0.0.0', port=WEB_PORT, debug=False, use_reloader=False, threaded=True)
        except Exception as e2: print(f"Web Server 启动彻底失败: {e2}")


# --- 5. [美化版] 桌面显示小组件 ---
class HRWidget(ctk.CTkToplevel):
    def __init__(self, master):
        super().__init__(master)
        self.withdraw()
        self.overrideredirect(True) 
        self.attributes('-topmost', True) 
        # 设置透明背景色
        self.wm_attributes("-transparentcolor", "#000001") 
        self.config(bg="#000001") 

        # 主容器
        self.display_frame = ctk.CTkFrame(self, fg_color="#000001")
        self.display_frame.pack(padx=0, pady=0)

        # 布局容器：横向排列 [心形] [数字]
        self.content_box = ctk.CTkFrame(self.display_frame, fg_color="#000001")
        self.content_box.pack()

        # 心形图标
        self.label_heart = ctk.CTkLabel(
            self.content_box, 
            text="❤️", 
            font=("Segoe UI Emoji", 32), 
            text_color="#FF3B30", # 苹果红
            bg_color="#000001"
        )
        self.label_heart.pack(side="left", padx=(0, 5), pady=0) 

        # 数字容器 (包含数值和单位)
        self.text_box = ctk.CTkFrame(self.content_box, fg_color="#000001")
        self.text_box.pack(side="left")

        # 心率数值
        self.label_hr = ctk.CTkLabel(
            self.text_box, 
            text="---", 
            font=("Impact", 48), # 使用 Impact 字体更有 HUD 的感觉，如果没有则回退
            text_color="#2ECC71", 
            bg_color="#000001"
        )
        self.label_hr.pack(side="left", anchor="s") # 底部对齐

        # BPM 单位 (小一点)
        self.label_unit = ctk.CTkLabel(
            self.text_box,
            text=" BPM",
            font=("Arial", 14, "bold"),
            text_color="#888888",
            bg_color="#000001"
        )
        self.label_unit.pack(side="left", anchor="s", pady=(0, 8)) # 稍微抬高一点

        # 绑定拖动事件
        for widget in [self.display_frame, self.content_box, self.text_box, self.label_heart, self.label_hr, self.label_unit]:
            widget.bind('<ButtonPress-1>', self._start_drag)
            widget.bind('<B1-Motion>', self._do_drag)
        
        self.heart_size_toggle = False
        self._animate_heart() 

    def _start_drag(self, event):
        self._offsetx = event.x
        self._offsety = event.y

    def _do_drag(self, event):
        x = self.winfo_x() + event.x - self._offsetx
        y = self.winfo_y() + event.y - self._offsety
        self.geometry(f"+{x}+{y}")

    def _animate_heart(self):
        # 获取基准大小
        try:
            base_size = int(self.master.size_slider.get())
        except:
            base_size = 48

        icon_size = int(base_size * 0.7)
        
        if self.heart_size_toggle:
            self.label_heart.configure(font=("Segoe UI Emoji", icon_size + 4)) 
        else:
            self.label_heart.configure(font=("Segoe UI Emoji", icon_size)) 
        
        self.heart_size_toggle = not self.heart_size_toggle
        self.after(500, self._animate_heart)

    def get_color_by_zone(self, hr):
        """根据心率区间返回颜色"""
        if hr < 100: return "#2ECC71" # 绿色 (轻松)
        if hr < 120: return "#F1C40F" # 黄色 (燃脂)
        if hr < 140: return "#E67E22" # 橙色 (耐力)
        return "#E74C3C"              # 红色 (极限)

    def update_hr_display(self, hr_val, custom_color, size_val):
        font_size = int(size_val)
        
        if hr_val > 0:
            # 如果用户没填自定义颜色，则使用动态区间颜色
            if not self.master.color_entry.get():
                display_color = self.get_color_by_zone(hr_val)
            else:
                display_color = custom_color

            self.label_hr.configure(
                text=str(hr_val), 
                text_color=display_color, 
                font=("Impact", font_size)
            )
            self.label_heart.configure(text_color="#FF3B30") # 心形始终红色
            self.label_unit.configure(text_color=display_color)
        else:
            self.label_hr.configure(text="---", text_color="grey")
            self.label_heart.configure(text_color="grey")
            self.label_unit.configure(text_color="grey")
        
        self.update_idletasks()


# --- 6. 主控制面板应用程序 ---
class HRControlApp(ctk.CTk):
    def __init__(self):
        super().__init__()
        self.title("心率助手 v4.1 (HUD版)")
        self.geometry("380x680") 
        self.resizable(False, False)
        
        self.current_hr_color = "#2ECC71"
        self.device_list = {} 
        self.ble_loop = None 
        self.stop_event = None 
        self.web_server_started = False
        self.ipv6_address = self._get_global_ipv6()
        
        self.toggle_display_var = ctk.BooleanVar(value=True) 
        self.hr_widget = HRWidget(self)
        self._setup_ui()
        self.after(500, self._check_status_update) 
        self.protocol("WM_DELETE_WINDOW", self._on_closing)

    def _get_global_ipv6(self):
        try:
            info = socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET6)
            candidates = []
            for item in info:
                ip = item[4][0]
                if "%" in ip: ip = ip.split("%")[0]
                if not ip.startswith("fe80") and not ip == "::1": candidates.append(ip)
            for ip in candidates:
                if ip.startswith("2"): return ip 
            return candidates[0] if candidates else None
        except: return None

    def _get_local_ipv4(self):
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM); s.connect(("8.8.8.8", 80))
            ip = s.getsockname()[0]; s.close(); return ip
        except: return "127.0.0.1"

    def _setup_ui(self):
        # 状态栏
        status_frame = ctk.CTkFrame(self)
        status_frame.pack(fill="x", padx=15, pady=(20, 10))
        self.status_dot = ctk.CTkLabel(status_frame, text="●", font=("Arial", 20), text_color="red")
        self.status_dot.pack(side="left", padx=(10, 5))
        self.conn_label = ctk.CTkLabel(status_frame, text="未连接", font=("Segoe UI", 14), text_color="white")
        self.conn_label.pack(side="left", fill="x", expand=True)
        self.device_name_label = ctk.CTkLabel(status_frame, text="设备: 无", font=("Segoe UI", 12), text_color="gray")
        self.device_name_label.pack(side="right", padx=(5, 10))
        
        # 连接控制
        ctk.CTkLabel(self, text="设备连接", font=("Segoe UI", 16, "bold")).pack(pady=(10, 5))
        self.scan_button = ctk.CTkButton(self, text="① 扫描设备", command=self._start_scan)
        self.scan_button.pack(pady=5, padx=20, fill="x")
        self.device_combo = ctk.CTkComboBox(self, values=["请先扫描"], state="disabled")
        self.device_combo.pack(pady=5, padx=20, fill="x")
        self.connect_button = ctk.CTkButton(self, text="② 连接选中设备", command=self._start_connect, state="disabled", fg_color="blue", hover_color="#4169e1")
        self.connect_button.pack(pady=(5, 10), padx=20, fill="x")

        # IPv6 Web
        ctk.CTkFrame(self, height=2, fg_color="#444444").pack(fill="x", padx=20, pady=10)
        ctk.CTkLabel(self, text=f"远程访问 (端口 {WEB_PORT})", font=("Segoe UI", 16, "bold")).pack(pady=(5, 5))
        self.web_switch = ctk.CTkSwitch(self, text="开启 Web 服务", command=self._toggle_web_server)
        self.web_switch.pack(pady=5)
        self.url_entry = ctk.CTkEntry(self, placeholder_text="服务未开启")
        self.url_entry.pack(pady=5, padx=20, fill="x")
        self.url_entry.configure(state="readonly")
        self.copy_btn = ctk.CTkButton(self, text="复制链接", command=self._copy_url, fg_color="gray", state="disabled")
        self.copy_btn.pack(pady=5)
        ctk.CTkLabel(self, text=f"提示: 确保防火墙放行端口 {WEB_PORT}", font=("Segoe UI", 11), text_color="gray").pack()

        # 显示控制
        ctk.CTkFrame(self, height=2, fg_color="#444444").pack(fill="x", padx=20, pady=10)
        ctk.CTkLabel(self, text="HUD 显示控制", font=("Segoe UI", 16, "bold")).pack(pady=(5, 5))
        self.toggle_switch = ctk.CTkSwitch(self, text="桌面悬浮窗", command=self._toggle_display, width=60, variable=self.toggle_display_var)
        self.toggle_switch.pack(pady=5)
        
        ctk.CTkLabel(self, text="大小调整", font=("Segoe UI", 12)).pack(pady=2)
        self.size_slider = ctk.CTkSlider(self, from_=30, to=120, number_of_steps=90, command=self._on_size_change)
        self.size_slider.set(60)
        self.size_slider.pack(pady=5, padx=20, fill="x")

        ctk.CTkLabel(self, text="固定颜色 (留空则启用动态区间色)", font=("Segoe UI", 12)).pack(pady=5)
        color_frame = ctk.CTkFrame(self, fg_color="transparent")
        color_frame.pack(fill="x", padx=20)
        self.color_entry = ctk.CTkEntry(color_frame, placeholder_text="例如: #00FF00")
        self.color_entry.pack(side="left", fill="x", expand=True, padx=(0, 10))
        ctk.CTkButton(color_frame, text="应用", command=self._apply_custom_color, width=60).pack(side="right")

        ctk.CTkButton(self, text="退出软件", command=self._on_closing, fg_color="red", hover_color="#c0392b").pack(pady=(20, 10), padx=20, fill="x")

    # 逻辑部分保持精简
    def _toggle_web_server(self):
        if self.web_switch.get() == 1:
            self.ipv6_address = self._get_global_ipv6()
            if not self.web_server_started:
                flask_app.config['BIND_IP'] = self.ipv6_address if self.ipv6_address else "0.0.0.0"
                threading.Thread(target=run_flask, daemon=True).start()
                self.web_server_started = True
            
            if self.ipv6_address:
                url = f"http://[{self.ipv6_address}]:{WEB_PORT}"
                self.copy_btn.configure(state="normal", fg_color="green", text="复制 IPv6 链接")
            else:
                url = f"http://{self._get_local_ipv4()}:{WEB_PORT}"
                self.copy_btn.configure(state="normal", fg_color="#AA5500", text="复制内网链接")
            self.url_entry.configure(state="normal"); self.url_entry.delete(0, "end"); self.url_entry.insert(0, url); self.url_entry.configure(state="readonly")
        else:
            self.url_entry.configure(state="normal"); self.url_entry.delete(0, "end"); self.url_entry.insert(0, "服务已暂停"); self.url_entry.configure(state="readonly"); self.copy_btn.configure(state="disabled", fg_color="gray")

    def _copy_url(self):
        self.clipboard_clear(); self.clipboard_append(self.url_entry.get()); self.copy_btn.configure(text="已复制！"); self.after(2000, lambda: self.copy_btn.configure(text="复制链接"))

    def _toggle_display(self):
        if self.toggle_display_var.get(): self.hr_widget.deiconify()
        else: self.hr_widget.withdraw()
    def _on_size_change(self, value): self.hr_widget.update_hr_display(hr_value, self.current_hr_color, value)
    def _apply_custom_color(self): self.current_hr_color = self.color_entry.get(); self.hr_widget.update_hr_display(hr_value, self.current_hr_color, self.size_slider.get())
    
    def _check_status_update(self):
        if is_scanning: self.status_dot.configure(text_color="yellow"); self.conn_label.configure(text="正在扫描...")
        elif is_connected: self.status_dot.configure(text_color="#00FF00"); self.conn_label.configure(text="已连接")
        else: self.status_dot.configure(text_color="red"); self.conn_label.configure(text="未连接")
        self.hr_widget.update_hr_display(hr_value, self.current_hr_color, self.size_slider.get())
        self.after(200, self._check_status_update)

    def _start_scan(self):
        global is_scanning
        if is_scanning: return
        self.device_combo.set("扫描中..."); self.scan_button.configure(state="disabled", text="扫描中..."); is_scanning = True
        threading.Thread(target=self._run_ble_loop_for_scan, daemon=True).start()

    def _start_connect(self):
        global current_device_address
        selected_name = self.device_combo.get()
        if selected_name not in self.device_list: return
        current_device_address = self.device_list[selected_name]
        self.connect_button.configure(state="disabled", text="连接中..."); self.scan_button.configure(state="disabled")
        threading.Thread(target=self._run_ble_loop_for_connect, daemon=True).start()

    def _run_ble_loop_for_scan(self):
        loop = asyncio.new_event_loop(); asyncio.set_event_loop(loop); loop.run_until_complete(self._scan_devices()); loop.close()

    async def _scan_devices(self):
        global is_scanning; self.device_list = {}
        try:
            devices = await BleakScanner.discover(timeout=5.0)
            for device in devices:
                name = device.name or "Unknown"
                if "iqoo" in name.lower() or "watch" in name.lower():
                    self.device_list[f"{name} ({device.address})"] = device.address
        except Exception as e: print(f"Scan error: {e}")
        self.after(0, self._update_scan_results); is_scanning = False

    def _update_scan_results(self):
        names = list(self.device_list.keys())
        self.device_combo.configure(values=names, state="normal")
        if names: self.device_combo.set(names[0]); self.connect_button.configure(state="normal")
        else: self.device_combo.set("未找到设备")
        self.scan_button.configure(state="normal", text="① 扫描设备")

    def _run_ble_loop_for_connect(self):
        loop = asyncio.new_event_loop(); asyncio.set_event_loop(loop); self.ble_loop = loop; self.stop_event = asyncio.Event()
        try: loop.run_until_complete(self._connect_and_read_hr())
        finally: loop.close(); self.ble_loop = None

    async def _handle_hr_data(self, sender, data):
        global hr_value
        flags = data[0]
        hr_value = struct.unpack('<H', data[1:3])[0] if flags & 0x1 else struct.unpack('<B', data[1:2])[0]

    async def _connect_and_read_hr(self):
        global ble_client, is_connected, hr_value
        while not self.stop_event.is_set():
            try:
                print(f"Connecting to {current_device_address}...")
                async with BleakClient(current_device_address) as client:
                    ble_client = client; is_connected = True
                    self.after(0, lambda: self.device_name_label.configure(text=f"设备: {self.device_combo.get()}"))
                    self.after(0, lambda: self.connect_button.configure(text="已连接", state="disabled"))
                    await client.start_notify(HR_CHAR_UUID, self._handle_hr_data)
                    while not self.stop_event.is_set() and client.is_connected: await asyncio.sleep(1)
                    await client.stop_notify(HR_CHAR_UUID)
            except asyncio.CancelledError: break
            except Exception as e:
                print(f"Error: {e}"); is_connected = False; hr_value = 0
                self.after(0, lambda: self.connect_button.configure(text="连接断开，3秒后重试..."))
                if not self.stop_event.is_set(): await asyncio.sleep(3)
        is_connected = False; hr_value = 0; ble_client = None
        self.after(0, lambda: [self.connect_button.configure(state="normal", text="② 连接选中设备"), self.scan_button.configure(state="normal"), self.device_name_label.configure(text="设备: 无")])

    def _on_closing(self):
        if self.ble_loop and self.ble_loop.is_running() and self.stop_event: self.ble_loop.call_soon_threadsafe(self.stop_event.set)
        self.after(100, lambda: [self.hr_widget.destroy(), self.destroy(), sys.exit()])

if __name__ == "__main__":
    ctk.set_appearance_mode("Dark"); ctk.set_default_color_theme("blue")
    gui_app = HRControlApp(); gui_app.mainloop()