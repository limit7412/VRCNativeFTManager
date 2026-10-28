using System.Runtime.CompilerServices;

// テストアセンブリからinternal型（Domain、UseCase、Infra）へアクセスするため
[assembly: InternalsVisibleTo("NativeFTManager.Editor.Tests")]

// 連携用アセンブリから、Localizationなどのinternal型へアクセスするため
[assembly: InternalsVisibleTo("NativeFTManager.Editor.GeneratorCompat")]
[assembly: InternalsVisibleTo("NativeFTManager.Editor.AAOCompat")]
