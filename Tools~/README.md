# 実機確認用の道具

issue #2（M0 実機確認）の確認項目を、VRChat のクライアントで確かめるための道具を置く。
フォルダ名の末尾に `~` を付けているので、Unity はこのフォルダを取り込まない。
配布物（VPM 用 zip、booth 用 zip）にも含まれない。

| フォルダ | 中身 | 使う確認項目 |
| ---- | ---- | ---- |
| `ft-sender/` | 固定値の顔データを VRChat へ送るスクリプト | 1、3、5、7、11〜13 など、入力の値を固定したいもの |
| `test-write-layer/` | FT と同じシェイプへ 0／50／100 を書き続ける FX レイヤーを置くエディタ拡張 | 1（アニメーターと FT の出力の合成） |

## ft-sender: 顔データの送信

VRCFT の `BinaryFaceDataSender` と同じ形式で、OSC アドレス `/tracking/face/v1` に 102 バイトの blob を送る。
バイト i は `VRCAvatarDescriptor.FaceTrackingKey` の i 番目のキーの値で、0〜1 を 0〜255 に量子化したものである。
VRCFT（2026-10-29 時点の master）の `UnifiedTrackingData.CopyTo` と同じ並びと量子化にしている。

Python 3（標準ライブラリだけ）で動く。GUI には tkinter を使う。Windows の公式インストーラーの Python なら同梱されている。

**使う前に VRCFT を止める。** 両方が送ると値が交互に入れ替わり、結果を判定できない。

```
python ft_sender.py                                  # GUI を開く
python ft_sender.py --list-presets                   # プリセットの一覧
python ft_sender.py --preset a4-jaw-half             # プリセットを送り続ける（Ctrl+C で止める）
python ft_sender.py --set JawOpen=0.5 --set MouthCornerPull=1
python ft_sender.py --sine EyeClosedRight --sine-period 1.5
```

- キー名は SDK の名前で書く（`python ft_sender.py --list-keys`）。`MouthCornerPull` のように Right と Left の組の共通部分を書くと両方を指す。`LipFunnel` は上下左右の 4 つ、`LipSuckUpper` は上の左右 2 つを指す
- GUI では、スライダーで各キーの値を決め、「送信開始」で送り始める。「送信停止」はパケットそのものを止めるので、確認項目 3（データが来ないときの挙動）の再現に使える。「sin」にチェックしたキーは、周期の秒数でサイン波になる
- 送り先は既定で `127.0.0.1:9000`（VRChat の OSC 受信ポート）。変えるときは `--host` と `--port` を付ける
- テストは `python -m unittest test_ft_sender` で実行する

VRCFT は、アバターが Use All Tracking Keys を立てているとき（OSCQuery の `avatarfullfacetracking`）だけこの blob を送る。
このスクリプトはアバターを見ずに送り続ける。

## test-write-layer: テスト用レイヤー

確認項目 1 を確かめるために、FT が動かすのと同じシェイプへ固定値を書き続ける FX レイヤーを置く。
レイヤーは MA Merge Animator と MA Menu Item を持つ GameObject（`KxNFTMTest Write`）1 つにまとめてアバターの直下に置くので、確認が終わったらこれを消せば元に戻る。

1. `test-write-layer/` フォルダを、確認に使う Unity プロジェクトの `Assets/` の下へコピーする
2. Unity のメニューの Tools > Kx VRC NativeFT Manager > Verification > Test Write Layer を開く
3. アバターを選び、FT のキー（既定は JawOpen）と、そのキーで FT が動かすシェイプを指定する
   - Descriptor に Set があれば、その割り当てを候補に出す。Generator や Manager がビルド時に Set を作る構成ではシーンの Descriptor に Set が無いので、シェイプ名を一覧か手入力で指定する
4. 「テスト用レイヤーを置く」を押す。生成物は `Assets/KxNFTMTest/Generated/<アバター名>/` にできる
5. 確認が終わったら「取り除く」を押す。GameObject と生成フォルダを消す。コピーした `test-write-layer/` も消してよい

EX メニューの `Test Write` サブメニューに、次の 3 つのトグルができる。パラメーター `KxNFTMTest/Write`（Int、ローカルのみ、保存なし）の値で切り替える。

| トグル | 値 | 書く値 |
| ---- | ---- | ---- |
| （どれもオフ） | 0 | 何も書かない |
| Write 0 | 1 | 0 |
| Write 50 | 2 | 50 |
| Write 100 | 3 | 100 |

レイヤーは MA Merge Animator のレイヤー優先度を高くして FX の一番下に置き、アニメーター側でいちばん後に評価されるようにしている。
ステートの Write Defaults はすべてオフである。
そのため、Write 100 などから「どれもオフ」に戻しても、最後に書いた値がアニメーター側に残ることがある。
FT だけの状態と比べるときは、アバターを読み込み直してから、テスト用レイヤーを一度も動かしていない状態で見る。
