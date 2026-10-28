// Avatar Optimizer対策（仕様書11章）を置くアセンブリ。
//
// AAOが入っているプロジェクトでだけコンパイルされる（asmdefのdefineConstraints）。
// 範囲 [1.6.0,2.0.0) はGeneratorのAAOCompatと同じ。AAO本家がネイティブFTに対応したら上限を絞る。
//
// GeneratorのAAOCompatがParameter Behaviorsまで扱う版（仕様書16章のP2）を判定する
// define（KX_ARKIT_GENERATOR_AAO_FULL）は、P2のリリースで版が決まってから足す。
// 対策の処理はM3で実装する。
