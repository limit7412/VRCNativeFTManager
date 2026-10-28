using System.Runtime.CompilerServices;

// Editorアセンブリからinternalメンバー（EditorOnValidateHook）へアクセスするため
[assembly: InternalsVisibleTo("NativeFTManager.Editor")]

// テストアセンブリからinternalメンバーへアクセスするため
[assembly: InternalsVisibleTo("NativeFTManager.Editor.Tests")]
