"""ft_sender の送信形式のテスト（python -m unittest test_ft_sender）"""

import socket
import struct
import unittest

import ft_sender


def parse_osc_blob(packet):
    def read_string(offset):
        end = packet.index(b"\0", offset)
        text = packet[offset:end].decode("ascii")
        return text, (end + 4) & ~3

    address, offset = read_string(0)
    tags, offset = read_string(offset)
    (size,) = struct.unpack(">i", packet[offset:offset + 4])
    blob = packet[offset + 4:offset + 4 + size]
    assert len(packet) == offset + 4 + size + (-size % 4)
    return address, tags, blob


class FtSenderTests(unittest.TestCase):
    def test_key_order_matches_sdk(self):
        # VRCAvatarDescriptor.FaceTrackingKey の値（SDK 3.10.6）
        self.assertEqual(ft_sender.KEY_INDEX["EyeClosedRight"], 8)
        self.assertEqual(ft_sender.KEY_INDEX["EyeSquintRight"], 14)
        self.assertEqual(ft_sender.KEY_INDEX["JawOpen"], 36)
        self.assertEqual(ft_sender.KEY_INDEX["MouthCornerPullRight"], 70)
        self.assertEqual(ft_sender.KEY_INDEX["TongueOut"], 86)
        self.assertEqual(ft_sender.KEY_INDEX["NeckFlexLeft"], 101)

    def test_encode_quantizes_like_vrcft(self):
        blob = ft_sender.encode({"JawOpen": 0.5, "TongueOut": 1.5, "MouthClosed": -1})
        self.assertEqual(len(blob), 102)
        self.assertEqual(blob[36], 127)  # (byte)(0.5 * 255)
        self.assertEqual(blob[86], 255)
        self.assertEqual(blob[43], 0)

    def test_expand_pairs(self):
        self.assertEqual(ft_sender.expand("MouthCornerPull"), ["MouthCornerPullRight", "MouthCornerPullLeft"])
        self.assertEqual(len(ft_sender.expand("LipFunnel")), 4)
        self.assertEqual(ft_sender.expand("LipSuckUpper"), ["LipSuckUpperRight", "LipSuckUpperLeft"])
        self.assertEqual(ft_sender.expand("JawOpen"), ["JawOpen"])
        with self.assertRaises(ValueError):
            ft_sender.expand("NoSuchKey")

    def test_presets_resolve(self):
        for name, (_, values) in ft_sender.PRESETS.items():
            with self.subTest(name):
                ft_sender.resolve(values)

    def test_packet_over_udp(self):
        receiver = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        receiver.bind(("127.0.0.1", 0))
        receiver.settimeout(2)
        sender = ft_sender.Sender("127.0.0.1", receiver.getsockname()[1], 60)
        sender.set_values(ft_sender.resolve({"MouthCornerPull": 1.0}))
        sender.send_once()
        address, tags, blob = parse_osc_blob(receiver.recv(1024))
        self.assertEqual(address, "/tracking/face/v1")
        self.assertEqual(tags, ",b")
        self.assertEqual(blob[70], 255)
        self.assertEqual(blob[71], 255)
        self.assertEqual(sum(blob), 510)

    def test_restart_does_not_leave_old_loop(self):
        sender = ft_sender.Sender("127.0.0.1", 9, 10)
        for _ in range(5):
            sender.start()
            sender.stop()
        sender.start()
        alive = [t for t in __import__("threading").enumerate() if t.name.startswith("Thread") and t.is_alive() and t.daemon]
        sender.stop()
        self.assertEqual(len(alive), 1)
        self.assertFalse(sender.sending)


if __name__ == "__main__":
    unittest.main()
