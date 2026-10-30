using System.Runtime.CompilerServices;

// テストアセンブリからinternal型（Domain、UseCase、Infra）へアクセスするため
[assembly: InternalsVisibleTo("NativeFTManager.Editor.Tests")]
