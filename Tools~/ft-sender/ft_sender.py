#!/usr/bin/env python3
"""ネイティブ FT の確認用に、固定値の顔データを VRChat へ送るツール（仕様書 14.2 の 1）

VRCFT の BinaryFaceDataSender と同じ形式で、OSC アドレス /tracking/face/v1 に
102 バイトの blob を送る。バイト i は VRCAvatarDescriptor.FaceTrackingKey の i 番目のキーで、
0〜1 の値を 0〜255 に量子化したもの。

使う前に VRCFT を止めること。両方が送ると値が交互に入れ替わり、判定できない。

標準ライブラリだけで動く（GUI は tkinter）。

    python ft_sender.py                         # GUI
    python ft_sender.py --preset a4-jaw-half    # プリセットを送り続ける（Ctrl+C で止める）
    python ft_sender.py --set JawOpen=0.5 --set MouthCornerPull=1
    python ft_sender.py --list-presets
"""

import argparse
import math
import socket
import struct
import sys
import threading
import time

ADDRESS = "/tracking/face/v1"

# VRCAvatarDescriptor.FaceTrackingKey の並び（SDK 3.10.6）。VRCFT の UnifiedTrackingData.CopyTo と同じ順
KEYS = [
    "EyeLookOutRight", "EyeLookInRight", "EyeLookUpRight", "EyeLookDownRight",
    "EyeLookOutLeft", "EyeLookInLeft", "EyeLookUpLeft", "EyeLookDownLeft",
    "EyeClosedRight", "EyeClosedLeft",
    "EyeDilationRight", "EyeDilationLeft", "EyeConstrictRight", "EyeConstrictLeft",
    "EyeSquintRight", "EyeSquintLeft", "EyeWideRight", "EyeWideLeft",
    "BrowPinchRight", "BrowPinchLeft", "BrowLowererRight", "BrowLowererLeft",
    "BrowInnerUpRight", "BrowInnerUpLeft", "BrowOuterUpRight", "BrowOuterUpLeft",
    "NasalDilationRight", "NasalDilationLeft", "NasalConstrictRight", "NasalConstrictLeft",
    "CheekSquintRight", "CheekSquintLeft", "CheekPuffRight", "CheekPuffLeft",
    "CheekSuckRight", "CheekSuckLeft",
    "JawOpen", "JawRight", "JawLeft", "JawForward", "JawBackward", "JawClench", "JawMandibleRaise",
    "MouthClosed",
    "LipSuckUpperRight", "LipSuckUpperLeft", "LipSuckLowerRight", "LipSuckLowerLeft",
    "LipSuckCornerRight", "LipSuckCornerLeft",
    "LipFunnelUpperRight", "LipFunnelUpperLeft", "LipFunnelLowerRight", "LipFunnelLowerLeft",
    "LipPuckerUpperRight", "LipPuckerUpperLeft", "LipPuckerLowerRight", "LipPuckerLowerLeft",
    "MouthUpperUpRight", "MouthUpperUpLeft", "MouthUpperDeepenRight", "MouthUpperDeepenLeft",
    "NoseSneerRight", "NoseSneerLeft", "MouthLowerDownRight", "MouthLowerDownLeft",
    "MouthUpperRight", "MouthUpperLeft", "MouthLowerRight", "MouthLowerLeft",
    "MouthCornerPullRight", "MouthCornerPullLeft", "MouthCornerSlantRight", "MouthCornerSlantLeft",
    "MouthFrownRight", "MouthFrownLeft", "MouthStretchRight", "MouthStretchLeft",
    "MouthDimpleRight", "MouthDimpleLeft", "MouthRaiserUpper", "MouthRaiserLower",
    "MouthPressRight", "MouthPressLeft", "MouthTightenerRight", "MouthTightenerLeft",
    "TongueOut", "TongueUp", "TongueDown", "TongueRight", "TongueLeft", "TongueRoll",
    "TongueBendDown", "TongueCurlUp", "TongueSquish", "TongueFlat",
    "TongueTwistRight", "TongueTwistLeft",
    "SoftPalateClose", "ThroatSwallow", "NeckFlexRight", "NeckFlexLeft",
]
assert len(KEYS) == 102

KEY_INDEX = {name: i for i, name in enumerate(KEYS)}

# A3・A4 の手順（notes の A3_A4_確認手順.md、issue #2）に合わせた値。書いていないキーは 0
PRESETS = {
    "neutral": ("すべて 0（目は開き、口は閉じる）", {}),
    "a3-emulation-smile": ("エミュレーション: 笑い → 頬・えくぼ（規則 11、12）", {"MouthCornerPull": 1.0}),
    "a3-emulation-eyewide": ("エミュレーション: 目を見開く → 眉の外側（規則 15）", {"EyeWide": 1.0}),
    "a3-emulation-frown": ("エミュレーション: への字 → 困り眉（規則 14）", {"MouthFrown": 1.0}),
    "a3-limits-funnel": ("制限: すぼめ＋上唇を上げる → 上唇が抑えられる（規則 3）",
                         {"LipFunnel": 1.0, "MouthUpperUp": 1.0}),
    "a3-limits-tongue": ("制限: 舌を出す → 顎が 0.25 開く（規則 2）", {"TongueOut": 1.0}),
    "a3-limits-lipsuck": ("制限: 口を閉じて唇を巻き込む → 巻き込みが抑えられる（規則 6）",
                          {"MouthClosed": 1.0, "LipSuckUpper": 1.0, "LipSuckLower": 1.0}),
    "a3-eyesync-wink": ("Eye Sync: 右目だけ閉じる", {"EyeClosedRight": 1.0}),
    "a4-jaw-open": ("合成: 口を大きく開ける", {"JawOpen": 1.0}),
    "a4-jaw-half": ("合成: 口を半分開ける（足し算か大きい方かを分ける）", {"JawOpen": 0.5}),
}


def expand(name):
    """キー名を展開する。完全一致が無ければ Right/Left、Upper/Lower の組をまとめて指す

    例: MouthCornerPull → MouthCornerPullRight、MouthCornerPullLeft
        LipFunnel → LipFunnelUpperRight など 4 つ
        LipSuckUpper → LipSuckUpperRight、LipSuckUpperLeft
    """
    if name in KEY_INDEX:
        return [name]
    candidates = []
    for middle in ("", "Upper", "Lower"):
        for side in ("Right", "Left"):
            key = name + middle + side
            if key in KEY_INDEX:
                candidates.append(key)
    if not candidates:
        raise ValueError(f"キーが見つからない: {name}")
    return candidates


def resolve(values):
    result = {}
    for name, value in values.items():
        for key in expand(name):
            result[key] = value
    return result


def osc_string(text):
    data = text.encode("ascii") + b"\0"
    return data + b"\0" * (-len(data) % 4)


def osc_blob_message(address, blob):
    blob_part = struct.pack(">i", len(blob)) + blob + b"\0" * (-len(blob) % 4)
    return osc_string(address) + osc_string(",b") + blob_part


def encode(values):
    """キー名 → 0〜1 の値 を 102 バイトにする。VRCFT と同じく切り詰めてから 255 倍して切り捨てる"""
    blob = bytearray(len(KEYS))
    for key, value in values.items():
        clamped = min(max(float(value), 0.0), 1.0)
        blob[KEY_INDEX[key]] = int(clamped * 255)
    return bytes(blob)


class Sender:
    """値を一定の間隔で送り続ける。stop() で送信そのものを止める（確認項目 3 の「データが来ない」）"""

    def __init__(self, host, port, rate):
        self.address = (host, port)
        self.interval = 1.0 / rate
        self.values = {}
        self.sine_keys = set()
        self.sine_period = 2.0
        self.sending = False
        self.lock = threading.Lock()
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.thread = None
        self.started_at = time.monotonic()

    def set_values(self, values):
        with self.lock:
            self.values = dict(values)

    def set_value(self, key, value):
        with self.lock:
            if value <= 0:
                self.values.pop(key, None)
            else:
                self.values[key] = value

    def current(self):
        with self.lock:
            values = dict(self.values)
            sine_keys = set(self.sine_keys)
            period = self.sine_period
        if sine_keys:
            phase = (time.monotonic() - self.started_at) / max(period, 0.1)
            wave = 0.5 - 0.5 * math.cos(2 * math.pi * phase)
            for key in sine_keys:
                values[key] = values.get(key, 1.0) * wave if key in values else wave
        return values

    def send_once(self):
        message = osc_blob_message(ADDRESS, encode(self.current()))
        self.socket.sendto(message, self.address)

    def _loop(self):
        while self.sending:
            self.send_once()
            time.sleep(self.interval)

    def start(self):
        if self.sending:
            return
        self.sending = True
        self.thread = threading.Thread(target=self._loop, daemon=True)
        self.thread.start()

    def stop(self):
        self.sending = False


def parse_set(items):
    values = {}
    for item in items:
        if "=" not in item:
            raise ValueError(f"--set は KEY=VALUE の形: {item}")
        name, value = item.split("=", 1)
        values[name.strip()] = float(value)
    return values


def run_cli(args, values):
    sender = Sender(args.host, args.port, args.rate)
    resolved = resolve(values)
    sender.set_values(resolved)
    if args.sine:
        sender.sine_keys = set(sum((expand(n) for n in args.sine), []))
        sender.sine_period = args.sine_period
    shown = ", ".join(f"{k}={v:g}" for k, v in sorted(resolved.items())) or "すべて 0"
    print(f"{args.host}:{args.port} {ADDRESS} へ {args.rate:g} Hz で送信: {shown}")
    if sender.sine_keys:
        print(f"サイン波（周期 {sender.sine_period:g} 秒）: {', '.join(sorted(sender.sine_keys))}")
    print("Ctrl+C で止める")
    sender.start()
    try:
        if args.duration > 0:
            time.sleep(args.duration)
        else:
            while True:
                time.sleep(1)
    except KeyboardInterrupt:
        pass
    sender.stop()


def run_gui(args, values):
    import tkinter as tk
    from tkinter import ttk

    sender = Sender(args.host, args.port, args.rate)
    sender.set_values(resolve(values))

    root = tk.Tk()
    root.title("KxNFTM FT Sender")
    root.geometry("560x720")

    top = ttk.Frame(root, padding=8)
    top.pack(fill="x")

    status = tk.StringVar(value="停止中")
    preset_var = tk.StringVar(value="neutral")
    filter_var = tk.StringVar()
    period_var = tk.StringVar(value="2.0")
    scales = {}
    sine_vars = {}

    def refresh_scales():
        current = sender.values
        for key, var in scales.items():
            var.set(current.get(key, 0.0))

    def toggle_send():
        if sender.sending:
            sender.stop()
            status.set("停止中（パケットを送っていない）")
            send_button.config(text="送信開始")
        else:
            sender.start()
            status.set(f"送信中 {args.host}:{args.port} {args.rate:g} Hz")
            send_button.config(text="送信停止")

    def apply_preset(*_):
        name = preset_var.get().split(" ", 1)[0]
        sender.set_values(resolve(PRESETS[name][1]))
        refresh_scales()

    def all_zero():
        sender.set_values({})
        refresh_scales()

    def update_sine(*_):
        with sender.lock:
            sender.sine_keys = {k for k, v in sine_vars.items() if v.get()}
            try:
                sender.sine_period = float(period_var.get())
            except ValueError:
                pass

    send_button = ttk.Button(top, text="送信開始", command=toggle_send)
    send_button.grid(row=0, column=0, sticky="w")
    ttk.Label(top, textvariable=status).grid(row=0, column=1, columnspan=3, sticky="w", padx=8)

    ttk.Label(top, text="プリセット").grid(row=1, column=0, sticky="w", pady=(8, 0))
    preset_box = ttk.Combobox(
        top, textvariable=preset_var, state="readonly", width=60,
        values=[f"{name} {desc}" for name, (desc, _) in PRESETS.items()])
    preset_box.grid(row=1, column=1, columnspan=3, sticky="we", pady=(8, 0))
    preset_box.bind("<<ComboboxSelected>>", apply_preset)

    ttk.Button(top, text="すべて 0", command=all_zero).grid(row=2, column=0, sticky="w", pady=4)
    ttk.Label(top, text="サイン波の周期（秒）").grid(row=2, column=1, sticky="e")
    period_entry = ttk.Entry(top, textvariable=period_var, width=6)
    period_entry.grid(row=2, column=2, sticky="w")
    period_var.trace_add("write", update_sine)
    ttk.Label(top, text="絞り込み").grid(row=3, column=0, sticky="w")
    ttk.Entry(top, textvariable=filter_var).grid(row=3, column=1, columnspan=3, sticky="we")
    top.columnconfigure(1, weight=1)

    container = ttk.Frame(root)
    container.pack(fill="both", expand=True)
    canvas = tk.Canvas(container, highlightthickness=0)
    scrollbar = ttk.Scrollbar(container, orient="vertical", command=canvas.yview)
    body = ttk.Frame(canvas, padding=(8, 0))
    body.bind("<Configure>", lambda e: canvas.configure(scrollregion=canvas.bbox("all")))
    canvas.create_window((0, 0), window=body, anchor="nw")
    canvas.configure(yscrollcommand=scrollbar.set)
    canvas.pack(side="left", fill="both", expand=True)
    scrollbar.pack(side="right", fill="y")
    canvas.bind_all("<MouseWheel>", lambda e: canvas.yview_scroll(int(-e.delta / 120), "units"))

    rows = {}
    for i, key in enumerate(KEYS):
        var = tk.DoubleVar(value=sender.values.get(key, 0.0))
        scales[key] = var
        sine = tk.BooleanVar(value=False)
        sine_vars[key] = sine
        label = ttk.Label(body, text=f"{i:3d} {key}", width=28)
        scale = ttk.Scale(body, from_=0.0, to=1.0, variable=var, length=220,
                          command=lambda v, k=key: sender.set_value(k, float(v)))
        value_label = ttk.Label(body, width=5)
        var.trace_add("write", lambda *_, v=var, l=value_label: l.config(text=f"{v.get():.2f}"))
        value_label.config(text=f"{var.get():.2f}")
        check = ttk.Checkbutton(body, text="sin", variable=sine, command=update_sine)
        rows[key] = (label, scale, value_label, check)

    def layout(*_):
        text = filter_var.get().lower()
        row = 0
        for key, widgets in rows.items():
            visible = text in key.lower()
            for column, widget in enumerate(widgets):
                if visible:
                    widget.grid(row=row, column=column, sticky="w", pady=1)
                else:
                    widget.grid_remove()
            if visible:
                row += 1

    filter_var.trace_add("write", layout)
    layout()
    refresh_scales()

    def on_close():
        sender.stop()
        root.destroy()

    root.protocol("WM_DELETE_WINDOW", on_close)
    root.mainloop()


def main(argv=None):
    parser = argparse.ArgumentParser(description="ネイティブ FT の確認用に固定値の顔データを送る")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=9000, help="VRChat の OSC 受信ポート（既定 9000）")
    parser.add_argument("--rate", type=float, default=60.0, help="送信の頻度（Hz）")
    parser.add_argument("--preset", choices=sorted(PRESETS), help="プリセットの値を送る")
    parser.add_argument("--set", action="append", default=[], metavar="KEY=VALUE",
                        help="キーの値（0〜1）。Right/Left などの組はまとめて書ける。何度でも指定できる")
    parser.add_argument("--sine", action="append", default=[], metavar="KEY",
                        help="このキーをサイン波で 0〜1 に動かす")
    parser.add_argument("--sine-period", type=float, default=2.0, help="サイン波の周期（秒）")
    parser.add_argument("--duration", type=float, default=0.0, help="この秒数だけ送って終わる（0 なら Ctrl+C まで）")
    parser.add_argument("--list-presets", action="store_true")
    parser.add_argument("--list-keys", action="store_true")
    parser.add_argument("--gui", action="store_true", help="GUI を開く（引数が無いときの既定）")
    args = parser.parse_args(argv)

    if args.list_presets:
        for name, (desc, values) in PRESETS.items():
            shown = ", ".join(f"{k}={v:g}" for k, v in values.items()) or "-"
            print(f"{name:24} {desc}\n{'':24} {shown}")
        return 0
    if args.list_keys:
        for i, key in enumerate(KEYS):
            print(f"{i:3d} {key}")
        return 0

    try:
        values = dict(PRESETS[args.preset][1]) if args.preset else {}
        values.update(parse_set(args.set))
        resolve(values)
        for name in args.sine:
            expand(name)
    except ValueError as error:
        parser.error(str(error))

    has_cli_input = args.preset or args.set or args.sine or args.duration > 0
    if args.gui or not has_cli_input:
        run_gui(args, values)
    else:
        run_cli(args, values)
    return 0


if __name__ == "__main__":
    sys.exit(main())
