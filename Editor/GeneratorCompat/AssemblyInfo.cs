// Kx VRC ARKit BlendShape Generatorとの連携（仕様書10.1）を置くアセンブリ。
//
// Generatorが入っているプロジェクトでだけコンパイルされる（asmdefのdefineConstraints）。
// versionDefinesの下限は、GeneratorがSet名を公開定数（FaceTrackingSetNames）にし、
// Set書き込み（writeFaceTrackingBlendshapesSet）を含む版（仕様書16章のP1）である。
// 現在の [0.2.1,1.0.0) は、P1を含む次のリリースを0.2.1と見込んだ仮値で、リリースの版に合わせて直す。
