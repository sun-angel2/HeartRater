import json
import time
import requests
import threading
import paho.mqtt.client as mqtt
from mcrcon import MCRcon

# ================= 核心配置区 =================

# 1. MC服务器 RCON 信息
RCON_HOST = '127.0.0.1'
RCON_PORT = 25575
RCON_PASS = '123456'

# 2. 玩家数据源列表 (关键！)
# 格式：'玩家ID': {'type': '类型', 'source': '地址'}
PLAYERS_CONFIG = {
    # === 你的配置 (HTTP/IPv6 模式) ===
    'xiao_qv_angel': {
        'type': 'http', 
        'source': 'http://[240e:398:f11:7ee9:1df0:e8a7:9003:46f8]:8088/api/hr' 
        # 注意：这里要加上 /api/hr 后缀，因为你的软件接口在这个路径下
    },

    # === 朋友A (如果他也用网址) ===
    # 'Friend_A': {'type': 'http', 'source': 'http://1.2.3.4:8088/api/hr'},

    # === 朋友B (如果他用网页/MQTT) ===
    # 'Friend_B': {'type': 'mqtt', 'topic': 'iqoo_watch_share/hr_data'},
}

# 3. MQTT 公共配置 (如果有玩家用MQTT)
MQTT_BROKER = 'broker.emqx.io'
MQTT_PORT = 1883
# ============================================

def update_score(player, hr):
    """ 发送 RCON 指令的核心函数 """
    if not hr: return
    try:
        with MCRcon(RCON_HOST, RCON_PASS, port=RCON_PORT) as mcr:
            mcr.command(f'scoreboard players set {player} heart_rate {hr}')
            # print(f"同步 -> {player}: {hr}") # 调试时可取消注释
    except Exception as e:
        print(f"RCON 错误 ({player}): {e}")

# --- 模块1: HTTP 轮询 (用于你的 IPv6) ---
def http_poller_loop():
    print("🌐 HTTP 轮询线程已启动...")
    while True:
        for player, config in PLAYERS_CONFIG.items():
            if config['type'] == 'http':
                try:
                    # 设置超时，防止卡顿
                    resp = requests.get(config['source'], timeout=2)
                    if resp.status_code == 200:
                        data = resp.json()
                        hr = data.get('hr')
                        if hr and hr > 0:
                            update_score(player, hr)
                except Exception as e:
                    # 网络波动很正常，不刷屏报错
                    pass 
        time.sleep(1) # 每秒轮询一次

# --- 模块2: MQTT 监听 (用于网页版玩家) ---
def on_mqtt_message(client, userdata, msg):
    try:
        payload = json.loads(msg.payload.decode())
        hr = payload.get('hr')
        topic = msg.topic
        
        # 查找是谁的 Topic
        for player, config in PLAYERS_CONFIG.items():
            if config['type'] == 'mqtt' and config.get('topic') == topic:
                update_score(player, hr)
    except:
        pass

def start_mqtt():
    # 扫描配置里有没有人用 MQTT
    topics = [cfg['topic'] for cfg in PLAYERS_CONFIG.values() if cfg['type'] == 'mqtt']
    if not topics:
        print("ℹ️ 当前配置无 MQTT 玩家，跳过 MQTT 连接")
        return

    client = mqtt.Client()
    client.on_message = on_mqtt_message
    try:
        client.connect(MQTT_BROKER, MQTT_PORT, 60)
        for t in topics:
            client.subscribe(t)
            print(f"📡 已订阅 MQTT 频道: {t}")
        client.loop_start() # 在后台线程运行
    except Exception as e:
        print(f"MQTT 连接失败: {e}")

# --- 主程序 ---
if __name__ == "__main__":
    print("🚀 服务器心率同步网关已启动")
    
    # 1. 启动 MQTT (后台)
    start_mqtt()
    
    # 2. 启动 HTTP 轮询 (主线程阻断运行)
    try:
        http_poller_loop()
    except KeyboardInterrupt:
        print("停止运行")